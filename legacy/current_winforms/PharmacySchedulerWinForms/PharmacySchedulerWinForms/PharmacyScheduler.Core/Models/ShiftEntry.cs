namespace PharmacyScheduler.Core.Models;

public sealed class ShiftEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScheduleId { get; set; }
    public Guid LocationId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly Start { get; set; } = new(8, 0);
    public TimeOnly End { get; set; } = new(16, 0);
    public TimeType TimeType { get; set; } = TimeType.Work;
    public string Note { get; set; } = string.Empty;

    public decimal Hours => (decimal)(End.ToTimeSpan() - Start.ToTimeSpan()).TotalHours;
}
