using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record UpdateEmployeeCapabilitiesRequest(
    IReadOnlyList<StaffingCapability> Capabilities,
    uint ExpectedEmployeeVersion);

public sealed record EmployeeCapabilitiesResponse(
    Guid EmployeeId,
    string EmployeeDisplayName,
    IReadOnlyList<StaffingCapability> AssignedCapabilities,
    IReadOnlyList<StaffingCapability> EffectiveCapabilities,
    bool CountsAsPharmacistCompatibility,
    uint EmployeeVersion);

public sealed record UpdateEmployeeWorkProfileRequest(
    int ContractedMonthlyMinutes,
    int? ContractedWeeklyMinutes,
    int StandardShiftMinutes,
    int MinimumShiftMinutes,
    int MaximumRegularShiftMinutes,
    int MaximumDailyMinutes,
    bool AllowsLongShift,
    int? MaximumLongShiftMinutes,
    bool AllowsFullOpeningHoursShift,
    bool AllowsOvertime,
    int? MaximumOvertimeMinutesPerMonth,
    bool AllowsOnCallDuty,
    int? MaximumOnCallAssignmentsPerMonth,
    bool AllowsStandby,
    int? MaximumStandbyAssignmentsPerMonth,
    bool AllowsSaturday,
    int? MaximumSaturdaysPerMonth,
    bool AllowsSunday,
    int? MaximumSundaysPerMonth,
    bool IncludeInAutoFill,
    uint? ExpectedVersion);

public sealed record EmployeeWorkProfileResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeDisplayName,
    int ContractedMonthlyMinutes,
    int? ContractedWeeklyMinutes,
    int StandardShiftMinutes,
    int MinimumShiftMinutes,
    int MaximumRegularShiftMinutes,
    int MaximumDailyMinutes,
    bool AllowsLongShift,
    int? MaximumLongShiftMinutes,
    bool AllowsFullOpeningHoursShift,
    bool AllowsOvertime,
    int? MaximumOvertimeMinutesPerMonth,
    bool AllowsOnCallDuty,
    int? MaximumOnCallAssignmentsPerMonth,
    bool AllowsStandby,
    int? MaximumStandbyAssignmentsPerMonth,
    bool AllowsSaturday,
    int? MaximumSaturdaysPerMonth,
    bool AllowsSunday,
    int? MaximumSundaysPerMonth,
    bool IncludeInAutoFill,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateEmployeeShiftQuotaRuleRequest(
    ShiftQuotaDimension Dimension,
    QuotaPeriod Period,
    int Minimum,
    int Target,
    int Maximum,
    QuotaSeverity Severity,
    bool IsActive);

public sealed record UpdateEmployeeShiftQuotaRuleRequest(
    ShiftQuotaDimension Dimension,
    QuotaPeriod Period,
    int Minimum,
    int Target,
    int Maximum,
    QuotaSeverity Severity,
    bool IsActive,
    uint ExpectedVersion);

public sealed record DeactivateEmployeeShiftQuotaRuleRequest(uint ExpectedVersion);

public sealed record EmployeeShiftQuotaRuleResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeDisplayName,
    ShiftQuotaDimension Dimension,
    QuotaPeriod Period,
    int Minimum,
    int Target,
    int Maximum,
    QuotaSeverity Severity,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
