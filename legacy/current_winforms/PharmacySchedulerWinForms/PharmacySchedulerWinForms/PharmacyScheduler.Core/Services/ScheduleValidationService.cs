using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.Core.Services;

public sealed class ScheduleValidationService
{
    public ValidationReport Validate(AppData data, SchedulePlan schedule)
    {
        var report = new ValidationReport();
        var employees = data.Employees.ToDictionary(x => x.Id);
        var locations = data.Locations.ToDictionary(x => x.Id);
        var otherEntries = data.Schedules
            .Where(x => x.Id != schedule.Id)
            .SelectMany(x => x.Entries)
            .ToList();

        foreach (var entry in schedule.Entries)
        {
            if (entry.Date < schedule.PeriodStart || entry.Date > schedule.PeriodEnd)
            {
                report.Add(new ValidationIssue
                {
                    Severity = Severity.Hard,
                    Code = "ENTRY_OUT_OF_PERIOD",
                    ShiftEntryId = entry.Id,
                    Message = $"A {entry.Date:yyyy-MM-dd} dátumú bejegyzés kívül esik a beosztás időszakán."
                });
            }

            if (entry.Start >= entry.End)
            {
                report.Add(new ValidationIssue
                {
                    Severity = Severity.Hard,
                    Code = "INVALID_TIME_RANGE",
                    ShiftEntryId = entry.Id,
                    Message = $"A bejegyzés kezdete nem lehet később vagy egyenlő a végével ({entry.Date:yyyy-MM-dd})."
                });
            }

            if (!HalfHourHelper.IsHalfHourAligned(entry.Start) || !HalfHourHelper.IsHalfHourAligned(entry.End))
            {
                report.Add(new ValidationIssue
                {
                    Severity = Severity.Hard,
                    Code = "NOT_HALF_HOUR_ALIGNED",
                    ShiftEntryId = entry.Id,
                    Message = $"A bejegyzésnek 30 perces rácsra kell illeszkednie ({entry.Date:yyyy-MM-dd}, {entry.Start:HH\\:mm}-{entry.End:HH\\:mm})."
                });
            }

            if (entry.Start < TimeOnly.MinValue || entry.End > new TimeOnly(23, 59))
            {
                report.Add(new ValidationIssue
                {
                    Severity = Severity.Hard,
                    Code = "OUT_OF_DAY",
                    ShiftEntryId = entry.Id,
                    Message = $"A bejegyzés időtartama kilóg a napból ({entry.Date:yyyy-MM-dd})."
                });
            }

            if (!employees.TryGetValue(entry.EmployeeId, out var employee))
            {
                report.Add(new ValidationIssue
                {
                    Severity = Severity.Hard,
                    Code = "UNKNOWN_EMPLOYEE",
                    ShiftEntryId = entry.Id,
                    Message = $"Ismeretlen dolgozó van hozzárendelve egy bejegyzéshez ({entry.Date:yyyy-MM-dd})."
                });
                continue;
            }

            if (!locations.TryGetValue(entry.LocationId, out var location))
            {
                report.Add(new ValidationIssue
                {
                    Severity = Severity.Hard,
                    Code = "UNKNOWN_LOCATION",
                    ShiftEntryId = entry.Id,
                    EmployeeId = employee.Id,
                    Message = $"Ismeretlen telephely van hozzárendelve {employee.DisplayName} bejegyzéséhez."
                });
                continue;
            }

            if (employee.AllowedTimeTypes.Count > 0 && !employee.AllowedTimeTypes.Contains(entry.TimeType))
            {
                report.Add(new ValidationIssue
                {
                    Severity = data.Settings.AllowedTimeTypeSeverity,
                    Code = "TIME_TYPE_NOT_ALLOWED",
                    EmployeeId = employee.Id,
                    ShiftEntryId = entry.Id,
                    Message = $"{employee.DisplayName} nem osztható be erre az időtípusra: {entry.TimeType.ToDisplayText()}."
                });
            }

            if (employee.AllowedLocationIds.Count > 0 && !employee.AllowedLocationIds.Contains(entry.LocationId))
            {
                report.Add(new ValidationIssue
                {
                    Severity = data.Settings.AllowedLocationSeverity,
                    Code = "LOCATION_NOT_ALLOWED",
                    EmployeeId = employee.Id,
                    LocationId = entry.LocationId,
                    ShiftEntryId = entry.Id,
                    Message = $"{employee.DisplayName} nincs hozzárendelve ehhez a telephelyhez: {location.Name}."
                });
            }

            if (entry.TimeType.IsWorkLike() && IsEmployeeOnLeave(data, entry.EmployeeId, entry.Date))
            {
                report.Add(new ValidationIssue
                {
                    Severity = data.Settings.LeaveConflictSeverity,
                    Code = "LEAVE_CONFLICT",
                    EmployeeId = employee.Id,
                    ShiftEntryId = entry.Id,
                    Message = $"{employee.DisplayName} távolléten van {entry.Date:yyyy-MM-dd} napon, mégis munkára van beosztva."
                });
            }

            var preferredWindows = TimeWindowParser.ParseMany(employee.PreferredWindows);
            if (entry.TimeType.IsWorkLike() && preferredWindows.Count > 0 && !preferredWindows.Any(window => window.Contains(entry.Start, entry.End)))
            {
                report.Add(new ValidationIssue
                {
                    Severity = data.Settings.PreferredWindowSeverity,
                    Code = "OUTSIDE_PREFERRED_WINDOW",
                    EmployeeId = employee.Id,
                    ShiftEntryId = entry.Id,
                    Message = $"{employee.DisplayName} a preferált idősávján kívül dolgozik ({entry.Start:HH\\:mm}-{entry.End:HH\\:mm})."
                });
            }

            var forbiddenWindows = TimeWindowParser.ParseMany(employee.ForbiddenWindows);
            if (entry.TimeType.IsWorkLike() && forbiddenWindows.Any(window => window.Overlaps(entry.Start, entry.End)))
            {
                report.Add(new ValidationIssue
                {
                    Severity = data.Settings.ForbiddenWindowSeverity,
                    Code = "FORBIDDEN_WINDOW",
                    EmployeeId = employee.Id,
                    ShiftEntryId = entry.Id,
                    Message = $"{employee.DisplayName} tiltott idősávra lett beosztva ({entry.Start:HH\\:mm}-{entry.End:HH\\:mm})."
                });
            }
        }

        ValidateOverlaps(report, employees, schedule.Entries, otherEntries);
        ValidateDailyHours(data, report, employees, schedule.Entries, otherEntries);
        ValidateMonthlyHours(data, report, employees, schedule.Entries, otherEntries);
        ValidateCoverage(data, report, employees, schedule, otherEntries);

        return report;
    }

