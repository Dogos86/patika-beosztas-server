using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Api.Endpoints;

internal static class ScheduleMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static ScheduleGenerationRunResponse Map(ScheduleGenerationRun run)
    {
        var statistics = string.IsNullOrWhiteSpace(run.SolverStatisticsJson) ||
                         run.SolverStatisticsJson == "{}"
            ? null
            : JsonSerializer.Deserialize<ScheduleSolverStatistics>(
                run.SolverStatisticsJson,
                JsonOptions);
        return new(
            run.Id,
            run.SchedulePlanId,
            run.Status,
            run.SolverStatus,
            run.RequestedAtUtc,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.CancellationRequestedAtUtc,
            run.AlgorithmVersion,
            run.DeterministicSeed,
            run.InputSnapshotHash,
            run.ObjectiveValue,
            statistics is null
                ? null
                : new ScheduleSolverStatisticsResponse(
                    statistics.CandidateOptionCount,
                    statistics.VariableCount,
                    statistics.ConstraintCount,
                    statistics.WallTimeSeconds,
                    statistics.BestObjectiveBound,
                    statistics.Conflicts,
                    statistics.Branches),
            run.ErrorCode,
            run.RedactedError,
            run.Version);
    }

    public static SchedulePlanResponse Map(SchedulePlan plan)
    {
        var shifts = plan.ShiftAssignments
            .Where(shift => shift.ChangeKind != ShiftChangeKind.Deleted)
            .OrderBy(shift => shift.Date)
            .ThenBy(shift => shift.StartTime)
            .ThenBy(shift => shift.EmployeeId)
            .Select(Map)
            .ToArray();
        return new(
            plan.Id,
            plan.PeriodStart,
            plan.PeriodEnd,
            plan.TimeZoneId,
            plan.Status,
            plan.BasedOnScheduleId,
            plan.PublishedRevisionNumber,
            plan.AlgorithmVersion,
            plan.InputSnapshotHash,
            shifts,
            BuildSummary(plan, shifts),
            plan.Version,
            plan.CreatedAtUtc,
            plan.UpdatedAtUtc,
            plan.ReviewRequestedAtUtc,
            plan.ApprovedAtUtc,
            plan.PublishedAtUtc,
            plan.ArchivedAtUtc);
    }

    public static ShiftAssignmentResponse Map(ShiftAssignment shift) =>
        new(
            shift.Id,
            shift.EmployeeId,
            shift.Employee?.DisplayName ?? string.Empty,
            shift.LocationId,
            shift.Location?.Name ?? string.Empty,
            shift.Date,
            shift.StartTime,
            shift.EndTime,
            shift.Source,
            shift.IsLocked,
            shift.GeneratedByRunId,
            shift.ReplacesShiftId,
            shift.ChangeKind,
            shift.Segments
                .OrderBy(segment => segment.StartTime)
                .Select(segment => new ShiftSegmentResponse(
                    segment.Id,
                    segment.StartTime,
                    segment.EndTime,
                    segment.TimeType,
                    Minutes(segment.StartTime, segment.EndTime)))
                .ToArray(),
            shift.Version);

    public static ScheduleIssueResponse Map(ScheduleIssue issue) =>
        new(
            issue.Id,
            issue.Code,
            issue.Severity,
            issue.EmployeeId,
            issue.LocationId,
            issue.ShiftAssignmentId,
            issue.Date,
            issue.StartTime,
            issue.EndTime,
            issue.ParametersJson,
            issue.IsResolved,
            issue.IsAcknowledged,
            issue.Version);

    public static ShiftExplanationResponse Map(ShiftExplanation explanation)
    {
        var reasonCodes = JsonSerializer.Deserialize<string[]>(
            explanation.ReasonCodesJson,
            JsonOptions) ?? [];
        var scoreComponents = JsonSerializer.Deserialize<Dictionary<string, long>>(
            explanation.ScoreComponentsJson,
            JsonOptions) ?? [];
        var alternatives = JsonSerializer.Deserialize<ScheduleAlternativeScore[]>(
            explanation.AlternativesJson,
            JsonOptions) ?? [];
        return new(
            explanation.ShiftAssignmentId,
            explanation.GenerationRunId,
            explanation.AlgorithmVersion,
            reasonCodes,
            scoreComponents,
            alternatives.Select(item => new ScheduleAlternativeResponse(
                item.EmployeeId,
                item.EmployeeDisplayName,
                item.ScoreDifference,
                item.ScoreComponents,
                item.TradeoffCodes)).ToArray());
    }

    public static IQueryable<SchedulePlan> Query(PatikaBeosztas.Infrastructure.Persistence.PatikaDbContext dbContext) =>
        dbContext.SchedulePlans
            .Include(plan => plan.ShiftAssignments)
                .ThenInclude(shift => shift.Segments)
            .Include(plan => plan.ShiftAssignments)
                .ThenInclude(shift => shift.Employee)
            .Include(plan => plan.ShiftAssignments)
                .ThenInclude(shift => shift.Location)
            .Include(plan => plan.Issues);

    private static ScheduleGenerationSummaryResponse BuildSummary(
        SchedulePlan plan,
        IReadOnlyList<ShiftAssignmentResponse> shifts)
    {
        var blocking = plan.Issues.Count(issue =>
            issue.Severity == ScheduleIssueSeverity.Blocking &&
            !issue.IsResolved);
        var warnings = plan.Issues.Count(issue =>
            issue.Severity == ScheduleIssueSeverity.Warning &&
            !issue.IsResolved);
        var coverageIssues = plan.Issues.Count(issue =>
            issue.Code == "COVERAGE_SHORTAGE");
        var coveragePercent = coverageIssues == 0 ? 100m : 0m;
        var preferredMisses = plan.Issues.Count(issue =>
            issue.Code == "PREFERRED_WINDOW_NOT_MET");
        var preferencePercent = preferredMisses == 0 ? 100m : 0m;
        return new(
            coveragePercent,
            blocking,
            warnings,
            preferencePercent,
            plan.Issues.Where(issue => issue.Code == "TARGET_HOURS_DEVIATION")
                .Select(issue => issue.EmployeeId)
                .Distinct()
                .Count(),
            plan.Issues.Count(issue => issue.Code == "PENDING_LEAVE_OVERLAP"),
            plan.Issues.Count(issue =>
                issue.Code == "MULTI_LOCATION_SAME_DAY_NOT_ALLOWED"),
            shifts.Count(shift => shift.ChangeKind == ShiftChangeKind.New),
            shifts.Count(shift => shift.ChangeKind == ShiftChangeKind.Modified),
            0,
            shifts.Count(shift => shift.ChangeKind == ShiftChangeKind.Unchanged),
            shifts.SelectMany(shift => shift.Segments)
                .Where(segment => segment.TimeType == TimeType.Overtime)
                .Sum(segment => segment.Minutes));
    }

    private static int Minutes(TimeOnly start, TimeOnly end) =>
        checked((end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute));
}
