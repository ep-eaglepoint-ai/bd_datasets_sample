extern alias repository_after;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using EmployeeMatcher = repository_after::repository_after.EmployeeMatcher;
using ProjectRequirements = repository_after::repository_after.ProjectRequirements;
using TimeSlot = repository_after::repository_after.TimeSlot;
using Employee = repository_after::repository_after.Employee;
using BeforeEmployeeMatcher = repository_before.EmployeeMatcher;
using BeforeProjectRequirements = repository_before.ProjectRequirements;
using BeforeTimeSlot = repository_before.TimeSlot;

namespace tests;

[CollectionDefinition("EmployeeMatcherAfterEMPerformance", DisableParallelization = true)]
public sealed class EmployeeMatcherAfterEMPerformanceCollectionDefinition
{
}

[Collection("EmployeeMatcherAfterEMPerformance")]
public sealed class RepositoryAfterEMPerformanceTest : IAsyncLifetime
{
    private string _dbPath = string.Empty;
    private string _beforeDbPath = string.Empty;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"employee_matcher_after_{Guid.NewGuid():N}.sqlite");
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
        await context.EnsureCreatedAndSeededAsync();

        _beforeDbPath = Path.Combine(Path.GetTempPath(), $"employee_matcher_{Guid.NewGuid():N}.sqlite");
        if (File.Exists(_beforeDbPath))
        {
            File.Delete(_beforeDbPath);
        }

        await using var beforeContext = new BeforeEmployeeMatcher.EmployeeDbContext(_beforeDbPath, deleteDatabaseOnDispose: false);
        await beforeContext.EnsureCreatedAndSeededAsync();
    }

    public Task DisposeAsync()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup failures to keep tests resilient.
        }

        try
        {
            if (File.Exists(_beforeDbPath))
            {
                File.Delete(_beforeDbPath);
            }
        }
        catch
        {
            // Ignore cleanup failures to keep tests resilient.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task RepositoryAfterEMPerformanceTest_FindBestEmployees_IsFasterThanBaselineImplementation()
    {
        var afterRequirements = BuildRequirements(requiredSkillsCount: 640, requiredTimeSlotsCount: 80);
        var beforeRequirements = ConvertToBeforeRequirements(afterRequirements);

        var afterMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
                var matcher = new EmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(afterRequirements);
            });

        var beforeMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new BeforeEmployeeMatcher.EmployeeDbContext(_beforeDbPath, deleteDatabaseOnDispose: false);
                var matcher = new BeforeEmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(beforeRequirements);
            });

        var ratio = afterMedian.TotalMilliseconds / Math.Max(1.0, beforeMedian.TotalMilliseconds);
        Assert.True(
            ratio < 1.3,
            $"Optimized implementation should be faster. After {afterMedian.TotalMilliseconds:F0} ms, " +
            $"before {beforeMedian.TotalMilliseconds:F0} ms, ratio {ratio:F2}x.");
    }

    [Fact]
    public async Task RepositoryAfterEMPerformanceTest_FindBestEmployees_IsFasterThanBaselineImplementation_ModerateLoad()
    {
        var afterRequirements = BuildRequirements(requiredSkillsCount: 320, requiredTimeSlotsCount: 40);
        var beforeRequirements = ConvertToBeforeRequirements(afterRequirements);

        var afterMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
                var matcher = new EmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(afterRequirements);
            });

        var beforeMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new BeforeEmployeeMatcher.EmployeeDbContext(_beforeDbPath, deleteDatabaseOnDispose: false);
                var matcher = new BeforeEmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(beforeRequirements);
            });

        var ratio = afterMedian.TotalMilliseconds / Math.Max(1.0, beforeMedian.TotalMilliseconds);
        Assert.True(
            ratio < 1.15,
            $"Optimized implementation should be faster at moderate load. After {afterMedian.TotalMilliseconds:F0} ms, " +
            $"before {beforeMedian.TotalMilliseconds:F0} ms, ratio {ratio:F2}x.");
    }

    [Fact]
    public async Task RepositoryAfterEMPerformanceTest_FindBestEmployees_IsFasterThanBaselineImplementation_LightLoad()
    {
        var afterRequirements = BuildRequirements(requiredSkillsCount: 160, requiredTimeSlotsCount: 20);
        var beforeRequirements = ConvertToBeforeRequirements(afterRequirements);

        var afterMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
                var matcher = new EmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(afterRequirements);
            });

        var beforeMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new BeforeEmployeeMatcher.EmployeeDbContext(_beforeDbPath, deleteDatabaseOnDispose: false);
                var matcher = new BeforeEmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(beforeRequirements);
            });

        var ratio = afterMedian.TotalMilliseconds / Math.Max(1.0, beforeMedian.TotalMilliseconds);
        Assert.True(
            ratio < 1.1,
            $"Optimized implementation should be faster at light load. After {afterMedian.TotalMilliseconds:F0} ms, " +
            $"before {beforeMedian.TotalMilliseconds:F0} ms, ratio {ratio:F2}x.");
    }

    private static ProjectRequirements BuildBroadRequirements()
    {
        var requirements = new ProjectRequirements();
        requirements.RequiredSkills.AddRange(new[]
        {
            "C#", "Java", "Python", "SQL", "JavaScript", "React", "Azure", "AWS"
        });

        var baseStart = DateTime.Today.AddHours(9);
        requirements.TimeSlots.AddRange(Enumerable.Range(0, 5).Select(i =>
        {
            var start = baseStart.AddDays(i).AddHours(i);
            return new TimeSlot { Start = start, End = start.AddHours(2) };
        }));

        return requirements;
    }

    private static ProjectRequirements BuildRequirements(int requiredSkillsCount, int requiredTimeSlotsCount)
    {
        var skillPool = new[]
        {
            "C#", "Java", "Python", "SQL", "JavaScript", "React", "Angular", "Azure",
            "AWS", "GCP", "Kubernetes", "Docker", "Go", "Rust", "Project Management", "QA Automation"
        };

        var requirements = new ProjectRequirements();
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

            requirements.TimeSlots.Add(new TimeSlot
            {
                Start = start,
                End = start.AddHours(2)
            });
        }

        return requirements;
    }

    private static async Task<TimeSpan> MeasureMedianAsync(int iterations, Func<Task> action)
    {
        await action(); // warmup

        var samples = new List<long>(iterations);
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await action();
            sw.Stop();
            samples.Add(sw.ElapsedTicks);
        }

        samples.Sort();
        var medianTicks = samples[samples.Count / 2];
        return TimeSpan.FromTicks(medianTicks);
    }

    private async Task<Employee> GetDeterministicEmployeeAsync()
    {
        await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
        var employees = await context.GetAllEmployeesAsync();
        return employees.First();
    }

    private static BeforeProjectRequirements ConvertToBeforeRequirements(ProjectRequirements afterRequirements)
    {
        var before = new BeforeProjectRequirements();
        before.RequiredSkills.AddRange(afterRequirements.RequiredSkills);
        before.TimeSlots.AddRange(afterRequirements.TimeSlots.Select(slot => new BeforeTimeSlot
        {
            Start = slot.Start,
            End = slot.End
        }));
        return before;
    }
}
