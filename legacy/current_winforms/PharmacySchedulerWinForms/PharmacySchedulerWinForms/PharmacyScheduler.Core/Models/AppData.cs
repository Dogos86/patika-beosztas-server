namespace PharmacyScheduler.Core.Models;

public sealed class AppData
{
    public List<Location> Locations { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
    public List<CoverageRule> CoverageRules { get; set; } = new();
    public List<LeaveEntry> Leaves { get; set; } = new();
    public List<SchedulePlan> Schedules { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}
