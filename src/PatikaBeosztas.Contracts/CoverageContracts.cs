using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record CreateCoverageRequirementRequest(
    Guid LocationId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    StaffingCapability RequiredCapability,
    int RequiredCount,
    CoverageSeverity Severity,
    bool IsActive,
    TimeType TimeType = TimeType.Work);

public sealed record UpdateCoverageRequirementRequest(
    Guid LocationId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    StaffingCapability RequiredCapability,
    int RequiredCount,
    CoverageSeverity Severity,
    bool IsActive,
    uint ExpectedVersion,
    TimeType TimeType = TimeType.Work);

public sealed record DeactivateCoverageRequirementRequest(uint ExpectedVersion);

public sealed record CoverageRequirementResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    bool LocationIsActive,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    StaffingCapability RequiredCapability,
    int RequiredCount,
    CoverageSeverity Severity,
    bool IsActive,
    IReadOnlyList<string> Warnings,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    TimeType TimeType = TimeType.Work);
