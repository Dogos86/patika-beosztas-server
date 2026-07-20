namespace PharmacyScheduler.Core.Models;

public sealed class ScheduleExportRow
{
    public string LocationName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public string EmployeeFullName { get; set; } = string.Empty;
    public string EmployeeDisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string TimeTypeCode { get; set; } = string.Empty;
    public string TimeTypeName { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class ScheduleSummaryRow
{
    public string EmployeeFullName { get; set; } = string.Empty;
    public string EmployeeDisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string LocationNames { get; set; } = string.Empty;
    public string TimeTypeName { get; set; } = string.Empty;
    public decimal Hours { get; set; }
}