    private static bool IsEmployeeOnLeave(AppData data, Guid employeeId, DateOnly date) =>
        data.Leaves.Any(x => x.EmployeeId == employeeId && x.StartDate <= date && date <= x.EndDate);

    private static void ValidateOverlaps(
        ValidationReport report,
        IReadOnlyDictionary<Guid, Employee> employees,
        IReadOnlyList<ShiftEntry> currentEntries,
        IReadOnlyList<ShiftEntry> otherEntries)
    {
        foreach (var current in currentEntries)
        {
            if (!employees.TryGetValue(current.EmployeeId, out var employee))
            {
                continue;
            }

            var conflicting = otherEntries
                .Concat(currentEntries.Where(x => x.Id != current.Id))
                .Where(x =>
                    x.EmployeeId == current.EmployeeId &&
                    x.Date == current.Date &&
                    HalfHourHelper.Overlaps(current.Start, current.End, x.Start, x.End))
                .FirstOrDefault();

            if (conflicting is not null)
            {
                report.Add(new ValidationIssue
                {
                    Severity = Severity.Hard,
                    Code = "EMPLOYEE_OVERLAP",
                    EmployeeId = employee.Id,
                    ShiftEntryId = current.Id,
                    Message = $"{employee.DisplayName} több telephelyen / több műszakban szerepel egyszerre ({current.Date:yyyy-MM-dd}, {current.Start:HH\\:mm}-{current.End:HH\\:mm})."
                });
            }
        }
    }

