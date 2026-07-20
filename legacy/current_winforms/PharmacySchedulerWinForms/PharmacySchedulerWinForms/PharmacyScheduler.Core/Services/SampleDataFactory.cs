using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.Core.Services;

public static class SampleDataFactory
{
    public static AppData Create()
    {
        var central = new Location { Name = "Központi Patika", Address = "1111 Budapest, Fő utca 1." };
        var branch = new Location { Name = "Fiókpatika", Address = "1117 Budapest, Minta tér 2." };

        var manager = new Employee
        {
            FullName = "Dr. Kovács Anna",
            DisplayName = "Kovács Anna",
            BirthDate = new DateTime(1988, 4, 12),
            Role = EmployeeRole.PharmacyManager,
            MonthlyHoursLimit = 168,
            MaxDailyHours = 8,
            PreferredWindows = "08:00-16:00",
            AllowedLocationIds = new List<Guid> { central.Id },
            AllowedTimeTypes = new List<TimeType> { TimeType.Work, TimeType.Overtime, TimeType.Vacation, TimeType.SickLeave, TimeType.UnpaidLeave, TimeType.MaternityLeave }
        };

        var pharmacist = new Employee
        {
            FullName = "Nagy Péter",
            DisplayName = "Nagy Péter",
            BirthDate = new DateTime(1991, 9, 8),
            Role = EmployeeRole.Pharmacist,
            MonthlyHoursLimit = 168,
            MaxDailyHours = 10,
            PreferredWindows = "08:00-18:00",
            AllowedLocationIds = new List<Guid> { central.Id, branch.Id }
        };

        var assistant = new Employee
        {
            FullName = "Szabó Éva",
            DisplayName = "Szabó Éva",
            BirthDate = new DateTime(1994, 11, 23),
            Role = EmployeeRole.ExpediatingAssistant,
            MonthlyHoursLimit = 160,
            MaxDailyHours = 8,
            PreferredWindows = "08:00-16:00",
            AllowedLocationIds = new List<Guid> { central.Id, branch.Id }
        };

        var cleaner = new Employee
        {
            FullName = "Tóth Mária",
            DisplayName = "Tóth Mária",
            BirthDate = new DateTime(1975, 2, 14),
            Role = EmployeeRole.Cleaner,
            MonthlyHoursLimit = 80,
            MaxDailyHours = 4,
            PreferredWindows = "06:00-10:00",
            AllowedLocationIds = new List<Guid> { central.Id }
        };

        var start = DateOnly.FromDateTime(DateTime.Today).AddDays(-(int)DateTime.Today.DayOfWeek + 1);
        var schedule = new SchedulePlan
        {
            Name = "Minta heti beosztás",
            PeriodStart = start,
            PeriodEnd = start.AddDays(6)
        };

        schedule.Entries.Add(new ShiftEntry
        {
            ScheduleId = schedule.Id,
            EmployeeId = manager.Id,
            LocationId = central.Id,
            Date = start,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0),
            TimeType = TimeType.Work
        });

        schedule.Entries.Add(new ShiftEntry
        {
            ScheduleId = schedule.Id,
            EmployeeId = pharmacist.Id,
            LocationId = branch.Id,
            Date = start,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(14, 0),
            TimeType = TimeType.Work
        });

        schedule.Entries.Add(new ShiftEntry
        {
            ScheduleId = schedule.Id,
            EmployeeId = assistant.Id,
            LocationId = central.Id,
            Date = start,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0),
            TimeType = TimeType.Work
        });

        return new AppData
        {
            Locations = new List<Location> { central, branch },
            Employees = new List<Employee> { manager, pharmacist, assistant, cleaner },
            CoverageRules = CreateCoverageRules(central.Id, branch.Id),
            Leaves = new List<LeaveEntry>(),
            Schedules = new List<SchedulePlan> { schedule },
            Settings = new AppSettings()
        };
    }

    private static List<CoverageRule> CreateCoverageRules(Guid centralId, Guid branchId)
    {
        var rules = new List<CoverageRule>();

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            rules.Add(new CoverageRule
            {
                LocationId = centralId,
                DayOfWeek = day,
                Start = new TimeOnly(8, 0),
                End = new TimeOnly(16, 0),
                Role = EmployeeRole.Pharmacist,
                RequiredCount = 1,
                Severity = Severity.Hard
            });

            rules.Add(new CoverageRule
            {
                LocationId = centralId,
                DayOfWeek = day,
                Start = new TimeOnly(8, 0),
                End = new TimeOnly(16, 0),
                Role = EmployeeRole.ExpediatingAssistant,
                RequiredCount = 1,
                Severity = Severity.Soft
            });

            rules.Add(new CoverageRule
            {
                LocationId = branchId,
                DayOfWeek = day,
                Start = new TimeOnly(8, 0),
                End = new TimeOnly(14, 0),
                Role = EmployeeRole.Pharmacist,
                RequiredCount = 1,
                Severity = Severity.Hard
            });
        }

        return rules;
    }
}
