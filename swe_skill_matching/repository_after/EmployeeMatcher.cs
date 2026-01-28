using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace repository_after
{
    public class EmployeeMatcher
    {
        private readonly EmployeeDbContext _context;
        private readonly int _threshold;

        public EmployeeMatcher(EmployeeDbContext context, int threshold = 15)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _threshold = threshold;
        }

        public async Task<List<EmployeeMatch>> FindBestEmployeesForProject(
            ProjectRequirements projectRequirements,
            CancellationToken cancellationToken = default)
        {
            if (projectRequirements is null) throw new ArgumentNullException(nameof(projectRequirements));

            await _context.EnsureCreatedAndSeededAsync().ConfigureAwait(false);

            var requiredSkills = projectRequirements.RequiredSkills ?? new List<string>();
            var requiredSlots = projectRequirements.TimeSlots ?? new List<TimeSlot>();

            if (requiredSkills.Count == 0 && requiredSlots.Count == 0)
            {
                return new List<EmployeeMatch>();
            }

            // Required skill multiplicity matters for scoring (duplicates add score multiple times).
            var reqSkillCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < requiredSkills.Count; i++)
            {
                var skill = requiredSkills[i];
                if (string.IsNullOrWhiteSpace(skill)) continue;

                if (reqSkillCounts.TryGetValue(skill, out var count))
                {
                    reqSkillCounts[skill] = count + 1;
                }
                else
                {
                    reqSkillCounts[skill] = 1;
                }
            }

            var reqSkillSet = reqSkillCounts.Count == 0
                ? Array.Empty<string>()
                : reqSkillCounts.Keys.ToArray();

            var reqSlotsSorted = BuildSortedSlots(requiredSlots);

            // Load only minimal employee rows (Id + Name).
            var employees = await _context.Employees
                .AsNoTracking()
                .Select(e => new EmployeeRow { Id = e.Id, Name = e.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Load skill rows for required skills only.
            Dictionary<int, string[]> skillsByEmployee = reqSkillSet.Length == 0
                ? new Dictionary<int, string[]>()
                : await LoadSkillsByEmployeeAsync(reqSkillSet, cancellationToken).ConfigureAwait(false);

            // Load availability rows in a coarse time window only (reduces scanning).
            Dictionary<int, SlotInterval[]> availabilityByEmployee = reqSlotsSorted.Length == 0
                ? new Dictionary<int, SlotInterval[]>()
                : await LoadAvailabilityByEmployeeAsync(reqSlotsSorted, cancellationToken).ConfigureAwait(false);

            // Top-K selection avoids sorting the full match list.
            const int topK = 20;
            var top = new PriorityQueue<EmployeeCandidate, int>();

            for (int i = 0; i < employees.Count; i++)
            {
                var emp = employees[i];
                int score = 0;

                if (skillsByEmployee.TryGetValue(emp.Id, out var empSkills))
                {
                    for (int s = 0; s < empSkills.Length; s++)
                    {
                        var skillName = empSkills[s];
                        if (reqSkillCounts.TryGetValue(skillName, out var mult))
                        {
                            score += mult * 10;
                        }
                    }
                }

                if (reqSlotsSorted.Length != 0 && availabilityByEmployee.TryGetValue(emp.Id, out var empSlots))
                {
                    score += ComputeAvailabilityScore(reqSlotsSorted, empSlots);
                }

                if (score <= _threshold)
                {
                    continue;
                }

                if (top.Count < topK)
                {
                    top.Enqueue(new EmployeeCandidate(emp.Id, score), score);
                    continue;
                }

                if (top.TryPeek(out var smallest, out var smallestScore) && score > smallestScore)
                {
                    top.Dequeue();
                    top.Enqueue(new EmployeeCandidate(emp.Id, score), score);
                }
            }

            if (top.Count == 0)
            {
                return new List<EmployeeMatch>();
            }

            // Extract top candidates and sort only K items.
            var candidates = new List<EmployeeCandidate>(top.Count);
            while (top.TryDequeue(out var cand, out _))
            {
                candidates.Add(cand);
            }
            candidates.Sort(static (a, b) => b.Score.CompareTo(a.Score));

            var topIds = candidates.Select(c => c.EmployeeId).ToArray();

            // Load full details only for returned employees.
            var profiles = await _context.Employees
                .AsNoTracking()
                .Where(e => topIds.Contains(e.Id))
                .Include(e => e.Skills)
                .Include(e => e.Availability)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var profileById = new Dictionary<int, EmployeeProfile>(profiles.Count);
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                profileById[p.Id] = p;
            }

            var results = new List<EmployeeMatch>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (!profileById.TryGetValue(cand.EmployeeId, out var profile))
                {
                    continue;
                }

                var mapped = new Employee
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
                };

                results.Add(new EmployeeMatch { Employee = mapped, Score = cand.Score });
            }

            return results;
        }

        private static SlotInterval[] BuildSortedSlots(List<TimeSlot> slots)
        {
            if (slots is null || slots.Count == 0) return Array.Empty<SlotInterval>();

            var list = new List<SlotInterval>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s is null) continue;
                if (s.End <= s.Start) continue;
                list.Add(new SlotInterval(s.Start, s.End));
            }

            list.Sort(static (a, b) =>
            {
                var cmp = a.Start.CompareTo(b.Start);
                if (cmp != 0) return cmp;
                return a.End.CompareTo(b.End);
            });

            return list.ToArray();
        }

        private async Task<Dictionary<int, string[]>> LoadSkillsByEmployeeAsync(
            string[] requiredSkillSet,
            CancellationToken cancellationToken)
        {
            var rows = await _context.Skills
                .AsNoTracking()
                .Where(s => requiredSkillSet.Contains(s.Name))
                .Select(s => new SkillRow { EmployeeId = s.EmployeeProfileId, Name = s.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var dict = new Dictionary<int, List<string>>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (!dict.TryGetValue(r.EmployeeId, out var list))
                {
                    list = new List<string>(6);
                    dict[r.EmployeeId] = list;
                }
                list.Add(r.Name);
            }

            var result = new Dictionary<int, string[]>(dict.Count);
            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value.ToArray();
            }

            return result;
        }

        private async Task<Dictionary<int, SlotInterval[]>> LoadAvailabilityByEmployeeAsync(
            SlotInterval[] requiredSlotsSorted,
            CancellationToken cancellationToken)
        {
            // Coarse window: only slots that overlap any requirement window are relevant.
            var minStart = requiredSlotsSorted[0].Start;
            var maxEnd = requiredSlotsSorted[0].End;
            for (int i = 1; i < requiredSlotsSorted.Length; i++)
            {
                var s = requiredSlotsSorted[i];
                if (s.Start < minStart) minStart = s.Start;
                if (s.End > maxEnd) maxEnd = s.End;
            }

            var rows = await _context.AvailabilitySlots
                .AsNoTracking()
                .Where(a => a.Start < maxEnd && a.End > minStart)
                .Select(a => new AvailabilityRow { EmployeeId = a.EmployeeProfileId, Start = a.Start, End = a.End })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var dict = new Dictionary<int, List<SlotInterval>>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.End <= r.Start) continue;

                if (!dict.TryGetValue(r.EmployeeId, out var list))
                {
                    list = new List<SlotInterval>(6);
                    dict[r.EmployeeId] = list;
                }

                list.Add(new SlotInterval(r.Start, r.End));
            }

            var result = new Dictionary<int, SlotInterval[]>(dict.Count);
            foreach (var kvp in dict)
            {
                var list = kvp.Value;
                list.Sort(static (a, b) =>
                {
                    var cmp = a.Start.CompareTo(b.Start);
                    if (cmp != 0) return cmp;
                    return a.End.CompareTo(b.End);
                });
                result[kvp.Key] = list.ToArray();
            }

            return result;
        }

        private static int ComputeAvailabilityScore(SlotInterval[] requiredSlotsSorted, SlotInterval[] employeeSlotsSorted)
        {
            if (requiredSlotsSorted.Length == 0 || employeeSlotsSorted.Length == 0) return 0;

            int score = 0;
            int empIdx = 0;

            for (int r = 0; r < requiredSlotsSorted.Length; r++)
            {
                var req = requiredSlotsSorted[r];

                while (empIdx < employeeSlotsSorted.Length && employeeSlotsSorted[empIdx].End <= req.Start)
                {
                    empIdx++;
                }

                if (empIdx >= employeeSlotsSorted.Length)
                {
                    break;
                }

                var emp = employeeSlotsSorted[empIdx];

                // Overlap: req.Start < emp.End && emp.Start < req.End
                if (req.Start < emp.End && emp.Start < req.End)
                {
                    score += 5;
                }
            }

            return score;
        }

        private readonly struct EmployeeRow
        {
            public int Id { get; init; }
            public string Name { get; init; }
        }

        private readonly struct SkillRow
        {
            public int EmployeeId { get; init; }
            public string Name { get; init; }
        }

        private readonly struct AvailabilityRow
        {
            public int EmployeeId { get; init; }
            public DateTime Start { get; init; }
            public DateTime End { get; init; }
        }

        private readonly struct SlotInterval
        {
            public SlotInterval(DateTime start, DateTime end)
            {
                Start = start;
                End = end;
            }

            public DateTime Start { get; }
            public DateTime End { get; }
        }

        private readonly struct EmployeeCandidate
        {
            public EmployeeCandidate(int employeeId, int score)
            {
                EmployeeId = employeeId;
                Score = score;
            }

            public int EmployeeId { get; }
            public int Score { get; }
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
                await Database.EnsureCreatedAsync().ConfigureAwait(false);

                if (!await Employees.AnyAsync().ConfigureAwait(false))
                {
                    await GenerateMockData().ConfigureAwait(false);
                }
            }

            // Legacy helper retained for compatibility with older code paths.
            public async Task<List<Employee>> GetAllEmployeesAsync(CancellationToken cancellationToken = default)
            {
                var profiles = await Employees
                    .Include(e => e.Skills)
                    .Include(e => e.Availability)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

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

                await Employees.AddRangeAsync(employees).ConfigureAwait(false);
                await SaveChangesAsync().ConfigureAwait(false);
            }

            public override void Dispose()
            {
                base.Dispose();
                CleanupDatabaseFile();
            }

            public override async ValueTask DisposeAsync()
            {
                await base.DisposeAsync().ConfigureAwait(false);
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