    private static void ValidateDailyHours(
        AppData data,
        ValidationReport report,
        IReadOnlyDictionary<Guid, Employee> employees,
        IReadOnlyList<ShiftEntry> currentEntries,
        IReadOnlyList<ShiftEntry> otherEntries)
    {
        var allEntries = otherEntries
            .Concat(currentEntries)
            .Where(x => x.TimeType.IsWorkLike())
            .ToList();

        foreach (var group in allEntries.GroupBy(x => new { x.EmployeeId, x.Date }))
        {
            if (!employees.TryGetValue(group.Key.EmployeeId, out var employee))
            {
                continue;
            }

            var totalHours = group.Sum(x => x.Hours);
            if (totalHours > employee.MaxDailyHours)
            {
                report.Add(new ValidationIssue
                {
                    Severity = data.Settings.DailyHoursSeverity,
                    Code = "DAILY_LIMIT_EXCEEDED",
                    EmployeeId = employee.Id,
                    Message = $"{employee.DisplayName} túllépi a napi limitet ({totalHours:0.##} / {employee.MaxDailyHours:0.##} óra) ekkor: {group.Key.Date:yyyy-MM-dd}."
                });
            }
        }
    }

    private static void ValidateMonthlyHours(
        AppData data,
        ValidationReport report,
        IReadOnlyDictionary<Guid, Employee> employees,
        IReadOnlyList<ShiftEntry> currentEntries,
        IReadOnlyList<ShiftEntry> otherEntries)
    {
        var allEntries = otherEntries
            .Concat(currentEntries)
            .Where(x => x.TimeType.IsWorkLike())
            .ToList();

        foreach (var group in allEntries.GroupBy(x => new { x.EmployeeId, x.Date.Year, x.Date.Month }))
        {
            if (!employees.TryGetValue(group.Key.EmployeeId, out var employee))
            {
                continue;
            }

            var totalHours = group.Sum(x => x.Hours);
            if (totalHours > employee.MonthlyHoursLimit)
            {
                report.Add(new ValidationIssue
                {
                    Severity = data.Settings.MonthlyHoursSeverity,
                    Code = "MONTHLY_LIMIT_EXCEEDED",
                    EmployeeId = employee.Id,
                    Message = $"{employee.DisplayName} túllépi a havi keretet ({totalHours:0.##} / {employee.MonthlyHoursLimit:0.##} óra) {group.Key.Year}-{group.Key.Month:00} hónapban."
                });
            }
        }
    }

    private static void ValidateCoverage(
        AppData data,
        ValidationReport report,
        IReadOnlyDictionary<Guid, Employee> employees,
        SchedulePlan schedule,
        IReadOnlyList<ShiftEntry> otherEntries)
    {
        var combinedEntries = otherEntries.Concat(schedule.Entries).Where(x => x.TimeType.IsWorkLike()).ToList();
        var activeLocationIds = new HashSet<Guid>(data.Locations.Where(x => x.IsActive).Select(x => x.Id));

        for (var date = schedule.PeriodStart; date <= schedule.PeriodEnd; date = date.AddDays(1))
        {
            var rulesForDay = data.CoverageRules
                .Where(rule => rule.DayOfWeek == date.DayOfWeek)
                .Where(rule => activeLocationIds.Contains(rule.LocationId))
                .OrderBy(rule => rule.LocationId)
                .ThenBy(rule => rule.Start)
                .ToList();

            foreach (var rule in rulesForDay)
            {
                var shortageSlots = new List<string>();

                foreach (var slot in HalfHourHelper.EnumerateSlots(rule.Start, rule.End))
                {
                    var slotEnd = slot.AddMinutes(30);
                    var count = combinedEntries.Count(entry =>
                    {
                        if (entry.Date != date || entry.LocationId != rule.LocationId)
                        {
                            return false;
                        }

                        if (!employees.TryGetValue(entry.EmployeeId, out var employee))
                        {
                            return false;
                        }

                        return employee.Role == rule.Role &&
                               HalfHourHelper.Overlaps(entry.Start, entry.End, slot, slotEnd);
                    });

                    if (count < rule.RequiredCount)
                    {
                        shortageSlots.Add(slot.ToString("HH:mm"));
                    }
                }

                if (shortageSlots.Count > 0)
                {
                    report.Add(new ValidationIssue
                    {
                        Severity = rule.Severity,
                        Code = "COVERAGE_SHORTAGE",
                        LocationId = rule.LocationId,
                        Message = $"Lefedettségi hiány: {GetLocationName(data, rule.LocationId)} – {date:yyyy-MM-dd} – {rule.Role.ToDisplayText()}, hiányos idősávok: {string.Join(", ", shortageSlots)} (minimum {rule.RequiredCount} fő)."
                    });
                }
            }
        }
    }

    private static string GetLocationName(AppData data, Guid locationId) =>
        data.Locations.FirstOrDefault(x => x.Id == locationId)?.Name ?? "Ismeretlen telephely";
}
