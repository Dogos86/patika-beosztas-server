using PharmacyScheduler.Core;

namespace PharmacyScheduler.Core.Models;

public sealed class SchedulePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Új beosztás";
    public DateOnly PeriodStart { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly PeriodEnd { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(6));
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Draft;
    public string CreatedBy { get; set; } = Environment.UserName;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<ShiftEntry> Entries { get; set; } = new();

    public string DisplayTitle => $"{Name} ({PeriodStart:yyyy-MM-dd} - {PeriodEnd:yyyy-MM-dd}) [{Status.ToDisplayText()}]";
}
