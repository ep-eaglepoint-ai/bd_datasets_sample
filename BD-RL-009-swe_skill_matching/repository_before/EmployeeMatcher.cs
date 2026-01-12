using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace repository_before
{
    public class EmployeeMatcher
    {
        private readonly EmployeeDbContext _context;
        private readonly int _threshold;

        public EmployeeMatcher(EmployeeDbContext context, int threshold = 15)
        {
            _context = context;
            _threshold = threshold;
        }

        public async Task<List<EmployeeMatch>> FindBestEmployeesForProject(ProjectRequirements projectRequirements, CancellationToken cancellationToken = default)
        {
            await _context.EnsureCreatedAndSeededAsync();
            var allEmployees = await _context.GetAllEmployeesAsync(cancellationToken); // 10,000+ employees
            var matches = new List<EmployeeMatch>();

            foreach (var employee in allEmployees)
            {
                int matchScore = 0;

                foreach (var reqSkill in projectRequirements.RequiredSkills)
                {
                    if (employee.Skills.Contains(reqSkill))
                    {
                        matchScore += 10;
                    }
                }

                foreach (var reqSlot in projectRequirements.TimeSlots)
                {
                    foreach (var empSlot in employee.Availability)
                    {
                        if (AreSlotsOverlapping(reqSlot, empSlot))
                        {
                            matchScore += 5;
                            break;
                        }
                    }
                }

                if (matchScore > _threshold)
                {
                    matches.Add(new EmployeeMatch { Employee = employee, Score = matchScore });
                }
            }

            return matches
                .OrderByDescending(m => m.Score)
                .Take(20)
                .ToList();
        }

        private bool AreSlotsOverlapping(TimeSlot slot1, TimeSlot slot2)
        {
            return slot1.Start < slot2.End && slot2.Start < slot1.End;
        }

        public class EmployeeDbContext : DbContext
        {
            private readonly string _databasePath;
            private readonly bool _ownsDatabaseFile;
            private readonly EventHandler? _processExitHandler;
            private bool _databaseCleanupCompleted;

            public DbSet<EmployeeProfile> Employees => Set<EmployeeProfile>();
            public DbSet<EmployeeSkill> Skills => Set<EmployeeSkill>();
            public DbSet<EmployeeAvailabilitySlot> AvailabilitySlots => Set<EmployeeAvailabilitySlot>();

            public EmployeeDbContext(string? databasePath = null, bool deleteDatabaseOnDispose = true)
            {
                var defaultDatabasePath = Path.Combine(AppContext.BaseDirectory, "employee_profiles.sqlite");
                _databasePath = databasePath ?? defaultDatabasePath;
                _ownsDatabaseFile = deleteDatabaseOnDispose && databasePath is null;

                var directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (_ownsDatabaseFile)
                {
                    _processExitHandler = OnProcessExit;
                    AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
                }
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                if (!optionsBuilder.IsConfigured)
                {
                    optionsBuilder.UseSqlite($"Data Source={_databasePath}");
                }
            }

            public async Task EnsureCreatedAndSeededAsync()
            {
                await Database.EnsureCreatedAsync();

                if (!await Employees.AnyAsync())
                {
                    await GenerateMockData();
                }
            }

            public async Task<List<Employee>> GetAllEmployeesAsync(CancellationToken cancellationToken = default)
            {
                var profiles = await Employees
                    .Include(e => e.Skills)
                    .Include(e => e.Availability)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                return profiles.Select(profile => new Employee
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    Skills = profile.Skills
                        .Select(s => s.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Availability = profile.Availability
                        .Select(a => new TimeSlot { Start = a.Start, End = a.End })
                        .ToList()
                }).ToList();
            }

            private async Task GenerateMockData()
            {
                const int totalEmployees = 1000;
                var random = new Random(99);
                var skillPool = new[]
                {
                    "C#", "Java", "Python", "SQL", "JavaScript", "React", "Angular", "Azure",
                    "AWS", "GCP", "Kubernetes", "Docker", "Go", "Rust", "Project Management", "QA Automation"
                };

                var employees = new List<EmployeeProfile>(totalEmployees);

                for (int i = 1; i <= totalEmployees; i++)
                {
                    var employee = new EmployeeProfile
                    {
                        Name = $"Employee {i:D5}"
                    };

                    var skillCount = random.Next(3, 7);
                    var pickedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    while (pickedSkills.Count < skillCount)
                    {
                        pickedSkills.Add(skillPool[random.Next(skillPool.Length)]);
                    }

                    foreach (var skill in pickedSkills)
                    {
                        employee.Skills.Add(new EmployeeSkill { Name = skill });
                    }

                    var slotCount = random.Next(2, 6);
                    for (int j = 0; j < slotCount; j++)
                    {
                        var start = DateTime.Today
                            .AddDays(random.Next(0, 30))
                            .AddHours(random.Next(0, 8) * 3);
                        var durationHours = random.Next(1, 4);
                        employee.Availability.Add(new EmployeeAvailabilitySlot
                        {
                            Start = start,
                            End = start.AddHours(durationHours)
                        });
                    }

                    employees.Add(employee);
                }

                await Employees.AddRangeAsync(employees);
                await SaveChangesAsync();
            }

            public override void Dispose()
            {
                base.Dispose();
                CleanupDatabaseFile();
            }

            public override async ValueTask DisposeAsync()
            {
                await base.DisposeAsync();
                CleanupDatabaseFile();
            }

            private void OnProcessExit(object? sender, EventArgs e)
            {
                CleanupDatabaseFile();
            }

            private void CleanupDatabaseFile()
            {
                if (!_ownsDatabaseFile || _databaseCleanupCompleted)
                {
                    return;
                }

                try
                {
                    if (File.Exists(_databasePath))
                    {
                        File.Delete(_databasePath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Unable to delete SQLite database at '{_databasePath}': {ex.Message}");
                }
                finally
                {
                    if (_processExitHandler is not null)
                    {
                        AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
                    }

                    _databaseCleanupCompleted = true;
                }
            }
        }
    }

    public class ProjectRequirements
    {
        public List<string> RequiredSkills { get; set; } = new List<string>();
        public List<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
    }

    public class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Skills { get; set; } = new List<string>();
        public List<TimeSlot> Availability { get; set; } = new List<TimeSlot>();
    }

    public class TimeSlot
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class EmployeeMatch
    {
        public Employee Employee { get; set; } = new Employee();
        public int Score { get; set; }
    }

    public class EmployeeProfile
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<EmployeeSkill> Skills { get; set; } = new List<EmployeeSkill>();
        public List<EmployeeAvailabilitySlot> Availability { get; set; } = new List<EmployeeAvailabilitySlot>();
    }

    public class EmployeeSkill
    {
        public int Id { get; set; }
        public int EmployeeProfileId { get; set; }
        public string Name { get; set; } = string.Empty;
        public EmployeeProfile? Employee { get; set; }
    }

    public class EmployeeAvailabilitySlot
    {
        public int Id { get; set; }
        public int EmployeeProfileId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public EmployeeProfile? Employee { get; set; }
    }
}
