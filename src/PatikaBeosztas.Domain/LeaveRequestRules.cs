namespace PatikaBeosztas.Domain;

public static class LeaveRequestRules
{
    public const int MaximumNoteLength = 1000;

    public static LeaveRequestStatus InitialStatus(LeaveType type) =>
        type == LeaveType.SickLeave
            ? LeaveRequestStatus.Reported
            : LeaveRequestStatus.Draft;

    public static IReadOnlyList<DomainValidationIssue> ValidatePeriod(
        LeaveType type,
        DateOnly dateFrom,
        DateOnly? dateTo,
        bool isFullDay,
        TimeOnly? startTime,
        TimeOnly? endTime,
        string? employeeNote)
    {
        var issues = new List<DomainValidationIssue>();
        if (type != LeaveType.SickLeave && dateTo is null)
        {
            issues.Add(new(
                "LEAVE_END_DATE_REQUIRED",
                "A záró dátum csak betegállománynál maradhat nyitott."));
        }

        if (dateTo < dateFrom)
        {
            issues.Add(new(
                "LEAVE_DATE_ORDER",
                "A záró dátum nem lehet korábbi a kezdő dátumnál."));
        }

        if (isFullDay)
        {
            if (startTime is not null || endTime is not null)
            {
                issues.Add(new(
                    "FULL_DAY_LEAVE_HAS_TIME",
                    "Egész napos távolléthez nem adható kezdési vagy befejezési idő."));
            }
        }
        else
        {
            if (startTime is null || endTime is null)
            {
                issues.Add(new(
                    "PARTIAL_LEAVE_REQUIRES_TIME",
                    "Résznapos távolléthez a kezdési és befejezési idő kötelező."));
            }
            else if (startTime >= endTime)
            {
                issues.Add(new(
                    "LEAVE_TIME_ORDER",
                    "A kezdési időnek meg kell előznie a befejezési időt."));
            }

            if (dateTo is null || dateTo != dateFrom)
            {
                issues.Add(new(
                    "PARTIAL_LEAVE_SINGLE_DAY",
                    "Résznapos távollét csak egyetlen naptári napra rögzíthető."));
            }
        }

        if (employeeNote?.Trim().Length > MaximumNoteLength)
        {
            issues.Add(new(
                "LEAVE_NOTE_TOO_LONG",
                $"A megjegyzés legfeljebb {MaximumNoteLength} karakter lehet."));
        }

        if (type == LeaveType.SickLeave && !string.IsNullOrWhiteSpace(employeeNote))
        {
            issues.Add(new(
                "SICK_LEAVE_NOTE_NOT_ALLOWED",
                "Betegállományhoz egészségügyi szabad szöveg nem rögzíthető."));
        }

        return issues;
    }

    public static bool CanEdit(LeaveType type, LeaveRequestStatus status) =>
        type == LeaveType.SickLeave
            ? status == LeaveRequestStatus.Reported
            : status == LeaveRequestStatus.Draft;

    public static bool CanTransition(
        LeaveRequestStatus from,
        LeaveRequestStatus to) =>
        (from, to) switch
        {
            (LeaveRequestStatus.Draft, LeaveRequestStatus.Pending) => true,
            (LeaveRequestStatus.Draft, LeaveRequestStatus.Withdrawn) => true,
            (LeaveRequestStatus.Pending, LeaveRequestStatus.Approved) => true,
            (LeaveRequestStatus.Pending, LeaveRequestStatus.Rejected) => true,
            (LeaveRequestStatus.Pending, LeaveRequestStatus.Withdrawn) => true,
            (LeaveRequestStatus.Approved, LeaveRequestStatus.Cancelled) => true,
            (LeaveRequestStatus.Reported, LeaveRequestStatus.Recorded) => true,
            (LeaveRequestStatus.Recorded, LeaveRequestStatus.Closed) => true,
            _ => false
        };

    public static IReadOnlyList<DomainValidationIssue> ValidateTransition(
        LeaveRequestStatus from,
        LeaveRequestStatus to,
        DateOnly? dateTo,
        string? reason)
    {
        var issues = new List<DomainValidationIssue>();
        if (!CanTransition(from, to))
        {
            issues.Add(new(
                "INVALID_LEAVE_STATUS_TRANSITION",
                $"A(z) {from} állapotból a(z) {to} állapotba nem lehet átlépni."));
        }

        if (to == LeaveRequestStatus.Closed && dateTo is null)
        {
            issues.Add(new(
                "SICK_LEAVE_END_DATE_REQUIRED_TO_CLOSE",
                "Betegállomány lezárásához záró dátum szükséges."));
        }

        if (to is LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled &&
            string.IsNullOrWhiteSpace(reason))
        {
            issues.Add(new(
                "LEAVE_DECISION_REASON_REQUIRED",
                "Elutasításhoz vagy visszavonáshoz indoklás szükséges."));
        }

        if (reason?.Trim().Length > MaximumNoteLength)
        {
            issues.Add(new(
                "LEAVE_DECISION_REASON_TOO_LONG",
                $"Az indoklás legfeljebb {MaximumNoteLength} karakter lehet."));
        }

        return issues;
    }
}
