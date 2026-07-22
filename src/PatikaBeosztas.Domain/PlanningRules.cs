namespace PatikaBeosztas.Domain;

public static class WeekdayMaskRules
{
    public static int ToMask(IEnumerable<DayOfWeek> days)
    {
        var mask = 0;
        foreach (var day in days.Distinct())
        {
            mask |= 1 << (int)day;
        }

        return mask;
    }

    public static IReadOnlyList<DayOfWeek> FromMask(int mask) =>
        Enum.GetValues<DayOfWeek>()
            .Where(day => (mask & (1 << (int)day)) != 0)
            .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
            .ToArray();
}

public static class LocationShiftTemplateRules
{
    public const int MaximumNameLength = 100;

    public static IReadOnlyList<DomainValidationIssue> Validate(
        string name,
        IReadOnlyCollection<DayOfWeek> weekdays,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        var issues = new List<DomainValidationIssue>();
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new("SHIFT_TEMPLATE_NAME_REQUIRED", "A műszaksablon neve kötelező."));
        }
        else if (name.Trim().Length > MaximumNameLength)
        {
            issues.Add(new(
                "SHIFT_TEMPLATE_NAME_TOO_LONG",
                $"A műszaksablon neve legfeljebb {MaximumNameLength} karakter lehet."));
        }

        if (weekdays.Count == 0)
        {
            issues.Add(new(
                "SHIFT_TEMPLATE_WEEKDAY_REQUIRED",
                "A műszaksablonhoz legalább egy napot ki kell választani."));
        }

        if (weekdays.Any(day => !Enum.IsDefined(day)))
        {
            issues.Add(new(
                "SHIFT_TEMPLATE_WEEKDAY_INVALID",
                "A műszaksablon csak érvényes napot tartalmazhat."));
        }

        if (weekdays.Distinct().Count() != weekdays.Count)
        {
            issues.Add(new(
                "DUPLICATE_SHIFT_TEMPLATE_WEEKDAY",
                "Egy nap csak egyszer szerepelhet a műszaksablonban."));
        }

        if (startTime >= endTime)
        {
            issues.Add(new(
                "SHIFT_TEMPLATE_TIME_ORDER",
                "A műszaksablon kezdési idejének meg kell előznie a befejezési időt."));
        }

        return issues;
    }
}

public static class StaffingCapabilityRules
{
    public static IReadOnlySet<StaffingCapability> Expand(
        IEnumerable<StaffingCapability> assignedCapabilities)
    {
        var effective = assignedCapabilities.ToHashSet();
        if (effective.Contains(StaffingCapability.SpecialistPharmacist))
        {
            effective.Add(StaffingCapability.Pharmacist);
        }

        if (effective.Contains(StaffingCapability.SpecialistAssistant))
        {
            effective.Add(StaffingCapability.Assistant);
        }

        return effective;
    }

    public static IReadOnlySet<StaffingCapability> ResolveEffective(
        IEnumerable<StaffingCapability> assignedCapabilities,
        ProfessionalRole professionalRole,
        bool countsAsPharmacist)
    {
        var assigned = assignedCapabilities.ToHashSet();
        if (professionalRole == ProfessionalRole.PharmacyManager || countsAsPharmacist)
        {
            assigned.Add(StaffingCapability.Pharmacist);
        }

        return Expand(assigned);
    }
}

public static class CoverageRequirementRules
{
    public static IReadOnlyList<DomainValidationIssue> Validate(
        TimeOnly startTime,
        TimeOnly endTime,
        int requiredCount)
    {
        var issues = new List<DomainValidationIssue>();
        if (startTime >= endTime)
        {
            issues.Add(new(
                "COVERAGE_TIME_ORDER",
                "A lefedettségi idősáv kezdésének meg kell előznie a befejezést."));
        }

        if (requiredCount < 1)
        {
            issues.Add(new(
                "COVERAGE_REQUIRED_COUNT_INVALID",
                "A szükséges létszám legalább 1."));
        }

        return issues;
    }

    public static int GetEffectiveRequiredCount(
        IEnumerable<CoverageRequirement> requirements,
        DayOfWeek dayOfWeek,
        StaffingCapability capability,
        TimeOnly atTime) =>
        requirements
            .Where(requirement =>
                requirement.IsActive &&
                requirement.DayOfWeek == dayOfWeek &&
                requirement.RequiredCapability == capability &&
                requirement.StartTime <= atTime &&
                atTime < requirement.EndTime)
            .Select(requirement => requirement.RequiredCount)
            .DefaultIfEmpty(0)
            .Max();
}

public static class PlanningEligibilityRules
{
    public static bool IncludeLocationInActivePlanning(bool isLocationActive) =>
        isLocationActive;
}
