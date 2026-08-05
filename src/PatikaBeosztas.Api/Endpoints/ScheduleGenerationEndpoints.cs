using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;
using PatikaBeosztas.Infrastructure.Scheduling;

namespace PatikaBeosztas.Api.Endpoints;

public static class ScheduleGenerationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static IEndpointRouteBuilder MapScheduleGenerationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/schedule-generations")
            .WithTags("Schedule generations")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.RunAutoFill));
        group.MapPost("", CreateAsync)
            .RequireAntiforgery()
            .RequireIdempotencyKey()
            .WithSummary("Tartós teljes időszakos beosztásgenerálás indítása")
            .Produces<ScheduleGenerationRunResponse>(StatusCodes.Status202Accepted)
            .ProducesStandardErrors(includeConflict: true);
        group.MapGet("/preflight", PreflightAsync)
            .WithSummary("Beosztásgenerálás előfeltételeinek ellenőrzése")
            .Produces<ScheduleGenerationPreflightResponse>()
            .ProducesStandardErrors();
        group.MapGet("/{runId:guid}", GetAsync)
            .WithSummary("Generálási futás állapotának lekérése")
            .Produces<ScheduleGenerationRunResponse>()
            .ProducesStandardErrors();
        group.MapPost("/{runId:guid}/cancel", CancelAsync)
            .RequireAntiforgery()
            .WithSummary("Várakozó vagy futó generálás megszakításának kérése")
            .Produces<ScheduleGenerationRunResponse>()
            .ProducesStandardErrors(includeConflict: true);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateScheduleGenerationRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        ScheduleInputSnapshotFactory snapshotFactory,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var idempotencyKey = GetIdempotencyKey(httpContext);
        var errors = Validate(
            request.PeriodStart,
            request.PeriodEnd,
            request.MaxSolveSeconds,
            request.WorkerCount,
            request.Weights,
            idempotencyKey);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var keyHash = HashKey(actor.OrganizationId, idempotencyKey!);
        var existing = await dbContext.ScheduleGenerationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                run =>
                    run.OrganizationId == actor.OrganizationId &&
                    run.IdempotencyKeyHash == keyHash,
                cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(ScheduleMapper.Map(existing));
        }

        var preflight = await AnalyzePreflightAsync(
            actor.OrganizationId,
            request.PeriodStart,
            request.PeriodEnd,
            snapshotFactory,
            cancellationToken);
        if (!preflight.CanStart)
        {
            return EndpointHelpers.ValidationProblem(preflight.Issues.Select(issue =>
                new ApiValidationError(issue.Code, issue.Message, "generation")).ToArray());
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(
                item => item.Id == actor.OrganizationId,
                cancellationToken);
        var previousPublished = await dbContext.SchedulePlans
            .AsNoTracking()
            .Where(plan =>
                plan.OrganizationId == actor.OrganizationId &&
                plan.PeriodStart == request.PeriodStart &&
                plan.PeriodEnd == request.PeriodEnd &&
                plan.Status == ScheduleStatus.Published)
            .OrderByDescending(plan => plan.PublishedRevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var defaults = ScheduleGenerationOptions.CreateDefault(
            request.PeriodStart,
            request.PeriodEnd,
            request.DeterministicSeed ?? 1);
        var options = defaults with
        {
            MaxSolveSeconds = request.MaxSolveSeconds ?? defaults.MaxSolveSeconds,
            WorkerCount = request.WorkerCount ?? defaults.WorkerCount,
            PendingLeaveHandling = request.PendingLeaveHandling,
            Weights = MapWeights(request.Weights)
        };
        var optionsJson = JsonSerializer.Serialize(options, JsonOptions);
        var now = timeProvider.GetUtcNow();
        var plan = new SchedulePlan
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            TimeZoneId = organization.TimeZoneId,
            Status = ScheduleStatus.Generating,
            BasedOnScheduleId = previousPublished?.Id,
            AlgorithmVersion = OrToolsScheduleOptimizer.AlgorithmVersion,
            GenerationOptionsSnapshot = optionsJson,
            InputSnapshotHash = string.Empty,
            CreatedByUserId = actor.Id,
            CreatedAtUtc = now,
            UpdatedByUserId = actor.Id,
            UpdatedAtUtc = now
        };
        var run = new ScheduleGenerationRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            SchedulePlanId = plan.Id,
            Status = ScheduleGenerationStatus.Queued,
            RequestedByUserId = actor.Id,
            RequestedAtUtc = now,
            AlgorithmVersion = OrToolsScheduleOptimizer.AlgorithmVersion,
            DeterministicSeed = options.DeterministicSeed,
            OptionsJson = optionsJson,
            InputSnapshotJson = "{}",
            InputSnapshotHash = string.Empty,
            SolverStatus = ScheduleSolverStatus.NotStarted,
            SolverStatisticsJson = "{}",
            IdempotencyKeyHash = keyHash,
            ScopeConcurrencyKey = ScopeKey(request.PeriodStart, request.PeriodEnd)
        };
        dbContext.SchedulePlans.Add(plan);
        dbContext.ScheduleGenerationRuns.Add(run);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "ScheduleGeneration.Queued",
            "ScheduleGenerationRun",
            run.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Generálás sorba állítva; period={request.PeriodStart:yyyy-MM-dd}.." +
            $"{request.PeriodEnd:yyyy-MM-dd}; algorithm={run.AlgorithmVersion}.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            dbContext.ChangeTracker.Clear();
            var duplicate = await dbContext.ScheduleGenerationRuns
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.OrganizationId == actor.OrganizationId &&
                        item.IdempotencyKeyHash == keyHash,
                    cancellationToken);
            if (duplicate is not null)
            {
                return Results.Ok(ScheduleMapper.Map(duplicate));
            }

            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Már fut generálás erre az időszakra.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "SCHEDULE_GENERATION_ALREADY_ACTIVE"
                });
        }

        return Results.Accepted(
            $"/api/admin/schedule-generations/{run.Id}",
            ScheduleMapper.Map(run));
    }

    private static async Task<IResult> PreflightAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        ScheduleInputSnapshotFactory snapshotFactory,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var periodErrors = SchedulePlanRules.ValidatePeriod(periodStart, periodEnd);
        if (periodErrors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(periodErrors.Select(issue =>
                new ApiValidationError(issue.Code, issue.Message, "period")).ToArray());
        }

        var result = await AnalyzePreflightAsync(
            actor.OrganizationId,
            periodStart,
            periodEnd,
            snapshotFactory,
            cancellationToken);
        return Results.Ok(MapPreflight(result));
    }

    private static async Task<ScheduleGenerationPreflightResult> AnalyzePreflightAsync(
        Guid organizationId,
        DateOnly periodStart,
        DateOnly periodEnd,
        ScheduleInputSnapshotFactory snapshotFactory,
        CancellationToken cancellationToken)
    {
        var options = ScheduleGenerationOptions.CreateDefault(periodStart, periodEnd);
        var snapshot = await snapshotFactory.CreateForPreflightAsync(
            organizationId,
            periodStart,
            periodEnd,
            options,
            cancellationToken);
        var snapshotJson = ScheduleSnapshotCanonicalizer.Serialize(snapshot);
        var hash = ScheduleSnapshotCanonicalizer.ComputeHash(snapshotJson);
        var build = ScheduleCandidateBuilder.Build(snapshot, hash);
        return ScheduleGenerationDiagnostics.Analyze(
            snapshot,
            build.OptimizerInput.Candidates.Count);
    }

    private static ScheduleGenerationPreflightResponse MapPreflight(
        ScheduleGenerationPreflightResult result)
    {
        var counts = result.Counts;
        return new(
            result.CanStart,
            new ScheduleGenerationReadinessCountsResponse(
                counts.ActiveLocationCount,
                counts.OpeningIntervalCount,
                counts.ActiveShiftTemplateCount,
                counts.ApplicableShiftTemplateCount,
                counts.CoverageRequirementCount,
                counts.ActiveEmployeeCount,
                counts.SchedulableEmployeeCount,
                counts.AutoFillEmployeeCount,
                counts.LocationAssignedEmployeeCount,
                counts.WorkProfileEmployeeCount,
                counts.CapableEmployeeCount,
                counts.CandidateOptionCount),
            result.Issues.Select(issue => new ScheduleGenerationPreflightIssueResponse(
                issue.Code,
                issue.Severity,
                issue.Message,
                issue.SettingsPath)).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid runId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var run = await dbContext.ScheduleGenerationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == runId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        return run is null
            ? EndpointHelpers.NotFound()
            : Results.Ok(ScheduleMapper.Map(run));
    }

    private static async Task<IResult> CancelAsync(
        Guid runId,
        CancelScheduleGenerationRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var run = await dbContext.ScheduleGenerationRuns
            .Include(item => item.SchedulePlan)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == runId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (run is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (run.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A generálási futás a lekérés óta megváltozott.");
        }

        if (run.Status is not ScheduleGenerationStatus.Queued and
            not ScheduleGenerationStatus.Running)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "SCHEDULE_GENERATION_NOT_CANCELLABLE",
                    "Csak várakozó vagy futó generálás szakítható meg.",
                    "status")]);
        }

        var now = timeProvider.GetUtcNow();
        run.CancellationRequestedAtUtc = now;
        if (run.Status == ScheduleGenerationStatus.Queued)
        {
            run.Status = ScheduleGenerationStatus.Cancelled;
            run.SolverStatus = ScheduleSolverStatus.Cancelled;
            run.CompletedAtUtc = now;
            run.ErrorCode = "GENERATION_CANCELLED";
            run.RedactedError = "A várakozó generálást felhasználói kérés megszakította.";
            if (run.SchedulePlan is { Status: ScheduleStatus.Generating } plan)
            {
                plan.Status = ScheduleStatus.Draft;
                plan.UpdatedByUserId = actor.Id;
                plan.UpdatedAtUtc = now;
            }
        }

        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "ScheduleGeneration.CancellationRequested",
            "ScheduleGenerationRun",
            run.Id.ToString(),
            httpContext.TraceIdentifier,
            "Generálás megszakítása kérve.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A generálási futás mentés közben megváltozott.");
        }

        return Results.Ok(ScheduleMapper.Map(run));
    }

    internal static ScheduleGenerationOptions MapOptions(
        DateOnly periodStart,
        DateOnly periodEnd,
        int? deterministicSeed,
        int? maxSolveSeconds,
        int? workerCount,
        PendingLeaveHandlingMode pendingLeaveHandling,
        ScheduleGenerationWeightsRequest? weights,
        RegenerationScope scope)
    {
        var defaults = ScheduleGenerationOptions.CreateDefault(
            periodStart,
            periodEnd,
            deterministicSeed ?? 1);
        return defaults with
        {
            MaxSolveSeconds = maxSolveSeconds ?? defaults.MaxSolveSeconds,
            WorkerCount = workerCount ?? defaults.WorkerCount,
            PendingLeaveHandling = pendingLeaveHandling,
            Weights = MapWeights(weights),
            Scope = scope
        };
    }

    internal static List<ApiValidationError> Validate(
        DateOnly periodStart,
        DateOnly periodEnd,
        int? maxSolveSeconds,
        int? workerCount,
        ScheduleGenerationWeightsRequest? weights,
        string? idempotencyKey)
    {
        var errors = SchedulePlanRules.ValidatePeriod(periodStart, periodEnd)
            .Select(issue => new ApiValidationError(issue.Code, issue.Message, "period"))
            .ToList();
        if (maxSolveSeconds is not null and (< 1 or > 60))
        {
            errors.Add(new(
                "MAX_SOLVE_SECONDS_INVALID",
                "A megoldási időkorlát 1 és 60 másodperc között lehet.",
                "maxSolveSeconds"));
        }

        if (workerCount is not null and (< 1 or > 16))
        {
            errors.Add(new(
                "SOLVER_WORKER_COUNT_INVALID",
                "A solver workerszáma 1 és 16 között lehet.",
                "workerCount"));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length is < 8 or > 200)
        {
            errors.Add(new(
                "IDEMPOTENCY_KEY_REQUIRED",
                "A 8–200 karakteres Idempotency-Key header kötelező.",
                "Idempotency-Key"));
        }

        if (weights is not null &&
            WeightValues(weights).Any(value => value is < 0 or > 1_000_000))
        {
            errors.Add(new(
                "SCHEDULE_WEIGHT_INVALID",
                "A generálási súlyok 0 és 1 000 000 közötti egész számok lehetnek.",
                "weights"));
        }

        return errors;
    }

    internal static string HashKey(Guid organizationId, string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{organizationId:N}:{key.Trim()}"));
        return Convert.ToHexStringLower(hash);
    }

    internal static string ScopeKey(DateOnly start, DateOnly end) =>
        $"{start:yyyyMMdd}-{end:yyyyMMdd}";

    private static string? GetIdempotencyKey(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var values)
            ? values.ToString()
            : null;

    private static ScheduleOptimizationWeights MapWeights(
        ScheduleGenerationWeightsRequest? request)
    {
        var defaults = ScheduleOptimizationWeights.Default;
        return request is null
            ? defaults
            : new(
                request.BlockingShortage ?? defaults.BlockingShortage,
                request.WarningShortage ?? defaults.WarningShortage,
                request.PreferredWindowMatch ?? defaults.PreferredWindowMatch,
                request.AvoidWindowViolation ?? defaults.AvoidWindowViolation,
                request.TargetHoursDeviation ?? defaults.TargetHoursDeviation,
                request.Overtime ?? defaults.Overtime,
                request.WeekendFairness ?? defaults.WeekendFairness,
                request.EveningFairness ?? defaults.EveningFairness,
                request.LocationChange ?? defaults.LocationChange,
                request.QuotaTarget ?? defaults.QuotaTarget,
                request.LongShiftPreference ?? defaults.LongShiftPreference,
                request.PendingLeaveOverlap ?? defaults.PendingLeaveOverlap,
                request.PreviousScheduleChange ?? defaults.PreviousScheduleChange,
                request.PreserveAcceptedDecision ?? defaults.PreserveAcceptedDecision);
    }

    private static IEnumerable<int?> WeightValues(
        ScheduleGenerationWeightsRequest weights)
    {
        yield return weights.BlockingShortage;
        yield return weights.WarningShortage;
        yield return weights.PreferredWindowMatch;
        yield return weights.AvoidWindowViolation;
        yield return weights.TargetHoursDeviation;
        yield return weights.Overtime;
        yield return weights.WeekendFairness;
        yield return weights.EveningFairness;
        yield return weights.LocationChange;
        yield return weights.QuotaTarget;
        yield return weights.LongShiftPreference;
        yield return weights.PendingLeaveOverlap;
        yield return weights.PreviousScheduleChange;
        yield return weights.PreserveAcceptedDecision;
    }
}
