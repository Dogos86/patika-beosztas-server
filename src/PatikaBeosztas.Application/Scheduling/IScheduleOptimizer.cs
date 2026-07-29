using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Application.Scheduling;

public interface IScheduleOptimizer
{
    Task<ScheduleOptimizationResult> OptimizeAsync(
        ScheduleOptimizerInput input,
        CancellationToken cancellationToken);
}

public sealed record ScheduleOptimizationWeights(
    int BlockingShortage,
    int WarningShortage,
    int PreferredWindowMatch,
    int AvoidWindowViolation,
    int TargetHoursDeviation,
    int Overtime,
    int WeekendFairness,
    int EveningFairness,
    int LocationChange,
    int QuotaTarget,
    int LongShiftPreference,
    int PendingLeaveOverlap,
    int PreviousScheduleChange,
    int PreserveAcceptedDecision)
{
    public static ScheduleOptimizationWeights Default { get; } = new(
        BlockingShortage: 1_000_000,
        WarningShortage: 50_000,
        PreferredWindowMatch: 400,
        AvoidWindowViolation: 600,
        TargetHoursDeviation: 10,
        Overtime: 30,
        WeekendFairness: 20,
        EveningFairness: 10,
        LocationChange: 5,
        QuotaTarget: 100,
        LongShiftPreference: 10,
        PendingLeaveOverlap: 500,
        PreviousScheduleChange: 150,
        PreserveAcceptedDecision: 1_000);
}

public sealed record RegenerationScope(
    RegenerationScopeType Type,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Guid? LocationId,
    StaffingCapability? Capability,
    TimeType? TimeType,
    IReadOnlyList<Guid> IssueIds)
{
    public static RegenerationScope FullPeriod { get; } = new(
        RegenerationScopeType.FullPeriod,
        null,
        null,
        null,
        null,
        null,
        []);
}

public sealed record ScheduleGenerationOptions(
    int DeterministicSeed,
    int MaxSolveSeconds,
    int WorkerCount,
    PendingLeaveHandlingMode PendingLeaveHandling,
    ScheduleOptimizationWeights Weights,
    RegenerationScope Scope)
{
    public static ScheduleGenerationOptions CreateDefault(
        DateOnly periodStart,
        DateOnly periodEnd,
        int deterministicSeed = 1)
    {
        var days = periodEnd.DayNumber - periodStart.DayNumber + 1;
        return new(
            deterministicSeed,
            days <= 14 ? 20 : 60,
            1,
            PendingLeaveHandlingMode.IgnorePending,
            ScheduleOptimizationWeights.Default,
            RegenerationScope.FullPeriod);
    }
}

public sealed record ScheduleInputSnapshot(
    Guid OrganizationId,
    string OrganizationName,
    string TimeZoneId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string AlgorithmVersion,
    ScheduleGenerationOptions Options,
    IReadOnlyList<SnapshotLocation> Locations,
    IReadOnlyList<SnapshotOpeningInterval> OpeningIntervals,
    IReadOnlyList<SnapshotShiftTemplate> ShiftTemplates,
    IReadOnlyList<SnapshotCoverageRequirement> CoverageRequirements,
    IReadOnlyList<SnapshotEmployee> Employees,
    IReadOnlyList<SnapshotEmployeeLocation> EmployeeLocations,
    IReadOnlyList<SnapshotEmployeeCapability> EmployeeCapabilities,
    IReadOnlyList<SnapshotEmployeeWorkProfile> WorkProfiles,
    IReadOnlyList<SnapshotShiftQuota> ShiftQuotas,
    IReadOnlyList<SnapshotWorkPreference> WorkPreferences,
    IReadOnlyList<SnapshotLeave> LeaveRequests,
    IReadOnlyList<SnapshotExistingShift> ExistingShifts,
    IReadOnlyList<SnapshotRejectedSuggestion> RejectedSuggestions);

public sealed record SnapshotLocation(Guid Id, string Name, bool IsActive);

