namespace PatikaBeosztas.Domain;

public static class EmployeeTimeWindowRules
{
    public static IReadOnlyList<DomainValidationIssue> Validate(
        IReadOnlyCollection<EmployeeTimeWindow> windows)
    {
        var issues = new List<DomainValidationIssue>();
        var items = windows.ToArray();

        for (var index = 0; index < items.Length; index++)
        {
            var current = items[index];
            if (current.StartTime >= current.EndTime)
            {
                issues.Add(new(
                    "TIME_WINDOW_ORDER",
                    $"A(z) {index + 1}. időablaknál a kezdésnek meg kell előznie a befejezést."));
            }

            for (var otherIndex = index + 1; otherIndex < items.Length; otherIndex++)
            {
                var other = items[otherIndex];
                if (!DaysIntersect(current.DayOfWeek, other.DayOfWeek) ||
                    !TimesOverlap(current, other))
                {
                    continue;
                }

                var code = IsDuplicate(current, other)
                    ? "DUPLICATE_TIME_WINDOW"
                    : "OVERLAPPING_TIME_WINDOWS";
                issues.Add(new(
                    code,
                    $"A(z) {index + 1}. és {otherIndex + 1}. időablak átfed vagy ismétlődik."));
            }
        }

        return issues;
    }

    private static bool DaysIntersect(DayOfWeek? first, DayOfWeek? second) =>
        first is null || second is null || first == second;

    private static bool TimesOverlap(EmployeeTimeWindow first, EmployeeTimeWindow second) =>
        first.StartTime < second.EndTime && second.StartTime < first.EndTime;

    private static bool IsDuplicate(EmployeeTimeWindow first, EmployeeTimeWindow second) =>
        first.DayOfWeek == second.DayOfWeek &&
        first.StartTime == second.StartTime &&
        first.EndTime == second.EndTime &&
        first.Type == second.Type;
}

public sealed record DomainValidationIssue(string Code, string Message);
