namespace PatikaBeosztas.Domain;

public static class EmployeeWorkProfileRules
{
    public static IReadOnlyList<DomainValidationIssue> Validate(
        EmployeeWorkProfile profile,
        bool employeeIsActive,
        bool employeeIsSchedulable)
    {
        var issues = new List<DomainValidationIssue>();
        ValidateRequiredMinutes(
            profile.ContractedMonthlyMinutes,
            44_640,
            "CONTRACTED_MONTHLY_MINUTES_INVALID",
            "A szerződéses havi percszám 1 és 44 640 között lehet.",
            issues);
        if (profile.ContractedWeeklyMinutes is not null)
        {
            ValidateRequiredMinutes(
                profile.ContractedWeeklyMinutes.Value,
                10_080,
                "CONTRACTED_WEEKLY_MINUTES_INVALID",
                "A szerződéses heti percszám 1 és 10 080 között lehet.",
                issues);
        }

        ValidateRequiredMinutes(
            profile.MinimumShiftMinutes,
            1_440,
            "MINIMUM_SHIFT_MINUTES_INVALID",
            "A minimális műszakhossz 1 és 1 440 perc között lehet.",
            issues);
        ValidateRequiredMinutes(
            profile.StandardShiftMinutes,
            1_440,
            "STANDARD_SHIFT_MINUTES_INVALID",
            "A standard műszakhossz 1 és 1 440 perc között lehet.",
            issues);
        ValidateRequiredMinutes(
            profile.MaximumRegularShiftMinutes,
            1_440,
            "MAXIMUM_REGULAR_SHIFT_MINUTES_INVALID",
            "A normál maximális műszakhossz 1 és 1 440 perc között lehet.",
            issues);
        ValidateRequiredMinutes(
            profile.MaximumDailyMinutes,
            1_440,
            "MAXIMUM_DAILY_MINUTES_INVALID",
            "A napi maximum 1 és 1 440 perc között lehet.",
            issues);

        if (profile.MinimumShiftMinutes > profile.StandardShiftMinutes ||
            profile.StandardShiftMinutes > profile.MaximumRegularShiftMinutes)
        {
            issues.Add(new(
                "WORK_PROFILE_SHIFT_LIMIT_ORDER",
                "A műszaklimitek sorrendje: minimum ≤ standard ≤ normál maximum."));
        }

        if (profile.MaximumRegularShiftMinutes > profile.MaximumDailyMinutes)
        {
            issues.Add(new(
                "REGULAR_SHIFT_EXCEEDS_DAILY_MAXIMUM",
                "A normál műszak maximuma nem haladhatja meg a napi maximumot."));
        }

        ValidateConditionalLimit(
            profile.AllowsLongShift,
            profile.MaximumLongShiftMinutes,
            "LONG_SHIFT_LIMIT",
            issues);
        if (profile.AllowsLongShift &&
            profile.MaximumLongShiftMinutes < profile.MaximumRegularShiftMinutes)
        {
            issues.Add(new(
                "LONG_SHIFT_MAXIMUM_TOO_SMALL",
                "A hosszú műszak maximuma nem lehet kisebb a normál műszak maximumánál."));
        }

        if (profile.MaximumLongShiftMinutes > profile.MaximumDailyMinutes)
        {
            issues.Add(new(
                "LONG_SHIFT_EXCEEDS_DAILY_MAXIMUM",
                "A hosszú műszak maximuma nem haladhatja meg a napi maximumot."));
        }

        ValidateConditionalLimit(
            profile.AllowsOvertime,
            profile.MaximumOvertimeMinutesPerMonth,
            "OVERTIME_LIMIT",
            issues);
        ValidateConditionalLimit(
            profile.AllowsOnCallDuty,
            profile.MaximumOnCallAssignmentsPerMonth,
            "ON_CALL_LIMIT",
            issues);
        ValidateConditionalLimit(
            profile.AllowsStandby,
            profile.MaximumStandbyAssignmentsPerMonth,
            "STANDBY_LIMIT",
            issues);
        ValidateConditionalLimit(
            profile.AllowsSaturday,
            profile.MaximumSaturdaysPerMonth,
            "SATURDAY_LIMIT",
            issues);
        ValidateConditionalLimit(
            profile.AllowsSunday,
            profile.MaximumSundaysPerMonth,
            "SUNDAY_LIMIT",
            issues);

        if (profile.IncludeInAutoFill && (!employeeIsActive || !employeeIsSchedulable))
        {
            issues.Add(new(
                "AUTOFILL_REQUIRES_ACTIVE_SCHEDULABLE_EMPLOYEE",
                "Automatikus generálásba csak aktív és beosztható dolgozó vonható be."));
        }

        return issues;
    }

    private static void ValidateRequiredMinutes(
        int value,
        int maximum,
        string code,
        string message,
        List<DomainValidationIssue> issues)
    {
        if (value < 1 || value > maximum)
        {
            issues.Add(new(code, message));
        }
    }

    private static void ValidateConditionalLimit(
        bool isAllowed,
        int? limit,
        string codePrefix,
        List<DomainValidationIssue> issues)
    {
        if (isAllowed && limit is not > 0)
        {
            issues.Add(new(
                $"{codePrefix}_REQUIRED",
                "Engedélyezett vállaláshoz pozitív maximum szükséges."));
        }
        else if (!isAllowed && limit is not null and not 0)
        {
            issues.Add(new(
                $"{codePrefix}_MUST_BE_EMPTY",
                "Tiltott vállalásnál a maximum csak null vagy 0 lehet."));
        }
    }
}

public static class EmployeeShiftQuotaRuleRules
{
    public static IReadOnlyList<DomainValidationIssue> Validate(
        int minimum,
        int target,
        int maximum)
    {
        var issues = new List<DomainValidationIssue>();
        if (minimum < 0 || target < 0 || maximum < 0)
        {
            issues.Add(new(
                "SHIFT_QUOTA_NEGATIVE",
                "A kvóta minimuma, célja és maximuma nem lehet negatív."));
        }

        if (minimum > target || target > maximum)
        {
            issues.Add(new(
                "SHIFT_QUOTA_ORDER",
                "A kvóta sorrendje: minimum ≤ cél ≤ maximum."));
        }

        return issues;
    }
}
