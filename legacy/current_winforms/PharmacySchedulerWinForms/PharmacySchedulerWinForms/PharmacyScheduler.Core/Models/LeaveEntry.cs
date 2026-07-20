namespace PharmacyScheduler.Core.Models;

public sealed class LeaveEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public TimeType LeaveType { get; set; } = TimeType.Vacation;
    public string Note { get; set; } = string.Empty;
}
