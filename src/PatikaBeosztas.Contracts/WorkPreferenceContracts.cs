using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record CreateWorkPreferenceRequest(
    WorkPreferenceType Type,
    DateOnly DateFrom,
    DateOnly DateTo,
    DayOfWeek? DayOfWeek,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid? LocationId,
    string? Note);

public sealed record UpdateWorkPreferenceRequest(
    WorkPreferenceType Type,
    DateOnly DateFrom,
    DateOnly DateTo,
    DayOfWeek? DayOfWeek,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid? LocationId,
    string? Note,
    uint ExpectedVersion);

public sealed record DeactivateWorkPreferenceRequest(uint ExpectedVersion);

public sealed record WorkPreferenceResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeDisplayName,
    WorkPreferenceType Type,
    DateOnly DateFrom,
    DateOnly DateTo,
    DayOfWeek? DayOfWeek,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid? LocationId,
    string? LocationName,
    string? Note,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
