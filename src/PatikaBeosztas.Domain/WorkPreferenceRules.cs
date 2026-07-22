namespace PatikaBeosztas.Domain;

public static class WorkPreferenceRules
{
    public const int MaximumNoteLength = 1000;

    public static IReadOnlyList<DomainValidationIssue> Validate(
        DateOnly dateFrom,
        DateOnly dateTo,
        bool isFullDay,
        TimeOnly? startTime,
        TimeOnly? endTime,
        string? note)
    {
        var issues = new List<DomainValidationIssue>();
        if (dateFrom > dateTo)
        {
            issues.Add(new(
                "WORK_PREFERENCE_DATE_ORDER",
                "A kezdő dátum nem lehet későbbi a záró dátumnál."));
        }

        AddTimeRangeIssues(isFullDay, startTime, endTime, issues);

        if (note?.Trim().Length > MaximumNoteLength)
        {
            issues.Add(new(
                "WORK_PREFERENCE_NOTE_TOO_LONG",
                $"A megjegyzés legfeljebb {MaximumNoteLength} karakter lehet."));
        }

        return issues;
    }

    private static void AddTimeRangeIssues(
        bool isFullDay,
        TimeOnly? startTime,
        TimeOnly? endTime,
        List<DomainValidationIssue> issues)
    {
        if (isFullDay)
        {
            if (startTime is not null || endTime is not null)
            {
                issues.Add(new(
                    "FULL_DAY_WORK_PREFERENCE_HAS_TIME",
                    "Egész napos beállításhoz nem adható kezdési vagy befejezési idő."));
            }

            return;
        }

        if (startTime is null || endTime is null)
        {
            issues.Add(new(
                "PARTIAL_WORK_PREFERENCE_REQUIRES_TIME",
                "Résznapos beállításhoz a kezdési és befejezési idő kötelező."));
        }
        else if (startTime >= endTime)
        {
            issues.Add(new(
                "WORK_PREFERENCE_TIME_ORDER",
                "A kezdési időnek meg kell előznie a befejezési időt."));
        }
    }
}
