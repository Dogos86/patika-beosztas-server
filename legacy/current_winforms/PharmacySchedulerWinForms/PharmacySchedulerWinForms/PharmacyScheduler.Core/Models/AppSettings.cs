namespace PharmacyScheduler.Core.Models;

public sealed class AppSettings
{
    public Severity DailyHoursSeverity { get; set; } = Severity.Soft;
    public Severity MonthlyHoursSeverity { get; set; } = Severity.Soft;
    public Severity PreferredWindowSeverity { get; set; } = Severity.Soft;
    public Severity ForbiddenWindowSeverity { get; set; } = Severity.Hard;
    public Severity AllowedTimeTypeSeverity { get; set; } = Severity.Hard;
    public Severity AllowedLocationSeverity { get; set; } = Severity.Hard;
    public Severity LeaveConflictSeverity { get; set; } = Severity.Hard;
}
