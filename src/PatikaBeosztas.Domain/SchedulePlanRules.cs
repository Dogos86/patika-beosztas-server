namespace PatikaBeosztas.Domain;

public static class SchedulePlanRules
{
    public static IReadOnlyList<DomainValidationIssue> ValidatePeriod(
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        var issues = new List<DomainValidationIssue>();
        if (periodEnd < periodStart)
        {
            issues.Add(new(
                "SCHEDULE_PERIOD_ORDER",
                "A beosztási időszak kezdete nem lehet későbbi a végénél."));
        }
        else if (periodEnd.DayNumber - periodStart.DayNumber + 1 > 31)
        {
            issues.Add(new(
                "SCHEDULE_PERIOD_TOO_LONG",
                "A beosztási időszak legfeljebb 31 nap lehet."));
        }

        return issues;
    }

    public static IReadOnlyList<DomainValidationIssue> ValidateTransition(
        ScheduleStatus from,
        ScheduleStatus to,
        bool hasBlockingIssues)
    {
        var issues = new List<DomainValidationIssue>();
        var allowed = (from, to) switch
        {
            (ScheduleStatus.Draft, ScheduleStatus.UnderReview) => true,
            (ScheduleStatus.UnderReview, ScheduleStatus.Draft) => true,
            (ScheduleStatus.UnderReview, ScheduleStatus.Approved) => true,
            (ScheduleStatus.Approved, ScheduleStatus.Published) => true,
            (ScheduleStatus.Published, ScheduleStatus.Archived) => true,
            _ => false
        };
        if (!allowed)
        {
            issues.Add(new(
                "SCHEDULE_STATUS_TRANSITION_NOT_ALLOWED",
                "A kért beosztás-állapotátmenet nem engedélyezett."));
        }

        if (hasBlockingIssues &&
            to is ScheduleStatus.Approved or ScheduleStatus.Published)
        {
            issues.Add(new(
                "BLOCKING_SCHEDULE_ISSUES",
                "Blokkoló probléma mellett a beosztás nem hagyható jóvá és nem tehető közzé."));
        }

        return issues;
    }

    public static IReadOnlyList<DomainValidationIssue> ValidateAssignment(
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        var issues = new List<DomainValidationIssue>();
        if (date < periodStart || date > periodEnd)
        {
            issues.Add(new(
                "SHIFT_OUTSIDE_SCHEDULE_PERIOD",
                "A műszak kívül esik a beosztás időszakán."));
        }

        if (startTime >= endTime)
        {
            issues.Add(new(
                "SHIFT_TIME_ORDER",
                "A műszak kezdésének meg kell előznie a befejezést."));
        }

        if (startTime.Minute % 30 != 0 ||
            endTime.Minute % 30 != 0 ||
            startTime.Second != 0 ||
            endTime.Second != 0)
        {
            issues.Add(new(
                "SHIFT_NOT_ON_HALF_HOUR_GRID",
                "A műszaknak 30 perces rácsra kell illeszkednie."));
        }

        return issues;
    }

    public static IReadOnlyList<DomainValidationIssue> ValidateDailyAssignments(
        IEnumerable<(Guid LocationId, TimeOnly StartTime, TimeOnly EndTime)> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        var ordered = assignments
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.EndTime)
            .ToArray();
        if (ordered.Length < 2)
        {
            return [];
        }

        var issues = new List<DomainValidationIssue>();
        if (ordered.Select(item => item.LocationId).Distinct().Skip(1).Any())
        {
            issues.Add(new(
                "MULTI_LOCATION_SAME_DAY_NOT_ALLOWED",
                "Egy dolgozó egy napon csak egy telephelyen osztható be."));
        }

        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            if (current.StartTime < previous.EndTime)
            {
                issues.Add(new(
                    "SHIFT_TIME_CONFLICT",
                    "Egy dolgozó műszakjai nem fedhetik egymást."));
            }
            else if (previous.LocationId == current.LocationId &&
                     previous.EndTime < current.StartTime)
            {
                issues.Add(new(
                    "SPLIT_SHIFT_NOT_ALLOWED",
                    "A megszakított napi műszak nem engedélyezett."));
            }
        }

        return issues
            .DistinctBy(issue => issue.Code)
            .ToArray();
    }
}
