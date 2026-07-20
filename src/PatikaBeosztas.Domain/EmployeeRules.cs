namespace PatikaBeosztas.Domain;

public static class EmployeeRules
{
    public static DateOnly MinimumBirthDate { get; } = new(1900, 1, 1);

    public static IReadOnlyList<DomainValidationIssue> ValidateConfiguration(
        bool isActive,
        bool isSchedulable,
        bool includeInAutoFill,
        int? monthlyMinutesLimit,
        int? maxDailyMinutes,
        DateOnly? birthDate,
        DateOnly currentDate)
    {
        var issues = new List<DomainValidationIssue>();
        if (monthlyMinutesLimit is < 1 or > 44_640)
        {
            issues.Add(new(
                "MONTHLY_MINUTES_OUT_OF_RANGE",
                "A havi perclimit 1 és 44 640 perc között lehet."));
        }

        if (maxDailyMinutes is < 1 or > 1_440)
        {
            issues.Add(new(
                "MAX_DAILY_MINUTES_OUT_OF_RANGE",
                "A napi perclimit 1 és 1 440 perc között lehet."));
        }

        if (birthDate < MinimumBirthDate)
        {
            issues.Add(new(
                "BIRTH_DATE_TOO_EARLY",
                $"A születési dátum nem lehet korábbi, mint {MinimumBirthDate:yyyy-MM-dd}."));
        }

        if (birthDate > currentDate)
        {
            issues.Add(new(
                "BIRTH_DATE_IN_FUTURE",
                "A születési dátum nem lehet jövőbeli."));
        }

        if (includeInAutoFill && (!isActive || !isSchedulable))
        {
            issues.Add(new(
                "AUTOFILL_REQUIRES_ACTIVE_SCHEDULABLE_EMPLOYEE",
                "Automatikus kitöltésbe csak aktív és beosztható dolgozó vonható be."));
        }

        return issues;
    }
}
