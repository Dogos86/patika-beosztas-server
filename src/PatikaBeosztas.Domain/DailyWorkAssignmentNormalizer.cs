namespace PatikaBeosztas.Domain;

public sealed record WorkInterval(
    Guid EmployeeId,
    DateOnly Date,
    Guid LocationId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeType TimeType);

public sealed record AssignmentSegment(
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeType TimeType);

public sealed record DailyWorkAssignment(
    Guid EmployeeId,
    DateOnly Date,
    Guid LocationId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int TotalMinutes,
    string TimeZoneId,
    IReadOnlyList<AssignmentSegment> Segments);

public sealed record DailyWorkAssignmentNormalizationResult(
    DailyWorkAssignment? Assignment,
    IReadOnlyList<DomainValidationIssue> Issues)
{
    public bool IsValid => Assignment is not null && Issues.Count == 0;
}

public static class DailyWorkAssignmentNormalizer
{
    public static DailyWorkAssignmentNormalizationResult Normalize(
        IEnumerable<WorkInterval> sourceIntervals,
        int maximumDailyMinutes,
        string timeZoneId = "Europe/Budapest")
    {
        var intervals = sourceIntervals
            .OrderBy(interval => interval.StartTime)
            .ThenBy(interval => interval.EndTime)
            .ToArray();
        var issues = new List<DomainValidationIssue>();
        if (intervals.Length == 0)
        {
            issues.Add(new("WORK_INTERVAL_REQUIRED", "Legalább egy munkaintervallum szükséges."));
            return new(null, issues);
        }

        if (maximumDailyMinutes is < 1 or > 1_440)
        {
            issues.Add(new(
                "MAXIMUM_DAILY_MINUTES_INVALID",
                "A napi maximum 1 és 1 440 perc között lehet."));
        }

        var first = intervals[0];
        if (intervals.Any(interval =>
                interval.EmployeeId != first.EmployeeId || interval.Date != first.Date))
        {
            issues.Add(new(
                "MIXED_EMPLOYEE_OR_DATE",
                "Egy normalizálás csak egy dolgozó egyetlen napját tartalmazhatja."));
        }

        if (intervals.Any(interval => interval.LocationId != first.LocationId))
        {
            issues.Add(new(
                "MULTI_LOCATION_SAME_DAY_NOT_ALLOWED",
                "Egy dolgozó egy folyamatos napi munkablokkja nem tartalmazhat több telephelyet."));
        }

        foreach (var interval in intervals)
        {
            if (interval.TimeType is not TimeType.Work and not TimeType.Overtime)
            {
                issues.Add(new(
                    "UNSUPPORTED_WORK_INTERVAL_TYPE",
                    "A munkablokk-normalizálás csak Work és Overtime szegmenst fogad."));
            }

            if (interval.StartTime >= interval.EndTime)
            {
                issues.Add(new(
                    "WORK_INTERVAL_TIME_ORDER",
                    "A munkaintervallum kezdésének meg kell előznie a befejezést."));
            }
        }

        TimeZoneInfo? timeZone = null;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            issues.Add(new("TIME_ZONE_NOT_FOUND", "A szervezet időzónája nem található."));
        }
        catch (InvalidTimeZoneException)
        {
            issues.Add(new("TIME_ZONE_INVALID", "A szervezet időzónája érvénytelen."));
        }

        if (timeZone is not null)
        {
            foreach (var interval in intervals)
            {
                ValidateLocalBoundary(interval.Date, interval.StartTime, timeZone, issues);
                ValidateLocalBoundary(interval.Date, interval.EndTime, timeZone, issues);
            }
        }

        var coveredUntil = first.EndTime;
        for (var index = 1; index < intervals.Length; index++)
        {
            var interval = intervals[index];
            if (interval.StartTime > coveredUntil)
            {
                issues.Add(new(
                    "SPLIT_SHIFT_NOT_ALLOWED",
                    "A napi munkablokk nem tartalmazhat hézagot."));
            }

            if (intervals[..index].Any(previous =>
                    previous.TimeType != interval.TimeType &&
                    interval.StartTime < previous.EndTime &&
                    previous.StartTime < interval.EndTime))
            {
                issues.Add(new(
                    "OVERLAPPING_TIME_TYPES_NOT_ALLOWED",
                    "Eltérő munkaidőtípusok nem fedhetik át egymást; a bérszámfejtési besorolás nem található ki."));
            }

            if (interval.EndTime > coveredUntil)
            {
                coveredUntil = interval.EndTime;
            }
        }

        if (issues.Count > 0 || timeZone is null)
        {
            return new(null, issues);
        }

        var assignmentStart = intervals.Min(interval => interval.StartTime);
        var assignmentEnd = intervals.Max(interval => interval.EndTime);
        var totalMinutes = GetElapsedMinutes(
            first.Date,
            assignmentStart,
            assignmentEnd,
            timeZone);
        if (totalMinutes > maximumDailyMinutes)
        {
            issues.Add(new(
                "MAXIMUM_DAILY_MINUTES_EXCEEDED",
                "A normalizált munkablokk meghaladja a dolgozó napi maximumát."));
            return new(null, issues);
        }

        var segments = MergeSegments(intervals);
        return new(
            new DailyWorkAssignment(
                first.EmployeeId,
                first.Date,
                first.LocationId,
                assignmentStart,
                assignmentEnd,
                totalMinutes,
                timeZone.Id,
                segments),
            issues);
    }

    private static List<AssignmentSegment> MergeSegments(
        IReadOnlyList<WorkInterval> intervals)
    {
        var result = new List<AssignmentSegment>();
        foreach (var interval in intervals)
        {
            if (result.Count > 0 &&
                result[^1].TimeType == interval.TimeType &&
                interval.StartTime <= result[^1].EndTime)
            {
                var previous = result[^1];
                result[^1] = previous with
                {
                    EndTime = interval.EndTime > previous.EndTime
                        ? interval.EndTime
                        : previous.EndTime
                };
            }
            else
            {
                result.Add(new(
                    interval.StartTime,
                    interval.EndTime,
                    interval.TimeType));
            }
        }

        return result;
    }

    private static void ValidateLocalBoundary(
        DateOnly date,
        TimeOnly time,
        TimeZoneInfo timeZone,
        List<DomainValidationIssue> issues)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            issues.Add(new(
                "INVALID_LOCAL_TIME",
                "A helyi időpont az óraállítás miatt nem létezik."));
        }
        else if (timeZone.IsAmbiguousTime(local))
        {
            issues.Add(new(
                "AMBIGUOUS_LOCAL_TIME",
                "A helyi időpont az óraállítás miatt nem egyértelmű."));
        }
    }

    private static int GetElapsedMinutes(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        TimeZoneInfo timeZone)
    {
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(start, DateTimeKind.Unspecified),
            timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(end, DateTimeKind.Unspecified),
            timeZone);
        return checked((int)(endUtc - startUtc).TotalMinutes);
    }
}
