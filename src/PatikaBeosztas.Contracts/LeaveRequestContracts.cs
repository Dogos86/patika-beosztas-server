using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record CreateLeaveRequest(
    LeaveType Type,
    DateOnly DateFrom,
    DateOnly? DateTo,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? EmployeeNote);

public sealed record UpdateLeaveRequest(
    DateOnly DateFrom,
    DateOnly? DateTo,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? EmployeeNote,
    uint ExpectedVersion);

public sealed record LeaveVersionRequest(uint ExpectedVersion);

public sealed record LeaveDecisionRequest(
    LeaveDecision Decision,
    string? Reason,
    uint ExpectedVersion);

public sealed record CloseSickLeaveRequest(
    DateOnly DateTo,
    uint ExpectedVersion);

public sealed record CancelLeaveRequest(
    string Reason,
    uint ExpectedVersion);

public sealed record LeaveStatusHistoryResponse(
    LeaveRequestStatus? FromStatus,
    LeaveRequestStatus ToStatus,
    DateTimeOffset OccurredAtUtc,
    string? Reason);

public sealed record LeaveRequestResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeDisplayName,
    LeaveType Type,
    DateOnly DateFrom,
    DateOnly? DateTo,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    LeaveRequestStatus Status,
    string? EmployeeNote,
    string? DecisionReason,
    IReadOnlyList<LeaveStatusHistoryResponse> StatusHistory,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
