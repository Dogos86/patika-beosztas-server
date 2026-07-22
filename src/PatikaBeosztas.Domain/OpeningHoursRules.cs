namespace PatikaBeosztas.Domain;

public sealed record OpeningIntervalDefinition(
    TimeOnly StartTime,
    TimeOnly? EndTime);

public sealed record OpeningDayDefinition(
    DayOfWeek DayOfWeek,
    OpeningDayMode Mode,
    IReadOnlyList<OpeningIntervalDefinition> Intervals);

public static class OpeningHoursRules
{
    public static IReadOnlyList<DomainValidationIssue> ValidateWeek(
        IReadOnlyCollection<OpeningDayDefinition> days)
    {
        var issues = new List<DomainValidationIssue>();
        var validDays = Enum.GetValues<DayOfWeek>().ToHashSet();
        if (days.Any(day => !validDays.Contains(day.DayOfWeek)))
        {
            issues.Add(new(
                "OPENING_DAY_INVALID",
                "A heti nyitvatartás csak érvényes napot tartalmazhat."));
        }

        var duplicateDays = days
            .GroupBy(day => day.DayOfWeek)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateDays.Length > 0)
        {
            issues.Add(new(
                "DUPLICATE_OPENING_DAY",
                "A heti nyitvatartásban minden nap csak egyszer szerepelhet."));
        }

        if (!validDays.SetEquals(days.Select(day => day.DayOfWeek)))
        {
            issues.Add(new(
                "OPENING_WEEK_REQUIRES_SEVEN_DAYS",
                "A heti nyitvatartásnak mind a hét napot tartalmaznia kell."));
        }

        foreach (var day in days)
        {
            ValidateDay(day, issues);
        }

        return issues;
    }

    public static bool Contains(
        OpeningDayDefinition day,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        if (startTime >= endTime)
        {
            return false;
        }

        return day.Mode switch
        {
            OpeningDayMode.Open24Hours => true,
            OpeningDayMode.Closed => false,
            OpeningDayMode.CustomIntervals => day.Intervals.Any(interval =>
                ToMinute(interval.StartTime) <= ToMinute(startTime) &&
                ToEndMinute(interval.EndTime) >= ToMinute(endTime)),
            _ => false
        };
    }

    private static void ValidateDay(
        OpeningDayDefinition day,
        List<DomainValidationIssue> issues)
    {
        if (!Enum.IsDefined(day.Mode))
        {
            issues.Add(new(
                "OPENING_MODE_INVALID",
                "A napi nyitvatartási mód érvénytelen."));
            return;
        }

        if (day.Mode is OpeningDayMode.Closed or OpeningDayMode.Open24Hours)
        {
            if (day.Intervals.Count > 0)
            {
                issues.Add(new(
                    "OPENING_MODE_FORBIDS_INTERVALS",
                    "Zárt vagy 24 órás naphoz nem adható egyedi intervallum."));
            }

            return;
        }

        if (day.Intervals.Count == 0)
        {
            issues.Add(new(
                "CUSTOM_OPENING_REQUIRES_INTERVAL",
                "Egyedi nyitvatartáshoz legalább egy intervallum szükséges."));
            return;
        }

        var previousStart = -1;
        var previousEnd = -1;
        foreach (var interval in day.Intervals)
        {
            var start = ToMinute(interval.StartTime);
            var end = ToEndMinute(interval.EndTime);
            if (start >= end)
            {
                issues.Add(new(
                    "OPENING_INTERVAL_ORDER",
                    "A nyitási időnek meg kell előznie a zárási időt; a null zárás 24:00-t jelent."));
            }

            if (previousStart > start)
            {
                issues.Add(new(
                    "OPENING_INTERVALS_NOT_SORTED",
                    "A napi nyitvatartási intervallumokat kezdési idő szerint kell megadni."));
            }

            if (previousEnd > start)
            {
                issues.Add(new(
                    "OPENING_INTERVAL_OVERLAP",
                    "A napi nyitvatartási intervallumok nem fedhetik át egymást."));
            }

            previousStart = start;
            previousEnd = Math.Max(previousEnd, end);
        }
    }

    private static int ToMinute(TimeOnly value) => value.Hour * 60 + value.Minute;

    private static int ToEndMinute(TimeOnly? value) =>
        value is null ? 24 * 60 : ToMinute(value.Value);
}
