using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record EmployeeTimeWindowRequest(
    DayOfWeek? DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    EmployeeTimeWindowType Type);

public sealed record EmployeeLocationRequest(Guid LocationId, bool Enabled = true);

public sealed record CreateEmployeeRequest(
    string FullName,
    string DisplayName,
    ProfessionalRole ProfessionalRole,
    bool IsActive,
    bool IsSchedulable,
    bool IncludeInAutoFill,
    bool CountsAsPharmacist,
    int? MonthlyMinutesLimit,
    int? MaxDailyMinutes,
    DateOnly? BirthDate,
    string? ExternalPayrollId,
    IReadOnlyList<EmployeeLocationRequest>? Locations,
    IReadOnlyList<EmployeeTimeWindowRequest>? TimeWindows,
    IReadOnlyList<TimeType>? AllowedTimeTypes);

public sealed record UpdateEmployeeRequest(
    string FullName,
    string DisplayName,
    ProfessionalRole ProfessionalRole,
    bool IsActive,
    bool IsSchedulable,
    bool IncludeInAutoFill,
    bool CountsAsPharmacist,
    int? MonthlyMinutesLimit,
    int? MaxDailyMinutes,
    DateOnly? BirthDate,
    string? ExternalPayrollId,
    IReadOnlyList<EmployeeLocationRequest>? Locations,
    IReadOnlyList<EmployeeTimeWindowRequest>? TimeWindows,
    IReadOnlyList<TimeType>? AllowedTimeTypes,
    uint ExpectedVersion);

public sealed record EmployeeResponse(
    Guid Id,
    string FullName,
    string DisplayName,
    ProfessionalRole ProfessionalRole,
    bool IsActive,
    bool IsSchedulable,
    bool IncludeInAutoFill,
    bool CountsAsPharmacist,
    int? MonthlyMinutesLimit,
    int? MaxDailyMinutes,
    DateOnly? BirthDate,
    string? ExternalPayrollId,
    IReadOnlyList<EmployeeLocationResponse> Locations,
    IReadOnlyList<EmployeeTimeWindowResponse> TimeWindows,
    IReadOnlyList<TimeType> AllowedTimeTypes,
    LinkedUserSummary? LinkedUser,
    IReadOnlyList<string> Warnings,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record EmployeeLocationResponse(
    Guid LocationId,
    string LocationName,
    bool Enabled);

public sealed record EmployeeTimeWindowResponse(
    Guid Id,
    DayOfWeek? DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    EmployeeTimeWindowType Type);

public sealed record LinkedUserSummary(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsActive);
