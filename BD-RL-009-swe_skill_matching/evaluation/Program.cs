using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BeforeMatcher = repository_before.EmployeeMatcher;
using AfterMatcher = repository_after.EmployeeMatcher;
using BeforeRequirements = repository_before.ProjectRequirements;
using AfterRequirements = repository_after.ProjectRequirements;
using BeforeTimeSlot = repository_before.TimeSlot;
using AfterTimeSlot = repository_after.TimeSlot;

internal sealed record CliOptions(
    int Iterations,
    int Threshold,
    IReadOnlyList<int> RequiredSkills,
    IReadOnlyList<int> RequiredTimeSlots,
    string OutputDir);

internal sealed record ScenarioParameters(string Name, int RequiredSkills, int RequiredTimeSlots);

internal sealed record TimingSummary(double AvgMs, double MinMs, double MaxMs);

internal sealed record ScenarioResult(
    ScenarioParameters Parameters,
    TimingSummary Before,
    TimingSummary After,
    ComparisonMetrics Comparison);

internal sealed record ComparisonMetrics(double Speedup, double ImprovementPct);

internal sealed record SeedStats(int Employees, int Skills, int AvailabilitySlots);

internal sealed record SummaryMetrics(
    double BeforeAvgMs,
    double AfterAvgMs,
    double OverallSpeedup,
    double OverallImprovementPct);

internal sealed record MetricsBlock(
    SeedStats SeedStats,
    Dictionary<string, ScenarioResult> Scenarios,
    SummaryMetrics Summary);

internal sealed record EnvironmentInfo(
    string DotnetVersion,
    string OsDescription,
    string OsArchitecture,
    string ProcessArchitecture,
    string GitCommit,
    string GitBranch);

internal sealed record EvaluationParameters(
    int Iterations,
    int Threshold,
    string OutputDir,
    IReadOnlyList<ScenarioParameters> Scenarios);

