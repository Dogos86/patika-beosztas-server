using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record ScheduleGenerationWeightsRequest(
    int? BlockingShortage,
    int? WarningShortage,
    int? PreferredWindowMatch,
    int? AvoidWindowViolation,
    int? TargetHoursDeviation,
    int? Overtime,
    int? WeekendFairness,
    int? EveningFairness,
    int? LocationChange,
    int? QuotaTarget,
    int? LongShiftPreference,
    int? PendingLeaveOverlap,
    int? PreviousScheduleChange,
    int? PreserveAcceptedDecision);

public sealed record RegenerationScopeRequest(
    RegenerationScopeType Type,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Guid? LocationId,
    StaffingCapability? Capability,
    TimeType? TimeType,
    IReadOnlyList<Guid>? IssueIds);

public sealed record CreateScheduleGenerationRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int? DeterministicSeed,
    int? MaxSolveSeconds,
    int? WorkerCount,
    PendingLeaveHandlingMode PendingLeaveHandling =
        PendingLeaveHandlingMode.IgnorePending,
    ScheduleGenerationWeightsRequest? Weights = null);

public sealed record RegenerateScheduleRequest(
    RegenerationScopeRequest Scope,
    uint ExpectedVersion,
    int? DeterministicSeed,
    int? MaxSolveSeconds,
    int? WorkerCount,
    PendingLeaveHandlingMode PendingLeaveHandling =
        PendingLeaveHandlingMode.IgnorePending,
    ScheduleGenerationWeightsRequest? Weights = null);

public sealed record CancelScheduleGenerationRequest(uint ExpectedVersion);

public sealed record ScheduleGenerationRunResponse(
    Guid Id,
    Guid SchedulePlanId,
    ScheduleGenerationStatus Status,
    ScheduleSolverStatus SolverStatus,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancellationRequestedAtUtc,
    string AlgorithmVersion,
    int DeterministicSeed,
    string InputSnapshotHash,
    long? ObjectiveValue,
    ScheduleSolverStatisticsResponse? Statistics,
    string? ErrorCode,
    string? RedactedError,
    uint Version);

public sealed record ScheduleSolverStatisticsResponse(
    int CandidateOptionCount,
    int VariableCount,
    int ConstraintCount,
    double WallTimeSeconds,
    long? BestObjectiveBound,
    long? Conflicts,
    long? Branches);

public sealed record ScheduleGenerationReadinessCountsResponse(
    int ActiveLocationCount,
    int OpeningIntervalCount,
    int ActiveShiftTemplateCount,
    int ApplicableShiftTemplateCount,
    int CoverageRequirementCount,
    int ActiveEmployeeCount,
    int SchedulableEmployeeCount,
    int AutoFillEmployeeCount,
    int LocationAssignedEmployeeCount,
    int WorkProfileEmployeeCount,
    int CapableEmployeeCount,
    int CandidateOptionCount);

public sealed record ScheduleGenerationPreflightIssueResponse(
    string Code,
    ScheduleIssueSeverity Severity,
    string Message,
    string? SettingsPath);

public sealed record ScheduleGenerationPreflightResponse(
    bool CanStart,
    ScheduleGenerationReadinessCountsResponse Counts,
    IReadOnlyList<ScheduleGenerationPreflightIssueResponse> Issues);

