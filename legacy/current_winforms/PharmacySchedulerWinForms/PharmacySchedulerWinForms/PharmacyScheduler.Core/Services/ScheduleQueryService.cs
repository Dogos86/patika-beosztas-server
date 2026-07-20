using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.Core.Services;

public sealed class ScheduleQueryService
{
    public IReadOnlyList<ScheduleExportRow> FlattenSchedule(AppData data, SchedulePlan schedule)
    {
        var locations = data.Locations.ToDictionary(x => x.Id);
        var employees = data.Employees.ToDictionary(x => x.Id);

        return schedule.Entries
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Start)
            .Select(entry =>
            {
                employees.TryGetValue(entry.EmployeeId, out var employee);
                locations.TryGetValue(entry.LocationId, out var location);

                return new ScheduleExportRow
                {
                    LocationName = location?.Name ?? "Ismeretlen telephely",
                    Date = entry.Date,
                    Start = entry.Start,
                    End = entry.End,
                    EmployeeFullName = employee?.FullName ?? "Ismeretlen dolgozó",
                    EmployeeDisplayName = employee?.DisplayName ?? string.Empty,
                    RoleName = employee?.Role.ToDisplayText() ?? string.Empty,
                    TimeTypeCode = entry.TimeType.ToCode(),
                    TimeTypeName = entry.TimeType.ToDisplayText(),
                    Hours = entry.Hours,
                    Note = entry.Note
                };
            })
            .ToList();
    }

    public IReadOnlyList<ScheduleSummaryRow> BuildSummary(AppData data, SchedulePlan schedule)
    {
        var locations = data.Locations.ToDictionary(x => x.Id);
        var employees = data.Employees.ToDictionary(x => x.Id);

        return schedule.Entries
            .GroupBy(entry => new
            {
                entry.EmployeeId,
                entry.TimeType
            })
            .Select(group =>
            {
                var entryLocations = group
                    .Select(x => locations.TryGetValue(x.LocationId, out var location) ? location.Name : "Ismeretlen telephely")
                    .Distinct()
                    .OrderBy(x => x);

                employees.TryGetValue(group.Key.EmployeeId, out var employee);

                return new ScheduleSummaryRow
                {
                    EmployeeFullName = employee?.FullName ?? "Ismeretlen dolgozó",
                    EmployeeDisplayName = employee?.DisplayName ?? string.Empty,
                    RoleName = employee?.Role.ToDisplayText() ?? string.Empty,
                    LocationNames = string.Join(", ", entryLocations),
                    TimeTypeName = group.Key.TimeType.ToDisplayText(),
                    Hours = group.Sum(x => x.Hours)
                };
            })
            .OrderBy(x => x.EmployeeFullName)
            .ThenBy(x => x.TimeTypeName)
            .ToList();
    }
}
