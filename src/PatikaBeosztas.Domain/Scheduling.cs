namespace PatikaBeosztas.Domain;

public sealed class SchedulePlan
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public required string TimeZoneId { get; set; }

    public ScheduleStatus Status { get; set; }

    public Guid? BasedOnScheduleId { get; set; }

    public int PublishedRevisionNumber { get; set; }

    public required string AlgorithmVersion { get; set; }

    public required string GenerationOptionsSnapshot { get; set; }

    public required string InputSnapshotHash { get; set; }

    public string? CloneIdempotencyKeyHash { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid? ReviewRequestedByUserId { get; set; }

    public DateTimeOffset? ReviewRequestedAtUtc { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public Guid? ArchivedByUserId { get; set; }

    public DateTimeOffset? ArchivedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Organization? Organization { get; set; }

    public SchedulePlan? BasedOnSchedule { get; set; }

    public ICollection<ScheduleGenerationRun> GenerationRuns { get; } =
        new List<ScheduleGenerationRun>();

    public ICollection<ShiftAssignment> ShiftAssignments { get; } =
        new List<ShiftAssignment>();

    public ICollection<ScheduleIssue> Issues { get; } =
        new List<ScheduleIssue>();
}

public sealed class ScheduleGenerationRun
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid SchedulePlanId { get; set; }

    public ScheduleGenerationStatus Status { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? CancellationRequestedAtUtc { get; set; }

    public required string AlgorithmVersion { get; set; }

    public int DeterministicSeed { get; set; }

    public required string OptionsJson { get; set; }

    public required string InputSnapshotJson { get; set; }

    public required string InputSnapshotHash { get; set; }

    public ScheduleSolverStatus SolverStatus { get; set; }

    public required string SolverStatisticsJson { get; set; }

    public long? ObjectiveValue { get; set; }

    public string? ErrorCode { get; set; }

    public string? RedactedError { get; set; }

    public required string IdempotencyKeyHash { get; set; }

    public required string ScopeConcurrencyKey { get; set; }

    public uint Version { get; private set; }

    public SchedulePlan? SchedulePlan { get; set; }

    public ICollection<ShiftAssignment> GeneratedAssignments { get; } =
        new List<ShiftAssignment>();

    public ICollection<ScheduleIssue> Issues { get; } =
        new List<ScheduleIssue>();
}

public sealed class ShiftAssignment
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid SchedulePlanId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid LocationId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public ShiftAssignmentSource Source { get; set; }

    public bool IsLocked { get; set; }

    public Guid? GeneratedByRunId { get; set; }

    public Guid? ReplacesShiftId { get; set; }

    public ShiftChangeKind ChangeKind { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public SchedulePlan? SchedulePlan { get; set; }

    public Employee? Employee { get; set; }

    public Location? Location { get; set; }

    public ScheduleGenerationRun? GeneratedByRun { get; set; }

    public ShiftAssignment? ReplacesShift { get; set; }

    public ICollection<ShiftSegment> Segments { get; } =
        new List<ShiftSegment>();

    public ShiftExplanation? Explanation { get; set; }
}

public sealed class ShiftSegment
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid ShiftAssignmentId { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public TimeType TimeType { get; set; }

    public ShiftAssignment? ShiftAssignment { get; set; }
}

public sealed class ScheduleIssue
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid SchedulePlanId { get; set; }

    public Guid? GenerationRunId { get; set; }

    public required string Code { get; set; }

    public ScheduleIssueSeverity Severity { get; set; }

    public Guid? EmployeeId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? ShiftAssignmentId { get; set; }

    public DateOnly? Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public required string ParametersJson { get; set; }

    public bool IsResolved { get; set; }

    public bool IsAcknowledged { get; set; }

    public Guid? ResolutionByUserId { get; set; }

    public DateTimeOffset? ResolutionAtUtc { get; set; }

    public string? ResolutionNote { get; set; }

    public uint Version { get; private set; }

    public SchedulePlan? SchedulePlan { get; set; }

    public ScheduleGenerationRun? GenerationRun { get; set; }
}

public sealed class ShiftExplanation
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid SchedulePlanId { get; set; }

    public Guid ShiftAssignmentId { get; set; }

    public Guid GenerationRunId { get; set; }

    public required string AlgorithmVersion { get; set; }

    public required string ReasonCodesJson { get; set; }

    public required string ScoreComponentsJson { get; set; }

    public required string AlternativesJson { get; set; }

    public ShiftAssignment? ShiftAssignment { get; set; }
}

public sealed class GeneratedSuggestionDecision
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid SchedulePlanId { get; set; }

    public Guid ShiftAssignmentId { get; set; }

    public Guid? GenerationRunId { get; set; }

    public GeneratedSuggestionDecisionType DecisionType { get; set; }

    public Guid ActorUserId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? Reason { get; set; }

    public SuggestionExclusionScope ExclusionScope { get; set; }

    public ShiftAssignment? ShiftAssignment { get; set; }
}
