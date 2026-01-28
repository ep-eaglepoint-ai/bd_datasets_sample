extern alias repository_after;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using repository_before;
using Xunit;
using AfterEmployeeMatcher = repository_after::repository_after.EmployeeMatcher;
using AfterProjectRequirements = repository_after::repository_after.ProjectRequirements;
using AfterTimeSlot = repository_after::repository_after.TimeSlot;

namespace tests;

[CollectionDefinition("EmployeeMatcherBeforeEMPerformance", DisableParallelization = true)]
public sealed class EmployeeMatcherBeforeEMPerformanceCollectionDefinition
{
}

[Collection("EmployeeMatcherBeforeEMPerformance")]
public sealed class RepositoryBeforeEMPerformanceTest : IAsyncLifetime
{
    private string _dbPath = string.Empty;
    private string _afterDbPath = string.Empty;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"employee_matcher_{Guid.NewGuid():N}.sqlite");
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
        await context.EnsureCreatedAndSeededAsync();

        _afterDbPath = Path.Combine(Path.GetTempPath(), $"employee_matcher_after_{Guid.NewGuid():N}.sqlite");
        if (File.Exists(_afterDbPath))
        {
            File.Delete(_afterDbPath);
        }

        await using var afterContext = new AfterEmployeeMatcher.EmployeeDbContext(_afterDbPath, deleteDatabaseOnDispose: false);
        await afterContext.EnsureCreatedAndSeededAsync();
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
            if (File.Exists(_afterDbPath))
            {
                File.Delete(_afterDbPath);
            }
        }
        catch
        {
            // Ignore cleanup failures to keep tests resilient.
        }

        return Task.CompletedTask;
    }
    
    [Fact]
    public async Task RepositoryBeforeEMPerformanceTest_FindBestEmployees_IsSlowerThanOptimizedImplementation()
    {
        var beforeRequirements = BuildRequirements(requiredSkillsCount: 640, requiredTimeSlotsCount: 80);
        var afterRequirements = ConvertToAfterRequirements(beforeRequirements);

        var beforeMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
                var matcher = new EmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(beforeRequirements);
            });

        var afterMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new AfterEmployeeMatcher.EmployeeDbContext(_afterDbPath, deleteDatabaseOnDispose: false);
                var matcher = new AfterEmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(afterRequirements);
            });

        var ratio = beforeMedian.TotalMilliseconds / Math.Max(1.0, afterMedian.TotalMilliseconds);
        Assert.True(
            ratio > 1.3,
            $"Optimized implementation should be faster. Before {beforeMedian.TotalMilliseconds:F0} ms, " +
            $"after {afterMedian.TotalMilliseconds:F0} ms, ratio {ratio:F2}x.");
    }

    [Fact]
    public async Task RepositoryBeforeEMPerformanceTest_FindBestEmployees_IsSlowerThanOptimizedImplementation_ModerateLoad()
    {
        var beforeRequirements = BuildRequirements(requiredSkillsCount: 320, requiredTimeSlotsCount: 40);
        var afterRequirements = ConvertToAfterRequirements(beforeRequirements);

        var beforeMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
                var matcher = new EmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(beforeRequirements);
            });

        var afterMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new AfterEmployeeMatcher.EmployeeDbContext(_afterDbPath, deleteDatabaseOnDispose: false);
                var matcher = new AfterEmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(afterRequirements);
            });

        var ratio = beforeMedian.TotalMilliseconds / Math.Max(1.0, afterMedian.TotalMilliseconds);
        Assert.True(
            ratio > 1.15,
            $"Optimized implementation should be faster at moderate load. Before {beforeMedian.TotalMilliseconds:F0} ms, " +
            $"after {afterMedian.TotalMilliseconds:F0} ms, ratio {ratio:F2}x.");
    }

    [Fact]
    public async Task RepositoryBeforeEMPerformanceTest_FindBestEmployees_IsSlowerThanOptimizedImplementation_LightLoad()
    {
        var beforeRequirements = BuildRequirements(requiredSkillsCount: 160, requiredTimeSlotsCount: 20);
        var afterRequirements = ConvertToAfterRequirements(beforeRequirements);

        var beforeMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new EmployeeMatcher.EmployeeDbContext(_dbPath, deleteDatabaseOnDispose: false);
                var matcher = new EmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(beforeRequirements);
            });

        var afterMedian = await MeasureMedianAsync(
            iterations: 3,
            action: async () =>
            {
                await using var context = new AfterEmployeeMatcher.EmployeeDbContext(_afterDbPath, deleteDatabaseOnDispose: false);
                var matcher = new AfterEmployeeMatcher(context, threshold: 15);
                await matcher.FindBestEmployeesForProject(afterRequirements);
            });

        var ratio = beforeMedian.TotalMilliseconds / Math.Max(1.0, afterMedian.TotalMilliseconds);
        Assert.True(
            ratio > 1.1,
            $"Optimized implementation should be faster at light load. Before {beforeMedian.TotalMilliseconds:F0} ms, " +
            $"after {afterMedian.TotalMilliseconds:F0} ms, ratio {ratio:F2}x.");
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
        return employees.First(); // Seeder uses Random(99), so the first employee is stable.
    }

    private static AfterProjectRequirements ConvertToAfterRequirements(ProjectRequirements beforeRequirements)
    {
        var after = new AfterProjectRequirements();
        after.RequiredSkills.AddRange(beforeRequirements.RequiredSkills);
        after.TimeSlots.AddRange(beforeRequirements.TimeSlots.Select(slot => new AfterTimeSlot
        {
            Start = slot.Start,
            End = slot.End
        }));
        return after;
    }
}