internal sealed record EvaluationReport(
    string RunId,
    DateTime StartedAt,
    DateTime FinishedAt,
    double DurationSeconds,
    bool Success,
    string? Error,
    EvaluationParameters Parameters,
    EnvironmentInfo Environment,
    MetricsBlock? Metrics);

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        var runId = Guid.NewGuid().ToString("N")[..8];
        var startedAt = DateTime.UtcNow;
        var log = new StringBuilder();

        void Log(string message)
        {
            Console.WriteLine(message);
            log.AppendLine(message);
        }

        var localDate = startedAt.ToLocalTime();
        var outputRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(options.OutputDir) ? "evaluation" : options.OutputDir);
        var outputPath = Path.Combine(outputRoot, localDate.ToString("yyyy-MM-dd"), localDate.ToString("HH-mm-ss"));
        Directory.CreateDirectory(outputPath);

        Log(new string('=', 64));
        Log("EMPLOYEE MATCHER PERFORMANCE EVALUATION");
        Log(new string('=', 64));
        Log($"Run ID: {runId}");
        Log($"Started: {startedAt:O}");
        Log($"Output: {outputPath}");
        Log(new string('=', 64));
        Log($"Iterations: {options.Iterations}, Threshold: {options.Threshold}");
        Log($"Scenarios (skills/time slots): {string.Join(", ", options.RequiredSkills.Zip(options.RequiredTimeSlots, (s, t) => $"{s}/{t}"))}");
        Log(string.Empty);

        var environment = GetEnvironmentInfo();
        EvaluationReport report;
        MetricsBlock? metrics = null;
        bool success = false;
        string? error = null;

        try
        {
            var scenarios = BuildScenarioParameters(options.RequiredSkills, options.RequiredTimeSlots);
            var scenarioResults = new Dictionary<string, ScenarioResult>(StringComparer.OrdinalIgnoreCase);

            SeedStats? seedStats = null;

            foreach (var scenario in scenarios)
            {
                Log($"Running scenario '{scenario.Name}' (skills: {scenario.RequiredSkills}, slots: {scenario.RequiredTimeSlots})...");

                var beforeDbPath = Path.Combine(Path.GetTempPath(), $"employee_matcher_before_{Guid.NewGuid():N}.sqlite");
                var afterDbPath = Path.Combine(Path.GetTempPath(), $"employee_matcher_after_{Guid.NewGuid():N}.sqlite");

                try
                {
                    var beforeRequirements = BuildRequirements(scenario.RequiredSkills, scenario.RequiredTimeSlots);
                    var afterRequirements = ConvertToAfterRequirements(beforeRequirements);

                    // Seed both databases once per scenario.
                    await using (var ctx = new BeforeMatcher.EmployeeDbContext(beforeDbPath, deleteDatabaseOnDispose: false))
                    {
                        await ctx.EnsureCreatedAndSeededAsync();
                        seedStats ??= await CaptureSeedStatsAsync(ctx);
                    }

                    await using (var ctx = new AfterMatcher.EmployeeDbContext(afterDbPath, deleteDatabaseOnDispose: false))
                    {
                        await ctx.EnsureCreatedAndSeededAsync();
                    }

                    var beforeMetrics = await MeasureAsync(
                        options.Iterations,
                        async () =>
                        {
                            await using var ctx = new BeforeMatcher.EmployeeDbContext(beforeDbPath, deleteDatabaseOnDispose: false);
                            var matcher = new BeforeMatcher(ctx, options.Threshold);
                            await matcher.FindBestEmployeesForProject(beforeRequirements);
                        });

                    var afterMetrics = await MeasureAsync(
                        options.Iterations,
                        async () =>
                        {
                            await using var ctx = new AfterMatcher.EmployeeDbContext(afterDbPath, deleteDatabaseOnDispose: false);
                            var matcher = new AfterMatcher(ctx, options.Threshold);
                            await matcher.FindBestEmployeesForProject(afterRequirements);
                        });

                    var speedup = beforeMetrics.AvgMs > 0
                        ? beforeMetrics.AvgMs / Math.Max(1e-6, afterMetrics.AvgMs)
                        : 0;
                    var improvementPct = beforeMetrics.AvgMs > 0
                        ? ((beforeMetrics.AvgMs - afterMetrics.AvgMs) / beforeMetrics.AvgMs) * 100.0
                        : 0;

                    scenarioResults[scenario.Name] = new ScenarioResult(
                        scenario,
                        beforeMetrics,
                        afterMetrics,
                        new ComparisonMetrics(
                            Math.Round(speedup, 2),
                            Math.Round(improvementPct, 1)));

                    Log($"  ✓ Completed '{scenario.Name}': before {beforeMetrics.AvgMs:F2} ms, after {afterMetrics.AvgMs:F2} ms, speedup {speedup:F2}x");
                }
                finally
                {
                    TryDeleteFile(beforeDbPath);
                    TryDeleteFile(afterDbPath);
                }
            }

            metrics = BuildMetricsBlock(scenarioResults, seedStats ?? new SeedStats(0, 0, 0));
            success = true;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            Log("ERROR: " + ex.Message);
        }

        var finishedAt = DateTime.UtcNow;
        var durationSeconds = (finishedAt - startedAt).TotalSeconds;

        report = new EvaluationReport(
            runId,
            startedAt,
            finishedAt,
            durationSeconds,
            success,
            error,
            new EvaluationParameters(
                options.Iterations,
                options.Threshold,
                outputRoot,
                BuildScenarioParameters(options.RequiredSkills, options.RequiredTimeSlots)),
            environment,
            metrics);

        var jsonPath = Path.Combine(outputPath, "report.json");
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOptions));
        Log($"Saved JSON report: {jsonPath}");

        if (metrics is not null && success)
        {
            var mdPath = Path.Combine(outputPath, "report.md");
            await File.WriteAllTextAsync(mdPath, BuildMarkdownReport(report));
            Log($"Saved Markdown report: {mdPath}");
        }

        var logPath = Path.Combine(outputPath, "stdout.log");
        await File.WriteAllTextAsync(logPath, log.ToString());
        Log($"Saved console log: {logPath}");

        Log(new string('=', 64));
        Log($"EVALUATION COMPLETE (duration: {durationSeconds:F2}s)");
        Log(new string('=', 64));

        return success ? 0 : 1;
    }

    private static CliOptions ParseArgs(string[] args)
    {
        int iterations = 5;
        int threshold = 15;
        string outputDir = "evaluation";
        var skills = new List<int> { 160, 320, 640 };
        var timeSlots = new List<int> { 20, 40, 80 };

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--iterations" when i + 1 < args.Length && int.TryParse(args[i + 1], out var iter):
                    iterations = Math.Max(1, iter);
                    i++;
                    break;
                case "--threshold" when i + 1 < args.Length && int.TryParse(args[i + 1], out var th):
                    threshold = Math.Max(0, th);
                    i++;
                    break;
                case "--skills" when i + 1 < args.Length:
                    skills = ParseIntList(args[i + 1], skills);
                    i++;
                    break;
                case "--slots" when i + 1 < args.Length:
                    timeSlots = ParseIntList(args[i + 1], timeSlots);
                    i++;
                    break;
                case "--output-dir" when i + 1 < args.Length:
                    outputDir = args[i + 1];
                    i++;
                    break;
                default:
                    break;
            }
        }

        // Keep pairs aligned by the smaller count.
        var count = Math.Min(skills.Count, timeSlots.Count);
        if (count == 0)
        {
            skills = new List<int> { 160, 320, 640 };
            timeSlots = new List<int> { 20, 40, 80 };
            count = skills.Count;
        }

        return new CliOptions(
            iterations,
            threshold,
            skills.Take(count).ToArray(),
            timeSlots.Take(count).ToArray(),
            outputDir);
    }

    private static List<int> ParseIntList(string value, List<int> fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<int>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var parsed) && parsed > 0)
            {
                list.Add(parsed);
            }
        }

        return list.Count == 0 ? fallback : list;
    }

    private static IReadOnlyList<ScenarioParameters> BuildScenarioParameters(
        IReadOnlyList<int> skills,
        IReadOnlyList<int> slots)
    {
        var count = Math.Min(skills.Count, slots.Count);
        var scenarios = new List<ScenarioParameters>(count);
        for (int i = 0; i < count; i++)
        {
            var name = $"skills{skills[i]}_slots{slots[i]}";
            scenarios.Add(new ScenarioParameters(name, skills[i], slots[i]));
        }

        return scenarios;
    }

    private static async Task<TimingSummary> MeasureAsync(int iterations, Func<Task> action)
    {
        await action(); // warm-up

        var samples = new List<double>(iterations);
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await action();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var avg = samples.Average();
        var min = samples.First();
        var max = samples.Last();
        return new TimingSummary(
            Math.Round(avg, 2),
            Math.Round(min, 2),
            Math.Round(max, 2));
    }

    private static BeforeRequirements BuildRequirements(int requiredSkillsCount, int requiredTimeSlotsCount)
    {
        var skillPool = new[]
        {
            "C#", "Java", "Python", "SQL", "JavaScript", "React", "Angular", "Azure",
            "AWS", "GCP", "Kubernetes", "Docker", "Go", "Rust", "Project Management", "QA Automation"
        };

        var requirements = new BeforeRequirements();
        for (int i = 0; i < requiredSkillsCount; i++)
        {
            requirements.RequiredSkills.Add(skillPool[i % skillPool.Length]);
        }

        var baseStart = DateTime.Today.AddHours(9);
        for (int i = 0; i < requiredTimeSlotsCount; i++)
        {
            var start = baseStart
                .AddDays(i % 14)
                .AddHours((i % 4) * 2);

            requirements.TimeSlots.Add(new BeforeTimeSlot
            {
                Start = start,
                End = start.AddHours(2)
            });
        }

        return requirements;
    }

    private static AfterRequirements ConvertToAfterRequirements(BeforeRequirements beforeRequirements)
    {
        var after = new AfterRequirements();
        after.RequiredSkills.AddRange(beforeRequirements.RequiredSkills);
        after.TimeSlots.AddRange(beforeRequirements.TimeSlots.Select(slot => new AfterTimeSlot
        {
            Start = slot.Start,
            End = slot.End
        }));
        return after;
    }

    private static async Task<SeedStats> CaptureSeedStatsAsync(BeforeMatcher.EmployeeDbContext context)
    {
        var employees = await context.Employees.CountAsync();
        var skills = await context.Skills.CountAsync();
        var availability = await context.AvailabilitySlots.CountAsync();
        return new SeedStats(employees, skills, availability);
    }

    private static MetricsBlock BuildMetricsBlock(
        Dictionary<string, ScenarioResult> scenarios,
        SeedStats seedStats)
    {
        if (scenarios.Count == 0)
        {
            return new MetricsBlock(seedStats, scenarios, new SummaryMetrics(0, 0, 0, 0));
        }

        var beforeAvg = scenarios.Values.Average(s => s.Before.AvgMs);
        var afterAvg = scenarios.Values.Average(s => s.After.AvgMs);
        var overallSpeedup = afterAvg > 0 ? beforeAvg / afterAvg : 0;
        var overallImprovementPct = beforeAvg > 0 ? ((beforeAvg - afterAvg) / beforeAvg) * 100.0 : 0;

        return new MetricsBlock(
            seedStats,
            scenarios,
            new SummaryMetrics(
                Math.Round(beforeAvg, 2),
                Math.Round(afterAvg, 2),
                Math.Round(overallSpeedup, 2),
                Math.Round(overallImprovementPct, 1)));
    }

    private static EnvironmentInfo GetEnvironmentInfo()
    {
        return new EnvironmentInfo(
            DotnetVersion: Environment.Version.ToString(),
            OsDescription: RuntimeInformation.OSDescription,
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            GitCommit: RunGitCommand("rev-parse --short HEAD") ?? "unknown",
            GitBranch: RunGitCommand("rev-parse --abbrev-ref HEAD") ?? "unknown");
    }

    private static string? RunGitCommand(string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            if (process.ExitCode == 0)
            {
                return output.Trim();
            }
        }
        catch
        {
            // ignore git failures
        }

        return null;
    }

    private static string BuildMarkdownReport(EvaluationReport report)
    {
        if (report.Metrics is null)
        {
            return "# Performance Evaluation Report\n\nEvaluation failed before metrics were produced.";
        }

        var env = report.Environment;
        var summary = report.Metrics.Summary;
        var sb = new StringBuilder();

        sb.AppendLine("# Employee Matcher Performance Report");
        sb.AppendLine();
        sb.AppendLine($"**Run ID:** `{report.RunId}`  ");
        sb.AppendLine($"**Started:** {report.StartedAt:O}  ");
        sb.AppendLine($"**Finished:** {report.FinishedAt:O}  ");
        sb.AppendLine($"**Duration:** {report.DurationSeconds:F2} seconds");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Environment");
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| .NET | {env.DotnetVersion} |");
        sb.AppendLine($"| OS | {env.OsDescription} |");
        sb.AppendLine($"| OS Arch | {env.OsArchitecture} |");
        sb.AppendLine($"| Process Arch | {env.ProcessArchitecture} |");
        sb.AppendLine($"| Git Commit | `{env.GitCommit}` |");
        sb.AppendLine($"| Git Branch | `{env.GitBranch}` |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Parameters");
        sb.AppendLine();
        sb.AppendLine("| Parameter | Value |");
        sb.AppendLine("|-----------|-------|");
        sb.AppendLine($"| Iterations | {report.Parameters.Iterations} |");
        sb.AppendLine($"| Threshold | {report.Parameters.Threshold} |");
        sb.AppendLine($"| Scenarios | {string.Join(", ", report.Parameters.Scenarios.Select(s => s.Name))} |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Seed Statistics");
        sb.AppendLine();
        sb.AppendLine("| Entity | Count |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Employees | {report.Metrics.SeedStats.Employees} |");
        sb.AppendLine($"| Skills | {report.Metrics.SeedStats.Skills} |");
        sb.AppendLine($"| Availability Slots | {report.Metrics.SeedStats.AvailabilitySlots} |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Summary Results");
        sb.AppendLine();
        sb.AppendLine("| Metric | Before (Naive) | After (Optimized) | Improvement |");
        sb.AppendLine("|--------|----------------|-------------------|-------------|");
        sb.AppendLine($"| Average Response | {summary.BeforeAvgMs:F2} ms | {summary.AfterAvgMs:F2} ms | **{summary.OverallSpeedup:F2}x faster** |");
        sb.AppendLine($"| Improvement | - | - | {summary.OverallImprovementPct:F1}% |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Detailed Results by Scenario");
        sb.AppendLine();
        sb.AppendLine("| Scenario | Skills | Time Slots | Before (ms) | After (ms) | Speedup |");
        sb.AppendLine("|----------|--------|------------|-------------|------------|---------|");

        foreach (var kvp in report.Metrics.Scenarios.OrderBy(k => k.Key))
        {
            var result = kvp.Value;
            sb.AppendLine($"| {kvp.Key} | {result.Parameters.RequiredSkills} | {result.Parameters.RequiredTimeSlots} | {result.Before.AvgMs:F2} | {result.After.AvgMs:F2} | {result.Comparison.Speedup:F2}x |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Conclusion");
        sb.AppendLine();

        if (summary.OverallSpeedup >= 2.0)
        {
            sb.AppendLine($"✅ **Excellent optimization!** The optimized implementation is **{summary.OverallSpeedup:F2}x faster** than the naive version.");
        }
        else if (summary.OverallSpeedup >= 1.0)
        {
            sb.AppendLine($"✅ **Good optimization.** The optimized implementation is **{summary.OverallSpeedup:F2}x faster** than the naive version.");
        }
        else
        {
            sb.AppendLine($"⚠️ **Needs investigation.** The optimized implementation is slower than the naive version.");
        }

        return sb.ToString();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup best-effort only.
        }
    }
}