public sealed record ScheduleListItemResponse(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string TimeZoneId,
    ScheduleStatus Status,
    Guid? BasedOnScheduleId,
    int PublishedRevisionNumber,
    string AlgorithmVersion,
    string InputSnapshotHash,
    int ShiftCount,
    int BlockingIssueCount,
    int WarningIssueCount,
    uint Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record SchedulePlanResponse(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string TimeZoneId,
    ScheduleStatus Status,
    Guid? BasedOnScheduleId,
    int PublishedRevisionNumber,
    string AlgorithmVersion,
    string InputSnapshotHash,
    IReadOnlyList<ShiftAssignmentResponse> Shifts,
    ScheduleGenerationSummaryResponse Summary,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ReviewRequestedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record ShiftAssignmentResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeDisplayName,
    Guid LocationId,
    string LocationName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    ShiftAssignmentSource Source,
    bool IsLocked,
    Guid? GeneratedByRunId,
    Guid? ReplacesShiftId,
    ShiftChangeKind ChangeKind,
    IReadOnlyList<ShiftSegmentResponse> Segments,
    uint Version);

public sealed record ShiftSegmentResponse(
    Guid Id,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeType TimeType,
    int Minutes);

public sealed record ScheduleGenerationSummaryResponse(
    decimal BlockingCoveragePercent,
    int BlockingIssueCount,
    int WarningIssueCount,
    decimal PreferenceFulfillmentPercent,
    int EmployeesOutsideTargetCount,
    int PendingLeaveOverlapShiftCount,
    int MultiLocationConflictCount,
    int NewShiftCount,
    int ModifiedShiftCount,
    int DeletedShiftCount,
    int UnchangedShiftCount,
    int PlannedOvertimeMinutes);

public sealed record EmployeeScheduleMatrixResponse(
    Guid ScheduleId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    uint ScheduleVersion,
    IReadOnlyList<EmployeeScheduleRowResponse> Employees);

public sealed record EmployeeScheduleRowResponse(
    Guid EmployeeId,
    string EmployeeDisplayName,
    IReadOnlyList<EmployeeScheduleDayCellResponse> Days,
    int AssignedMinutes,
    int TargetMinutes,
    bool HasWorkProfile,
    int PlannedOvertimeMinutes,
    int WeekendShiftCount,
    int EveningShiftCount,
    int LocationChangeCount,
    int WarningIssueCount);

public sealed record EmployeeScheduleDayCellResponse(
    DateOnly Date,
    IReadOnlyList<ShiftAssignmentResponse> Shifts,
    IReadOnlyList<LeaveMarkerResponse> LeaveMarkers,
    int IssueCount);

public sealed record LeaveMarkerResponse(
    Guid LeaveRequestId,
    LeaveType Type,
    LeaveRequestStatus Status,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record LocationCoverageResponse(
    Guid ScheduleId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    uint ScheduleVersion,
    bool HasConfiguredRequirements,
    IReadOnlyList<LocationCoverageSlotResponse> Slots);

public sealed record LocationCoverageSlotResponse(
    Guid LocationId,
    string LocationName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    StaffingCapability RequiredCapability,
    TimeType TimeType,
    int RequiredCount,
    int ActualCount,
    int Shortage,
    CoverageSeverity Severity,
    string Status,
    IReadOnlyList<Guid> EmployeeIds);

public sealed record ScheduleIssueResponse(
    Guid Id,
    string Code,
    ScheduleIssueSeverity Severity,
    Guid? EmployeeId,
    Guid? LocationId,
    Guid? ShiftAssignmentId,
    DateOnly? Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string ParametersJson,
    bool IsResolved,
    bool IsAcknowledged,
    uint Version);

public sealed record ScheduleChangeResponse(
    ShiftChangeKind ChangeKind,
    Guid? ShiftAssignmentId,
    Guid? BasedOnShiftId,
    Guid EmployeeId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record ShiftExplanationResponse(
    Guid ShiftAssignmentId,
    Guid GenerationRunId,
    string AlgorithmVersion,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyDictionary<string, long> ScoreComponents,
    IReadOnlyList<ScheduleAlternativeResponse> Alternatives);

public sealed record ScheduleAlternativeResponse(
    Guid EmployeeId,
    string EmployeeDisplayName,
    long ScoreDifference,
    IReadOnlyDictionary<string, long> ScoreComponents,
    IReadOnlyList<string> TradeoffCodes);

public sealed record ShiftVersionRequest(
    uint ExpectedShiftVersion,
    uint ExpectedScheduleVersion,
    string? Reason = null);

public sealed record RejectGeneratedSuggestionRequest(
    uint ExpectedShiftVersion,
    uint ExpectedScheduleVersion,
    string Reason,
    SuggestionExclusionScope ExclusionScope =
        SuggestionExclusionScope.Schedule);

public sealed record ReplaceShiftRequest(
    Guid ReplacementEmployeeId,
    uint ExpectedShiftVersion,
    uint ExpectedScheduleVersion,
    string Reason);

public sealed record ScheduleVersionRequest(uint ExpectedVersion);

public sealed record CloneScheduleDraftRequest(uint ExpectedVersion);

public sealed record OwnScheduleResponse(
    Guid ScheduleId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int PublishedRevisionNumber,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<OwnShiftResponse> Shifts);

public sealed record OwnShiftResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<ShiftSegmentResponse> Segments);
