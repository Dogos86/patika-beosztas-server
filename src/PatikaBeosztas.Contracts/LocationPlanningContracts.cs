using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record OpeningIntervalRequest(
    TimeOnly StartTime,
    TimeOnly? EndTime);

public sealed record OpeningDayRequest(
    DayOfWeek DayOfWeek,
    OpeningDayMode Mode,
    IReadOnlyList<OpeningIntervalRequest> Intervals);

public sealed record UpdateLocationWeeklyOpeningRequest(
    IReadOnlyList<OpeningDayRequest> Days,
    uint? ExpectedVersion);

public sealed record OpeningIntervalResponse(
    Guid Id,
    TimeOnly StartTime,
    TimeOnly? EndTime);

public sealed record OpeningDayResponse(
    DayOfWeek DayOfWeek,
    OpeningDayMode Mode,
    IReadOnlyList<OpeningIntervalResponse> Intervals);

public sealed record LocationWeeklyOpeningResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    bool LocationIsActive,
    IReadOnlyList<OpeningDayResponse> Days,
    IReadOnlyList<string> Warnings,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

// Reserved value contract for a later date-specific exception endpoint.
public sealed record LocationOpeningExceptionDraft(
    DateOnly Date,
    OpeningDayMode Mode,
    IReadOnlyList<OpeningIntervalRequest> Intervals);

public sealed record CreateLocationShiftTemplateRequest(
    string Name,
    ShiftTemplateCategory Category,
    IReadOnlyList<DayOfWeek> Weekdays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive,
    StaffingCapability? RequiredCapability,
    TimeType TimeType = TimeType.Work);

public sealed record UpdateLocationShiftTemplateRequest(
    string Name,
    ShiftTemplateCategory Category,
    IReadOnlyList<DayOfWeek> Weekdays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive,
    StaffingCapability? RequiredCapability,
    uint ExpectedVersion,
    TimeType TimeType = TimeType.Work);

public sealed record DeactivateLocationShiftTemplateRequest(uint ExpectedVersion);

public sealed record LocationShiftTemplateResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    ShiftTemplateCategory Category,
    string Name,
    IReadOnlyList<DayOfWeek> Weekdays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive,
    StaffingCapability? RequiredCapability,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    TimeType TimeType = TimeType.Work);
