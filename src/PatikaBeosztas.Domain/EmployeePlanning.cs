namespace PatikaBeosztas.Domain;

public sealed class EmployeeCapability
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public StaffingCapability Capability { get; set; }

    public DateTimeOffset AssignedAtUtc { get; set; }

    public Employee? Employee { get; set; }
}

public sealed class EmployeeWorkProfile
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public int ContractedMonthlyMinutes { get; set; }

    public int? ContractedWeeklyMinutes { get; set; }

    public int StandardShiftMinutes { get; set; }

    public int MinimumShiftMinutes { get; set; }

    public int MaximumRegularShiftMinutes { get; set; }

    public int MaximumDailyMinutes { get; set; }

    public bool AllowsLongShift { get; set; }

    public int? MaximumLongShiftMinutes { get; set; }

    public bool AllowsFullOpeningHoursShift { get; set; }

    public bool AllowsOvertime { get; set; }

    public int? MaximumOvertimeMinutesPerMonth { get; set; }

    public bool AllowsOnCallDuty { get; set; }

    public int? MaximumOnCallAssignmentsPerMonth { get; set; }

    public bool AllowsStandby { get; set; }

    public int? MaximumStandbyAssignmentsPerMonth { get; set; }

    public bool AllowsSaturday { get; set; }

    public int? MaximumSaturdaysPerMonth { get; set; }

    public bool AllowsSunday { get; set; }

    public int? MaximumSundaysPerMonth { get; set; }

    public bool IncludeInAutoFill { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Employee? Employee { get; set; }
}

public sealed class EmployeeShiftQuotaRule
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public ShiftQuotaDimension Dimension { get; set; }

    public QuotaPeriod Period { get; set; }

    public int Minimum { get; set; }

    public int Target { get; set; }

    public int Maximum { get; set; }

    public QuotaSeverity Severity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Employee? Employee { get; set; }
}
