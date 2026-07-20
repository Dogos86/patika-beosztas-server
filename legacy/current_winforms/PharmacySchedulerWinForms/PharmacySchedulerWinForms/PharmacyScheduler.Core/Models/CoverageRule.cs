namespace PharmacyScheduler.Core.Models;

public sealed class CoverageRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LocationId { get; set; }
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;
    public TimeOnly Start { get; set; } = new(8, 0);
    public TimeOnly End { get; set; } = new(16, 0);
    public EmployeeRole Role { get; set; } = EmployeeRole.Pharmacist;
    public int RequiredCount { get; set; } = 1;
    public Severity Severity { get; set; } = Severity.Soft;
}
