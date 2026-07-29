using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Infrastructure.Scheduling;

public sealed class ScheduleGenerationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduleGenerationBackgroundService> logger,
    TimeProvider timeProvider)
    : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogWorkerFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3101, "ScheduleGenerationWorkerFailure"),
            "A beosztásgeneráló háttérfeldolgozó ciklusa sikertelen.");

    private static readonly Action<ILogger, int, Exception?> LogRecoveredRuns =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(3102, "ScheduleGenerationRunsRecovered"),
            "{RecoveredRunCount} megszakadt generálási futás Failed állapotba került.");

    private static readonly Action<ILogger, Guid, Exception?> LogRunFailure =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(3103, "ScheduleGenerationRunFailure"),
            "A(z) {RunId} beosztásgenerálási futás feldolgozása sikertelen.");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var recoveryCompleted = false;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200), timeProvider);
        do
        {
            try
            {
                if (!recoveryCompleted)
                {
                    await RecoverInterruptedRunsAsync(stoppingToken);
                    recoveryCompleted = true;
                }

                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (PostgresException exception) when (exception.SqlState == "42P01")
            {
                // The integration-test database can be recreated while the host is alive.
            }
            catch (Exception exception)
            {
                LogWorkerFailure(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RecoverInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            var now = timeProvider.GetUtcNow();
            var interruptedPlanIds = await dbContext.ScheduleGenerationRuns
                .AsNoTracking()
                .Where(run => run.Status == ScheduleGenerationStatus.Running)
                .Select(run => run.SchedulePlanId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
            var recovered = await dbContext.ScheduleGenerationRuns
                .Where(run => run.Status == ScheduleGenerationStatus.Running)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.Status, ScheduleGenerationStatus.Failed)
                        .SetProperty(run => run.SolverStatus, ScheduleSolverStatus.Failed)
                        .SetProperty(run => run.ErrorCode, "RECOVERED_AFTER_RESTART")
                        .SetProperty(
                            run => run.RedactedError,
                            "A futás szolgáltatás-újraindítás közben megszakadt.")
                        .SetProperty(run => run.CompletedAtUtc, now),
                    cancellationToken);
            if (interruptedPlanIds.Length > 0)
            {
                await dbContext.SchedulePlans
                    .Where(plan =>
                        interruptedPlanIds.Contains(plan.Id) &&
                        plan.Status == ScheduleStatus.Generating)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(plan => plan.Status, ScheduleStatus.Draft)
                            .SetProperty(plan => plan.UpdatedAtUtc, now),
                        cancellationToken);
            }

            if (recovered > 0)
            {
                LogRecoveredRuns(logger, recovered, null);
            }
        }
        catch (PostgresException exception) when (exception.SqlState == "42P01")
        {
            // Let the outer loop retry recovery after the first migration.
            throw;
        }
    }

    private async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        Guid? runId;
        await using (var claimScope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = claimScope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            runId = await dbContext.ScheduleGenerationRuns
                .AsNoTracking()
                .Where(run => run.Status == ScheduleGenerationStatus.Queued)
                .OrderBy(run => run.RequestedAtUtc)
                .Select(run => (Guid?)run.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (runId is null)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            var claimed = await dbContext.ScheduleGenerationRuns
                .Where(run =>
                    run.Id == runId &&
                    run.Status == ScheduleGenerationStatus.Queued)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(run => run.Status, ScheduleGenerationStatus.Running)
                        .SetProperty(run => run.StartedAtUtc, now)
                        .SetProperty(run => run.SolverStatus, ScheduleSolverStatus.NotStarted),
                    cancellationToken);
            if (claimed == 0)
            {
                return;
            }
        }

        try
        {
            await ProcessClaimedAsync(runId.Value, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException and
            not StackOverflowException)
        {
            LogRunFailure(logger, runId.Value, exception);
            await MarkFailedAsync(
                runId.Value,
                "GENERATION_PROCESSING_FAILED",
                $"A generálás sikertelen ({exception.GetType().Name}).",
                cancellationToken);
        }
    }

    private async Task ProcessClaimedAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        ScheduleGenerationOptions options;
        ScheduleCandidateBuildResult build;
        IScheduleOptimizer optimizer;
        await using (var snapshotScope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = snapshotScope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            var run = await dbContext.ScheduleGenerationRuns
                .Include(item => item.SchedulePlan)
                .SingleAsync(item => item.Id == runId, cancellationToken);
            options = JsonSerializer.Deserialize<ScheduleGenerationOptions>(
                run.OptionsJson,
                JsonOptions) ?? throw new InvalidOperationException(
                "A generálási opciók snapshotja nem olvasható.");
            var factory =
                snapshotScope.ServiceProvider.GetRequiredService<ScheduleInputSnapshotFactory>();
            var snapshot = await factory.CreateAsync(
                run.SchedulePlanId,
                options,
                cancellationToken);
            var snapshotJson = ScheduleSnapshotCanonicalizer.Serialize(snapshot);
            var hash = ScheduleSnapshotCanonicalizer.ComputeHash(snapshotJson);
            build = ScheduleCandidateBuilder.Build(snapshot, hash);
            run.InputSnapshotJson = snapshotJson;
            run.InputSnapshotHash = hash;
            run.SchedulePlan!.GenerationOptionsSnapshot = run.OptionsJson;
            run.SchedulePlan.InputSnapshotHash = hash;
            run.SchedulePlan.AlgorithmVersion = run.AlgorithmVersion;
            run.SchedulePlan.UpdatedByUserId = run.RequestedByUserId;
            run.SchedulePlan.UpdatedAtUtc = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            optimizer = snapshotScope.ServiceProvider.GetRequiredService<IScheduleOptimizer>();
        }

        ScheduleOptimizationResult result;
        try
        {
            result = await OptimizeWithCancellationPollingAsync(
                runId,
                optimizer,
                build.OptimizerInput,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException and
            not StackOverflowException)
        {
            await MarkFailedAsync(
                runId,
                "GENERATION_PROCESSING_FAILED",
                $"A generálás sikertelen ({exception.GetType().Name}).",
                cancellationToken);
            return;
        }

        await PersistResultAsync(
            runId,
            build.InputIssues,
            result,
            cancellationToken);
    }

    private async Task<ScheduleOptimizationResult>
        OptimizeWithCancellationPollingAsync(
            Guid runId,
            IScheduleOptimizer optimizer,
            ScheduleOptimizerInput input,
            CancellationToken stoppingToken)
    {
        using var solverCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var optimization = optimizer.OptimizeAsync(
            input,
            solverCancellation.Token);
        while (!optimization.IsCompleted)
        {
            var completed = await Task.WhenAny(
                optimization,
                Task.Delay(
                    TimeSpan.FromMilliseconds(200),
                    stoppingToken));
            if (completed == optimization)
            {
                break;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext =
                scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            var cancellationRequested = await dbContext.ScheduleGenerationRuns
                .AsNoTracking()
                .AnyAsync(
                    run =>
                        run.Id == runId &&
                        (run.CancellationRequestedAtUtc != null ||
                         run.Status == ScheduleGenerationStatus.Cancelled),
                    stoppingToken);
            if (cancellationRequested)
            {
                await solverCancellation.CancelAsync();
                break;
            }
        }

        return await optimization;
    }

    private async Task PersistResultAsync(
        Guid runId,
        IReadOnlyList<ScheduleOptimizationIssue> inputIssues,
        ScheduleOptimizationResult result,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var auditWriter = scope.ServiceProvider.GetRequiredService<AuditWriter>();
        var run = await dbContext.ScheduleGenerationRuns
            .Include(item => item.SchedulePlan)
            .SingleAsync(item => item.Id == runId, cancellationToken);
        if (run.CancellationRequestedAtUtc is not null ||
            run.Status == ScheduleGenerationStatus.Cancelled)
        {
            run.Status = ScheduleGenerationStatus.Cancelled;
            run.SolverStatus = ScheduleSolverStatus.Cancelled;
            run.CompletedAtUtc = timeProvider.GetUtcNow();
            run.ErrorCode = "GENERATION_CANCELLED";
            run.RedactedError = "A generálást felhasználói kérés megszakította.";
            if (run.SchedulePlan is { Status: ScheduleStatus.Generating } cancelledPlan)
            {
                cancelledPlan.Status = ScheduleStatus.Draft;
                cancelledPlan.UpdatedAtUtc = run.CompletedAtUtc.Value;
            }

            auditWriter.Add(
                run.OrganizationId,
                run.RequestedByUserId,
                "ScheduleGeneration.Cancelled",
                "ScheduleGenerationRun",
                run.Id.ToString(),
                run.Id.ToString(),
                "A generálási futás megszakítva.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        run.SolverStatus = result.Status;
        run.SolverStatisticsJson = JsonSerializer.Serialize(
            result.Statistics,
            JsonOptions);
        run.ObjectiveValue = result.ObjectiveValue;
        run.ErrorCode = result.ErrorCode;
        run.RedactedError = result.RedactedError;
        run.CompletedAtUtc = timeProvider.GetUtcNow();
        if (!result.IsAccepted)
        {
            run.Status = ScheduleGenerationStatus.Failed;
            if (run.SchedulePlan is { Status: ScheduleStatus.Generating } failedPlan)
            {
                failedPlan.Status = ScheduleStatus.Draft;
                failedPlan.UpdatedAtUtc = run.CompletedAtUtc.Value;
            }

            auditWriter.Add(
                run.OrganizationId,
                run.RequestedByUserId,
                "ScheduleGeneration.Failed",
                "ScheduleGenerationRun",
                run.Id.ToString(),
                run.Id.ToString(),
                $"Generálás sikertelen; solver status: {result.Status}.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var plan = run.SchedulePlan ?? throw new InvalidOperationException(
            "A generálási futáshoz nem tartozik beosztás.");
        var existing = await dbContext.ShiftAssignments
            .Include(shift => shift.Segments)
            .Include(shift => shift.Explanation)
            .Where(shift =>
                shift.OrganizationId == run.OrganizationId &&
                shift.SchedulePlanId == run.SchedulePlanId)
            .ToArrayAsync(cancellationToken);
        var existingById = existing.ToDictionary(shift => shift.Id);
        var selectedExistingIds = result.Assignments
            .Select(item => item.Candidate.ExistingShiftId)
            .Where(id => id is not null && existingById.ContainsKey(id.Value))
            .Select(id => id!.Value)
            .ToHashSet();
        foreach (var superseded in existing.Where(shift =>
                     !selectedExistingIds.Contains(shift.Id) &&
                     shift.ChangeKind != ShiftChangeKind.Deleted))
        {
            superseded.ChangeKind = ShiftChangeKind.Deleted;
            superseded.UpdatedByUserId = run.RequestedByUserId;
            superseded.UpdatedAtUtc = run.CompletedAtUtc.Value;
        }

        dbContext.ScheduleIssues.RemoveRange(await dbContext.ScheduleIssues
            .Where(issue =>
                issue.OrganizationId == run.OrganizationId &&
                issue.SchedulePlanId == run.SchedulePlanId)
            .ToArrayAsync(cancellationToken));

        var baseShifts = plan.BasedOnScheduleId is null
            ? []
            : await dbContext.ShiftAssignments
                .AsNoTracking()
                .Where(shift =>
                    shift.OrganizationId == run.OrganizationId &&
                    shift.SchedulePlanId == plan.BasedOnScheduleId)
                .ToArrayAsync(cancellationToken);
        foreach (var selected in result.Assignments)
        {
            var candidate = selected.Candidate;
            ShiftAssignment assignment;
            if (candidate.ExistingShiftId is not null &&
                existingById.TryGetValue(candidate.ExistingShiftId.Value, out var current))
            {
                assignment = current;
                if (assignment.Explanation is not null)
                {
                    dbContext.ShiftExplanations.Remove(assignment.Explanation);
                }
            }
            else
            {
                assignment = new ShiftAssignment
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = run.OrganizationId,
                    SchedulePlanId = run.SchedulePlanId,
                    EmployeeId = candidate.EmployeeId,
                    LocationId = candidate.LocationId,
                    Date = candidate.Date,
                    StartTime = candidate.StartTime,
                    EndTime = candidate.EndTime,
                    Source = ShiftAssignmentSource.Generated,
                    GeneratedByRunId = run.Id,
                    ChangeKind = GetChangeKind(candidate, baseShifts),
                    CreatedByUserId = run.RequestedByUserId,
                    CreatedAtUtc = run.CompletedAtUtc.Value,
                    UpdatedByUserId = run.RequestedByUserId,
                    UpdatedAtUtc = run.CompletedAtUtc.Value
                };
                foreach (var segment in candidate.Segments)
                {
                    assignment.Segments.Add(new ShiftSegment
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = run.OrganizationId,
                        ShiftAssignmentId = assignment.Id,
                        StartTime = segment.StartTime,
                        EndTime = segment.EndTime,
                        TimeType = segment.TimeType
                    });
                }

                dbContext.ShiftAssignments.Add(assignment);
            }

            dbContext.ShiftExplanations.Add(new ShiftExplanation
            {
                Id = Guid.NewGuid(),
                OrganizationId = run.OrganizationId,
                SchedulePlanId = run.SchedulePlanId,
                ShiftAssignmentId = assignment.Id,
                GenerationRunId = run.Id,
                AlgorithmVersion = run.AlgorithmVersion,
                ReasonCodesJson = JsonSerializer.Serialize(
                    selected.ReasonCodes,
                    JsonOptions),
                ScoreComponentsJson = JsonSerializer.Serialize(
                    selected.ScoreComponents,
                    JsonOptions),
                AlternativesJson = JsonSerializer.Serialize(
                    selected.Alternatives,
                    JsonOptions)
            });
        }

        var allIssues = inputIssues.Concat(result.Issues).ToArray();
        dbContext.ScheduleIssues.AddRange(allIssues.Select(issue =>
            new ScheduleIssue
            {
                Id = Guid.NewGuid(),
                OrganizationId = run.OrganizationId,
                SchedulePlanId = run.SchedulePlanId,
                GenerationRunId = run.Id,
                Code = issue.Code,
                Severity = issue.Severity,
                EmployeeId = issue.EmployeeId,
                LocationId = issue.LocationId,
                Date = issue.Date,
                StartTime = issue.StartTime,
                EndTime = issue.EndTime,
                ParametersJson = JsonSerializer.Serialize(issue.Parameters, JsonOptions)
            }));
        run.Status = ScheduleGenerationStatus.Succeeded;
        plan.Status = ScheduleStatus.Draft;
        plan.UpdatedByUserId = run.RequestedByUserId;
        plan.UpdatedAtUtc = run.CompletedAtUtc.Value;
        auditWriter.Add(
            run.OrganizationId,
            run.RequestedByUserId,
            "ScheduleGeneration.Succeeded",
            "ScheduleGenerationRun",
            run.Id.ToString(),
            run.Id.ToString(),
            $"Generálás kész; status={result.Status}; assignments={result.Assignments.Count}; " +
            $"blockingIssues={allIssues.Count(issue => issue.Severity == ScheduleIssueSeverity.Blocking)}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(
        Guid runId,
        string errorCode,
        string redactedError,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var auditWriter = scope.ServiceProvider.GetRequiredService<AuditWriter>();
        var run = await dbContext.ScheduleGenerationRuns
            .Include(item => item.SchedulePlan)
            .SingleAsync(
            item => item.Id == runId,
            cancellationToken);
        run.Status = ScheduleGenerationStatus.Failed;
        run.SolverStatus = ScheduleSolverStatus.Failed;
        run.ErrorCode = errorCode;
        run.RedactedError = redactedError;
        run.CompletedAtUtc = timeProvider.GetUtcNow();
        if (run.SchedulePlan is { Status: ScheduleStatus.Generating } failedPlan)
        {
            failedPlan.Status = ScheduleStatus.Draft;
            failedPlan.UpdatedAtUtc = run.CompletedAtUtc.Value;
        }

        auditWriter.Add(
            run.OrganizationId,
            run.RequestedByUserId,
            "ScheduleGeneration.Failed",
            "ScheduleGenerationRun",
            run.Id.ToString(),
            run.Id.ToString(),
            $"Generálás sikertelen; code={errorCode}.");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ShiftChangeKind GetChangeKind(
        ScheduleCandidateOption candidate,
        IReadOnlyCollection<ShiftAssignment> baseShifts)
    {
        if (baseShifts.Any(shift =>
                shift.EmployeeId == candidate.EmployeeId &&
                shift.LocationId == candidate.LocationId &&
                shift.Date == candidate.Date &&
                shift.StartTime == candidate.StartTime &&
                shift.EndTime == candidate.EndTime))
        {
            return ShiftChangeKind.Unchanged;
        }

        return baseShifts.Any(shift =>
            shift.LocationId == candidate.LocationId &&
            shift.Date == candidate.Date &&
            shift.StartTime == candidate.StartTime &&
            shift.EndTime == candidate.EndTime)
            ? ShiftChangeKind.Modified
            : ShiftChangeKind.New;
    }
}