public sealed record SnapshotOpeningInterval(
    Guid LocationId,
    DayOfWeek DayOfWeek,
    OpeningDayMode Mode,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record SnapshotShiftTemplate(
    Guid Id,
    Guid LocationId,
    string Name,
    ShiftTemplateCategory Category,
    int WeekdayMask,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive,
    StaffingCapability? RequiredCapability,
    TimeType TimeType);

public sealed record SnapshotCoverageRequirement(
    Guid Id,
    Guid LocationId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    StaffingCapability RequiredCapability,
    int RequiredCount,
    CoverageSeverity Severity,
    bool IsActive,
    TimeType TimeType);

public sealed record SnapshotEmployee(
    Guid Id,
    string DisplayName,
    ProfessionalRole ProfessionalRole,
    bool IsActive,
    bool IsSchedulable,
    bool IncludeInAutoFill,
    bool CountsAsPharmacist);

public sealed record SnapshotEmployeeLocation(
    Guid EmployeeId,
    Guid LocationId,
    bool Enabled);

public sealed record SnapshotEmployeeCapability(
    Guid EmployeeId,
    StaffingCapability Capability);

public sealed record SnapshotEmployeeWorkProfile(
    Guid EmployeeId,
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
    bool IncludeInAutoFill);

public sealed record SnapshotShiftQuota(
    Guid Id,
    Guid EmployeeId,
    ShiftQuotaDimension Dimension,
    QuotaPeriod Period,
    int Minimum,
    int Target,
    int Maximum,
    QuotaSeverity Severity,
    bool IsActive);

public sealed record SnapshotWorkPreference(
    Guid Id,
    Guid EmployeeId,
    WorkPreferenceType Type,
    DateOnly DateFrom,
    DateOnly DateTo,
    DayOfWeek? DayOfWeek,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    Guid? LocationId,
    bool IsActive);

public sealed record SnapshotLeave(
    Guid Id,
    Guid EmployeeId,
    LeaveType Type,
    DateOnly DateFrom,
    DateOnly? DateTo,
    bool IsFullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    LeaveRequestStatus Status);

public sealed record SnapshotExistingShift(
    Guid Id,
    Guid EmployeeId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsLocked,
    ShiftAssignmentSource Source,
    IReadOnlyList<SnapshotShiftSegment> Segments);

public sealed record SnapshotShiftSegment(
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeType TimeType);

public sealed record SnapshotRejectedSuggestion(
    Guid ShiftAssignmentId,
    Guid EmployeeId,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    SuggestionExclusionScope ExclusionScope);

public sealed record ScheduleCandidateOption(
    string Key,
    Guid EmployeeId,
    string EmployeeDisplayName,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SnapshotShiftSegment> Segments,
    IReadOnlySet<StaffingCapability> EffectiveCapabilities,
    bool IsFixed,
    bool IsLocked,
    Guid? ExistingShiftId,
    bool HasPreferredMatch,
    bool HasAvoidViolation,
    bool HasPendingLeaveOverlap,
    bool MatchesPreviousPublished,
    bool IsLongShift,
    int TotalMinutes,
    int OvertimeMinutes);

public sealed record ScheduleCoverageSlot(
    string Key,
    Guid LocationId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    StaffingCapability RequiredCapability,
    TimeType TimeType,
    int RequiredCount,
    CoverageSeverity Severity);

public sealed record ScheduleOptimizerEmployee(
    Guid EmployeeId,
    int TargetMinutes,
    int? MaximumOvertimeMinutes,
    int? MaximumSaturdayAssignments,
    int? MaximumSundayAssignments,
    int? MaximumOnCallAssignments,
    int? MaximumStandbyAssignments);

public sealed record ScheduleOptimizerQuota(
    Guid Id,
    Guid EmployeeId,
    ShiftQuotaDimension Dimension,
    QuotaPeriod Period,
    int Minimum,
    int Target,
    int Maximum,
    QuotaSeverity Severity);

public sealed record ScheduleOptimizerInput(
    string AlgorithmVersion,
    string InputSnapshotHash,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int DeterministicSeed,
    int MaxSolveSeconds,
    int WorkerCount,
    ScheduleOptimizationWeights Weights,
    IReadOnlyList<ScheduleCandidateOption> Candidates,
    IReadOnlyList<ScheduleCoverageSlot> CoverageSlots,
    IReadOnlyList<ScheduleOptimizerEmployee> Employees,
    IReadOnlyList<ScheduleOptimizerQuota> Quotas);

public sealed record ScheduleOptimizationIssue(
    string Code,
    ScheduleIssueSeverity Severity,
    Guid? EmployeeId,
    Guid? LocationId,
    DateOnly? Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    IReadOnlyDictionary<string, object?> Parameters);

public sealed record ScheduleAlternativeScore(
    Guid EmployeeId,
    string EmployeeDisplayName,
    long ScoreDifference,
    IReadOnlyDictionary<string, long> ScoreComponents,
    IReadOnlyList<string> TradeoffCodes);

public sealed record ScheduleSelectedAssignment(
    ScheduleCandidateOption Candidate,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyDictionary<string, long> ScoreComponents,
    IReadOnlyList<ScheduleAlternativeScore> Alternatives);

public sealed record ScheduleSolverStatistics(
    int CandidateOptionCount,
    int VariableCount,
    int ConstraintCount,
    double WallTimeSeconds,
    long? BestObjectiveBound,
    long? Conflicts,
    long? Branches);

public sealed record ScheduleOptimizationResult(
    ScheduleSolverStatus Status,
    IReadOnlyList<ScheduleSelectedAssignment> Assignments,
    IReadOnlyList<ScheduleOptimizationIssue> Issues,
    long? ObjectiveValue,
    ScheduleSolverStatistics Statistics,
    string? ErrorCode,
    string? RedactedError)
{
    public bool IsAccepted =>
        Status is ScheduleSolverStatus.Optimal or ScheduleSolverStatus.Feasible;
}
