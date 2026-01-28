using System;
using System.Linq;
using System.Threading.Tasks;
using repository_before;

namespace RepositoryBefore;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        await using var context = new EmployeeMatcher.EmployeeDbContext();
        var matcher = new EmployeeMatcher(context);

        var requirements = new ProjectRequirements
        {
            RequiredSkills = { "C#", "Azure", "React" },
            TimeSlots =
            {
                new TimeSlot
                {
                    Start = DateTime.Today.AddHours(9),
                    End = DateTime.Today.AddHours(12)
                },
                new TimeSlot
                {
                    Start = DateTime.Today.AddDays(1).AddHours(13),
                    End = DateTime.Today.AddDays(1).AddHours(17)
                }
            }
        };

        var matches = await matcher.FindBestEmployeesForProject(requirements);

        if (matches.Count == 0)
        {
            Console.WriteLine("No employees matched the sample project requirements.");
            return;
        }

        Console.WriteLine($"Top {matches.Count} employee matches:");
        foreach (var match in matches)
        {
            var highlightedSkills = string.Join(", ", match.Employee.Skills.Take(5));
            Console.WriteLine($"{match.Employee.Name} - Score {match.Score} - Skills: {highlightedSkills}");
        }
    }
}
