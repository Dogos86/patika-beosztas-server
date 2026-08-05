using System.Net;
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

public static class ScheduleEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static IEndpointRouteBuilder MapScheduleEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/schedules")
            .WithTags("Schedules")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageSchedules));
        admin.MapGet("", ListAsync)
            .WithSummary("Beosztástervek listázása")
            .Produces<IReadOnlyList<ScheduleListItemResponse>>()
            .ProducesStandardErrors();
        admin.MapGet("/{scheduleId:guid}", GetAsync)
            .WithSummary("Beosztásterv részletes lekérése")
            .Produces<SchedulePlanResponse>()
            .ProducesStandardErrors();
        admin.MapPost("/{scheduleId:guid}/clone-draft", CloneDraftAsync)
            .RequireAntiforgery()
            .RequireIdempotencyKey()
            .WithSummary("Immutable közzétett beosztás új Draft klónozása")
            .Produces<SchedulePlanResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors(includeConflict: true);
        admin.MapGet("/{scheduleId:guid}/employee-matrix", EmployeeMatrixAsync)
            .WithSummary("Dolgozó × nap beosztási mátrix")
            .Produces<EmployeeScheduleMatrixResponse>()
            .ProducesStandardErrors();
        admin.MapGet("/{scheduleId:guid}/location-coverage", LocationCoverageAsync)
            .WithSummary("Telephelyi 30 perces lefedettségi projekció")
            .Produces<LocationCoverageResponse>()
            .ProducesStandardErrors();
        admin.MapGet("/{scheduleId:guid}/issues", IssuesAsync)
            .WithSummary("Beosztási problémák szűrhető listája")
            .Produces<IReadOnlyList<ScheduleIssueResponse>>()
            .ProducesStandardErrors();
        admin.MapGet("/{scheduleId:guid}/changes", ChangesAsync)
            .WithSummary("Eltérések az alapul vett közzétett beosztáshoz képest")
            .Produces<IReadOnlyList<ScheduleChangeResponse>>()
            .ProducesStandardErrors();
        admin.MapGet(
                "/{scheduleId:guid}/shifts/{shiftId:guid}/explanation",
                ExplanationAsync)
            .WithSummary("Generált műszak strukturált magyarázata")
            .Produces<ShiftExplanationResponse>()
            .ProducesStandardErrors();
        admin.MapGet(
                "/{scheduleId:guid}/shifts/{shiftId:guid}/alternatives",
                AlternativesAsync)
            .WithSummary("Hard-valid alternatív dolgozók és tradeoffok")
            .Produces<IReadOnlyList<ScheduleAlternativeResponse>>()
            .ProducesStandardErrors();
        admin.MapPost("/{scheduleId:guid}/shifts/{shiftId:guid}/lock", LockAsync)
            .RequireAntiforgery()
            .WithSummary("Műszak rögzítése")
            .Produces<ShiftAssignmentResponse>()
            .ProducesStandardErrors(includeConflict: true);
        admin.MapPost("/{scheduleId:guid}/shifts/{shiftId:guid}/unlock", UnlockAsync)
            .RequireAntiforgery()
            .WithSummary("Műszak rögzítésének feloldása")
            .Produces<ShiftAssignmentResponse>()
            .ProducesStandardErrors(includeConflict: true);
        admin.MapPost("/{scheduleId:guid}/shifts/{shiftId:guid}/reject", RejectAsync)
            .RequireAntiforgery()
            .WithSummary("Generált műszakjavaslat elutasítása")
            .Produces<ShiftAssignmentResponse>()
            .ProducesStandardErrors(includeConflict: true);
        admin.MapPost("/{scheduleId:guid}/shifts/{shiftId:guid}/replace", ReplaceAsync)
            .RequireAntiforgery()
            .WithSummary("Műszak hard-valid alternatív dolgozóra cserélése")
            .Produces<ShiftAssignmentResponse>()
            .ProducesStandardErrors(includeConflict: true);
        admin.MapPost("/{scheduleId:guid}/submit-review", SubmitReviewAsync)
            .RequireAntiforgery()
            .WithSummary("Draft beosztás review-ra küldése")
            .Produces<SchedulePlanResponse>()
            .ProducesStandardErrors(includeConflict: true);
        admin.MapPost("/{scheduleId:guid}/return-draft", ReturnDraftAsync)
            .RequireAntiforgery()
            .WithSummary("Review alatt álló beosztás Draft állapotba visszaadása")
            .Produces<SchedulePlanResponse>()
            .ProducesStandardErrors(includeConflict: true);
        admin.MapPost("/{scheduleId:guid}/archive-empty-draft", ArchiveEmptyDraftAsync)
            .RequireAntiforgery()
            .WithSummary("Üres, nem publikált Draft biztonságos archiválása")
            .Produces<SchedulePlanResponse>()
            .ProducesStandardErrors(includeConflict: true);

        endpoints.MapPost(
                "/api/admin/schedules/{scheduleId:guid}/regenerate",
                RegenerateAsync)
            .WithTags("Schedules")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.RunAutoFill))
            .RequireAntiforgery()
            .RequireIdempotencyKey()
            .WithSummary("Teljes vagy részleges tartós újragenerálás")
            .Produces<ScheduleGenerationRunResponse>(StatusCodes.Status202Accepted)
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPost(
                "/api/admin/schedules/{scheduleId:guid}/approve",
                ApproveAsync)
            .WithTags("Schedules")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ApproveSchedules))
            .RequireAntiforgery()
            .WithSummary("Review alatt álló beosztás jóváhagyása")
            .Produces<SchedulePlanResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPost(
                "/api/admin/schedules/{scheduleId:guid}/publish",
                PublishAsync)
            .WithTags("Schedules")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.PublishSchedules))
            .RequireAntiforgery()
            .WithSummary("Jóváhagyott beosztás közzététele")
            .Produces<SchedulePlanResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPost(
                "/api/admin/schedules/{scheduleId:guid}/archive",
                ArchiveAsync)
            .WithTags("Schedules")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.PublishSchedules))
            .RequireAntiforgery()
            .WithSummary("Közzétett beosztás archiválása")
            .Produces<SchedulePlanResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapGet("/api/me/schedule", OwnScheduleAsync)
            .WithTags("Own schedule")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ViewOwnSchedule))
            .WithSummary("Kapcsolt dolgozó kizárólag közzétett beosztása")
            .Produces<OwnScheduleResponse>()
            .ProducesStandardErrors();
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        DateOnly? periodStart,
        DateOnly? periodEnd,
        ScheduleStatus? status,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var query = dbContext.SchedulePlans
            .AsNoTracking()
            .Where(plan => plan.OrganizationId == actor.OrganizationId);
        if (periodStart is not null)
        {
            query = query.Where(plan => plan.PeriodEnd >= periodStart);
        }

        if (periodEnd is not null)
        {
            query = query.Where(plan => plan.PeriodStart <= periodEnd);
        }

        if (status is not null)
        {
            query = query.Where(plan => plan.Status == status);
        }

        var plans = await query
            .OrderByDescending(plan => plan.PeriodStart)
            .ThenByDescending(plan => plan.PublishedRevisionNumber)
            .Select(plan => new ScheduleListItemResponse(
                plan.Id,
                plan.PeriodStart,
                plan.PeriodEnd,
                plan.TimeZoneId,
                plan.Status,
                plan.BasedOnScheduleId,
                plan.PublishedRevisionNumber,
                plan.AlgorithmVersion,
                plan.InputSnapshotHash,
                plan.ShiftAssignments.Count(shift =>
                    shift.ChangeKind != ShiftChangeKind.Deleted),
                plan.Issues.Count(issue =>
                    issue.Severity == ScheduleIssueSeverity.Blocking &&
                    !issue.IsResolved),
                plan.Issues.Count(issue =>
                    issue.Severity == ScheduleIssueSeverity.Warning &&
                    !issue.IsResolved),
                plan.Version,
                plan.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return Results.Ok(plans);
    }

    private static async Task<IResult> GetAsync(
        Guid scheduleId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var plan = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            scheduleId,
            cancellationToken);
        return plan is null
            ? EndpointHelpers.NotFound()
            : Results.Ok(ScheduleMapper.Map(plan));
    }

    private static async Task<IResult> CloneDraftAsync(
        Guid scheduleId,
        CloneScheduleDraftRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var idempotencyKey = IdempotencyKey(httpContext);
        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length is < 8 or > 200)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "IDEMPOTENCY_KEY_REQUIRED",
                    "A 8–200 karakteres Idempotency-Key header kötelező.",
                    "Idempotency-Key")]);
        }

        var keyHash = ScheduleGenerationEndpoints.HashKey(
            actor.OrganizationId,
            idempotencyKey);
        var existingClone = await dbContext.SchedulePlans
            .AsNoTracking()
            .SingleOrDefaultAsync(
                plan =>
                    plan.OrganizationId == actor.OrganizationId &&
                    plan.CloneIdempotencyKeyHash == keyHash,
                cancellationToken);
        if (existingClone is not null)
        {
            var loadedClone = await LoadPlanAsync(
                dbContext,
                actor.OrganizationId,
                existingClone.Id,
                cancellationToken);
            return Results.Ok(ScheduleMapper.Map(loadedClone!));
        }

        var source = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            scheduleId,
            cancellationToken,
            tracking: true);
        if (source is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (source.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A beosztás a lekérés óta megváltozott.");
        }

        if (source.Status is not ScheduleStatus.Published and
            not ScheduleStatus.Archived)
        {
            return BusinessError(
                "SCHEDULE_CLONE_SOURCE_NOT_IMMUTABLE",
                "Csak közzétett vagy archivált beosztásból készíthető Draft klón.");
        }

        var now = timeProvider.GetUtcNow();
        var clone = new SchedulePlan
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            PeriodStart = source.PeriodStart,
            PeriodEnd = source.PeriodEnd,
            TimeZoneId = source.TimeZoneId,
            Status = ScheduleStatus.Draft,
            BasedOnScheduleId = source.Id,
            AlgorithmVersion = source.AlgorithmVersion,
            GenerationOptionsSnapshot = source.GenerationOptionsSnapshot,
            InputSnapshotHash = source.InputSnapshotHash,
            CloneIdempotencyKeyHash = keyHash,
            CreatedByUserId = actor.Id,
            CreatedAtUtc = now,
            UpdatedByUserId = actor.Id,
            UpdatedAtUtc = now
        };
        foreach (var sourceShift in source.ShiftAssignments.Where(shift =>
                     shift.ChangeKind != ShiftChangeKind.Deleted))
        {
            var cloneShift = new ShiftAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                SchedulePlanId = clone.Id,
                EmployeeId = sourceShift.EmployeeId,
                LocationId = sourceShift.LocationId,
                Date = sourceShift.Date,
                StartTime = sourceShift.StartTime,
                EndTime = sourceShift.EndTime,
                Source = sourceShift.Source,
                IsLocked = sourceShift.IsLocked,
                ChangeKind = ShiftChangeKind.Unchanged,
                CreatedByUserId = actor.Id,
                CreatedAtUtc = now,
                UpdatedByUserId = actor.Id,
                UpdatedAtUtc = now
            };
            foreach (var segment in sourceShift.Segments)
            {
                cloneShift.Segments.Add(new ShiftSegment
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = actor.OrganizationId,
                    ShiftAssignmentId = cloneShift.Id,
                    StartTime = segment.StartTime,
                    EndTime = segment.EndTime,
                    TimeType = segment.TimeType
                });
            }

            clone.ShiftAssignments.Add(cloneShift);
        }

        dbContext.SchedulePlans.Add(clone);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "SchedulePlan.ClonedToDraft",
            "SchedulePlan",
            clone.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Draft klón létrehozva; source={source.Id}.");
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
            var duplicate = await dbContext.SchedulePlans
                .AsNoTracking()
                .SingleAsync(
                    plan =>
                        plan.OrganizationId == actor.OrganizationId &&
                        plan.CloneIdempotencyKeyHash == keyHash,
                    cancellationToken);
            var loaded = await LoadPlanAsync(
                dbContext,
                actor.OrganizationId,
                duplicate.Id,
                cancellationToken);
            return Results.Ok(ScheduleMapper.Map(loaded!));
        }

        var saved = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            clone.Id,
            cancellationToken);
        return Results.Created(
            $"/api/admin/schedules/{clone.Id}",
            ScheduleMapper.Map(saved!));
    }

    private static async Task<IResult> EmployeeMatrixAsync(
        Guid scheduleId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var plan = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            scheduleId,
            cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        var employees = await dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.OrganizationId == actor.OrganizationId &&
                employee.IsActive &&
                employee.IsSchedulable)
            .OrderBy(employee => employee.DisplayName)
            .ToArrayAsync(cancellationToken);
        var profiles = await dbContext.EmployeeWorkProfiles
            .AsNoTracking()
            .Where(profile => profile.OrganizationId == actor.OrganizationId)
            .ToDictionaryAsync(
                profile => profile.EmployeeId,
                cancellationToken);
        var leaves = await dbContext.LeaveRequests
            .AsNoTracking()
            .Where(leave =>
                leave.OrganizationId == actor.OrganizationId &&
                leave.DateFrom <= plan.PeriodEnd &&
                (leave.DateTo == null || leave.DateTo >= plan.PeriodStart) &&
                (leave.Status == LeaveRequestStatus.Approved ||
                 leave.Status == LeaveRequestStatus.Pending ||
                 leave.Status == LeaveRequestStatus.Reported ||
                 leave.Status == LeaveRequestStatus.Recorded ||
                 leave.Status == LeaveRequestStatus.Closed))
            .ToArrayAsync(cancellationToken);
        var rows = employees.Select(employee =>
        {
            var employeeShifts = plan.ShiftAssignments
                .Where(shift =>
                    shift.EmployeeId == employee.Id &&
                    shift.ChangeKind != ShiftChangeKind.Deleted)
                .OrderBy(shift => shift.Date)
                .ThenBy(shift => shift.StartTime)
                .ToArray();
            var days = Dates(plan.PeriodStart, plan.PeriodEnd)
                .Select(date => new EmployeeScheduleDayCellResponse(
                    date,
                    employeeShifts
                        .Where(shift => shift.Date == date)
                        .Select(ScheduleMapper.Map)
                        .ToArray(),
                    leaves
                        .Where(leave =>
                            leave.EmployeeId == employee.Id &&
                            leave.DateFrom <= date &&
                            date <= (leave.DateTo ?? plan.PeriodEnd))
                        .Select(leave => new LeaveMarkerResponse(
                            leave.Id,
                            leave.Type,
                            leave.Status,
                            leave.IsFullDay,
                            leave.StartTime,
                            leave.EndTime))
                        .ToArray(),
                    plan.Issues.Count(issue =>
                        issue.EmployeeId == employee.Id &&
                        issue.Date == date &&
                        !issue.IsResolved)))
                .ToArray();
            var assignedMinutes = employeeShifts
                .SelectMany(shift => shift.Segments)
                .Sum(segment => Minutes(segment.StartTime, segment.EndTime));
            var overtime = employeeShifts
                .SelectMany(shift => shift.Segments)
                .Where(segment => segment.TimeType == TimeType.Overtime)
                .Sum(segment => Minutes(segment.StartTime, segment.EndTime));
            var locations = employeeShifts
                .OrderBy(shift => shift.Date)
                .Select(shift => shift.LocationId)
                .ToArray();
            return new EmployeeScheduleRowResponse(
                employee.Id,
                employee.DisplayName,
                days,
                assignedMinutes,
                profiles.GetValueOrDefault(employee.Id)?.ContractedMonthlyMinutes ?? 0,
                profiles.ContainsKey(employee.Id),
                overtime,
                employeeShifts.Count(shift =>
                    shift.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday),
                employeeShifts.Count(shift => shift.StartTime >= new TimeOnly(14, 0)),
                locations.Zip(locations.Skip(1))
                    .Count(pair => pair.First != pair.Second),
                plan.Issues.Count(issue =>
                    issue.EmployeeId == employee.Id &&
                    issue.Severity == ScheduleIssueSeverity.Warning &&
                    !issue.IsResolved));
        }).ToArray();
        return Results.Ok(new EmployeeScheduleMatrixResponse(
            plan.Id,
            plan.PeriodStart,
            plan.PeriodEnd,
            plan.Version,
            rows));
    }

    private static async Task<IResult> LocationCoverageAsync(
        Guid scheduleId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var plan = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            scheduleId,
            cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        var requirements = await dbContext.CoverageRequirements
            .AsNoTracking()
            .Include(requirement => requirement.Location)
            .Where(requirement =>
                requirement.OrganizationId == actor.OrganizationId &&
                requirement.IsActive)
            .ToArrayAsync(cancellationToken);
        var capabilities = await dbContext.EmployeeCapabilities
            .AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId)
            .ToArrayAsync(cancellationToken);
        var employees = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var effectiveCapabilities = employees.ToDictionary(
            item => item.Key,
            item => StaffingCapabilityRules.ResolveEffective(
                capabilities.Where(capability => capability.EmployeeId == item.Key)
                    .Select(capability => capability.Capability),
                item.Value.ProfessionalRole,
                item.Value.CountsAsPharmacist));
        var result = new List<LocationCoverageSlotResponse>();
        foreach (var date in Dates(plan.PeriodStart, plan.PeriodEnd))
        {
            foreach (var group in requirements
                         .Where(requirement =>
                             requirement.DayOfWeek == date.DayOfWeek)
                         .GroupBy(requirement => new
                         {
                             requirement.LocationId,
                             requirement.RequiredCapability,
                             requirement.TimeType
                         }))
            {
                var firstMinute = group.Min(item => ToMinute(item.StartTime));
                var lastMinute = group.Max(item => ToMinute(item.EndTime));
                for (var minute = firstMinute; minute < lastMinute; minute += 30)
                {
                    var applicable = group.Where(item =>
                            ToMinute(item.StartTime) <= minute &&
                            minute < ToMinute(item.EndTime))
                        .ToArray();
                    if (applicable.Length == 0)
                    {
                        continue;
                    }

                    var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute));
                    var end = start.AddMinutes(30);
                    var employeeIds = plan.ShiftAssignments
                        .Where(shift =>
                            shift.ChangeKind != ShiftChangeKind.Deleted &&
                            shift.LocationId == group.Key.LocationId &&
                            shift.Date == date &&
                            effectiveCapabilities
                                .GetValueOrDefault(
                                    shift.EmployeeId,
                                    new HashSet<StaffingCapability>())
                                .Contains(group.Key.RequiredCapability) &&
                            shift.Segments.Any(segment =>
                                segment.TimeType == group.Key.TimeType &&
                                segment.StartTime < end &&
                                start < segment.EndTime))
                        .Select(shift => shift.EmployeeId)
                        .Distinct()
                        .Order()
                        .ToArray();
                    var required = applicable.Max(item => item.RequiredCount);
                    var severity = applicable.Any(item =>
                        item.Severity == CoverageSeverity.Blocking)
                        ? CoverageSeverity.Blocking
                        : CoverageSeverity.Warning;
                    var location = applicable[0].Location;
                    var shortage = Math.Max(0, required - employeeIds.Length);
                    result.Add(new(
                        group.Key.LocationId,
                        location?.Name ?? string.Empty,
                        date,
                        start,
                        end,
                        group.Key.RequiredCapability,
                        group.Key.TimeType,
                        required,
                        employeeIds.Length,
                        shortage,
                        severity,
                        location?.IsActive != true
                            ? "Inactive"
                            : shortage == 0
                                ? "Ok"
                                : severity == CoverageSeverity.Blocking
                                    ? "Blocking"
                                    : "Warning",
                        employeeIds));
                }
            }
        }

        return Results.Ok(new LocationCoverageResponse(
            plan.Id,
            plan.PeriodStart,
            plan.PeriodEnd,
            plan.Version,
            requirements.Length > 0,
            result));
    }

    private static async Task<IResult> IssuesAsync(
        Guid scheduleId,
        string? code,
        ScheduleIssueSeverity? severity,
        DateOnly? date,
        Guid? locationId,
        Guid? employeeId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        if (!await dbContext.SchedulePlans.AnyAsync(
                plan =>
                    plan.Id == scheduleId &&
                    plan.OrganizationId == actor.OrganizationId,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var query = dbContext.ScheduleIssues
            .AsNoTracking()
            .Where(issue =>
                issue.OrganizationId == actor.OrganizationId &&
                issue.SchedulePlanId == scheduleId);
        if (!string.IsNullOrWhiteSpace(code))
        {
            query = query.Where(issue => issue.Code == code);
        }

        if (severity is not null)
        {
            query = query.Where(issue => issue.Severity == severity);
        }

        if (date is not null)
        {
            query = query.Where(issue => issue.Date == date);
        }

        if (locationId is not null)
        {
            query = query.Where(issue => issue.LocationId == locationId);
        }

        if (employeeId is not null)
        {
            query = query.Where(issue => issue.EmployeeId == employeeId);
        }

        var issues = await query
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Date)
            .ThenBy(issue => issue.Code)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(issues.Select(ScheduleMapper.Map).ToArray());
    }

    private static async Task<IResult> ChangesAsync(
        Guid scheduleId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var plan = await dbContext.SchedulePlans
            .AsNoTracking()
            .Include(item => item.ShiftAssignments)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == scheduleId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        var baseShifts = plan.BasedOnScheduleId is null
            ? []
            : await dbContext.ShiftAssignments
                .AsNoTracking()
                .Where(shift =>
                    shift.OrganizationId == actor.OrganizationId &&
                    shift.SchedulePlanId == plan.BasedOnScheduleId &&
                    shift.ChangeKind != ShiftChangeKind.Deleted)
                .ToArrayAsync(cancellationToken);
        var changes = plan.ShiftAssignments
            .Where(shift => shift.ChangeKind != ShiftChangeKind.Deleted)
            .Select(shift =>
            {
                var exact = baseShifts.FirstOrDefault(item => SameShift(item, shift));
                var sameSlot = baseShifts.FirstOrDefault(item => SameSlot(item, shift));
                return new ScheduleChangeResponse(
                    exact is not null
                        ? ShiftChangeKind.Unchanged
                        : sameSlot is not null
                            ? ShiftChangeKind.Modified
                            : ShiftChangeKind.New,
                    shift.Id,
                    exact?.Id ?? sameSlot?.Id,
                    shift.EmployeeId,
                    shift.LocationId,
                    shift.Date,
                    shift.StartTime,
                    shift.EndTime);
            })
            .Concat(baseShifts
                .Where(baseShift => !plan.ShiftAssignments.Any(current =>
                    current.ChangeKind != ShiftChangeKind.Deleted &&
                    SameSlot(baseShift, current)))
                .Select(baseShift => new ScheduleChangeResponse(
                    ShiftChangeKind.Deleted,
                    null,
                    baseShift.Id,
                    baseShift.EmployeeId,
                    baseShift.LocationId,
                    baseShift.Date,
                    baseShift.StartTime,
                    baseShift.EndTime)))
            .OrderBy(change => change.Date)
            .ThenBy(change => change.StartTime)
            .ThenBy(change => change.EmployeeId)
            .ToArray();
        return Results.Ok(changes);
    }

    private static async Task<IResult> ExplanationAsync(
        Guid scheduleId,
        Guid shiftId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var explanation = await dbContext.ShiftExplanations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.OrganizationId == actor.OrganizationId &&
                    item.SchedulePlanId == scheduleId &&
                    item.ShiftAssignmentId == shiftId,
                cancellationToken);
        return explanation is null
            ? EndpointHelpers.NotFound()
            : Results.Ok(ScheduleMapper.Map(explanation));
    }

    private static async Task<IResult> AlternativesAsync(
        Guid scheduleId,
        Guid shiftId,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await ExplanationAsync(
            scheduleId,
            shiftId,
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (result is Microsoft.AspNetCore.Http.HttpResults.Ok<ShiftExplanationResponse> ok &&
            ok.Value is { } explanation)
        {
            return Results.Ok(explanation.Alternatives);
        }

        return result;
    }

    private static Task<IResult> LockAsync(
        Guid scheduleId,
        Guid shiftId,
        ShiftVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        SetLockAsync(
            scheduleId,
            shiftId,
            request,
            true,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> UnlockAsync(
        Guid scheduleId,
        Guid shiftId,
        ShiftVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        SetLockAsync(
            scheduleId,
            shiftId,
            request,
            false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> SetLockAsync(
        Guid scheduleId,
        Guid shiftId,
        ShiftVersionRequest request,
        bool locked,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var pair = await LoadEditableShiftAsync(
            dbContext,
            actor.OrganizationId,
            scheduleId,
            shiftId,
            cancellationToken);
        if (pair is null)
        {
            return EndpointHelpers.NotFound();
        }

        var (plan, shift) = pair.Value;
        var versionError = ValidateVersions(
            plan,
            shift,
            request.ExpectedScheduleVersion,
            request.ExpectedShiftVersion);
        if (versionError is not null)
        {
            return versionError;
        }

        if (plan.Status != ScheduleStatus.Draft)
        {
            return ImmutableOrStateError(plan.Status);
        }

        if (shift.IsLocked == locked)
        {
            return BusinessError(
                locked ? "SHIFT_ALREADY_LOCKED" : "SHIFT_ALREADY_UNLOCKED",
                locked ? "A műszak már rögzített." : "A műszak már nincs rögzítve.");
        }

        var now = timeProvider.GetUtcNow();
        shift.IsLocked = locked;
        shift.UpdatedByUserId = actor.Id;
        shift.UpdatedAtUtc = now;
        plan.UpdatedByUserId = actor.Id;
        plan.UpdatedAtUtc = now;
        dbContext.GeneratedSuggestionDecisions.Add(new GeneratedSuggestionDecision
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            SchedulePlanId = plan.Id,
            ShiftAssignmentId = shift.Id,
            GenerationRunId = shift.GeneratedByRunId,
            DecisionType = locked
                ? GeneratedSuggestionDecisionType.Lock
                : GeneratedSuggestionDecisionType.Unlock,
            ActorUserId = actor.Id,
            OccurredAtUtc = now,
            Reason = request.Reason?.Trim(),
            ExclusionScope = SuggestionExclusionScope.Schedule
        });
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            locked ? "ShiftAssignment.Locked" : "ShiftAssignment.Unlocked",
            "ShiftAssignment",
            shift.Id.ToString(),
            httpContext.TraceIdentifier,
            locked ? "Generált műszak rögzítve." : "Műszak rögzítése feloldva.");
        return await SaveShiftAsync(
            shift,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult> RejectAsync(
        Guid scheduleId,
        Guid shiftId,
        RejectGeneratedSuggestionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var pair = await LoadEditableShiftAsync(
            dbContext,
            actor.OrganizationId,
            scheduleId,
            shiftId,
            cancellationToken);
        if (pair is null)
        {
            return EndpointHelpers.NotFound();
        }

        var (plan, shift) = pair.Value;
        var versionError = ValidateVersions(
            plan,
            shift,
            request.ExpectedScheduleVersion,
            request.ExpectedShiftVersion);
        if (versionError is not null)
        {
            return versionError;
        }

        if (plan.Status != ScheduleStatus.Draft)
        {
            return ImmutableOrStateError(plan.Status);
        }

        if (shift.Source != ShiftAssignmentSource.Generated ||
            shift.ChangeKind == ShiftChangeKind.Deleted)
        {
            return BusinessError(
                "SHIFT_NOT_REJECTABLE_GENERATED_SUGGESTION",
                "Csak aktív generált műszakjavaslat utasítható el.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "REJECTION_REASON_REQUIRED",
                    "Az elutasítás indoka kötelező.",
                    "reason")]);
        }

        var now = timeProvider.GetUtcNow();
        shift.ChangeKind = ShiftChangeKind.Deleted;
        shift.UpdatedByUserId = actor.Id;
        shift.UpdatedAtUtc = now;
        plan.UpdatedByUserId = actor.Id;
        plan.UpdatedAtUtc = now;
        dbContext.GeneratedSuggestionDecisions.Add(new GeneratedSuggestionDecision
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            SchedulePlanId = plan.Id,
            ShiftAssignmentId = shift.Id,
            GenerationRunId = shift.GeneratedByRunId,
            DecisionType = GeneratedSuggestionDecisionType.Reject,
            ActorUserId = actor.Id,
            OccurredAtUtc = now,
            Reason = request.Reason.Trim(),
            ExclusionScope = request.ExclusionScope
        });
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "ShiftAssignment.Rejected",
            "ShiftAssignment",
            shift.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Generált műszak elutasítva; scope={request.ExclusionScope}.");
        return await SaveShiftAsync(
            shift,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult> ReplaceAsync(
        Guid scheduleId,
        Guid shiftId,
        ReplaceShiftRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var pair = await LoadEditableShiftAsync(
            dbContext,
            actor.OrganizationId,
            scheduleId,
            shiftId,
            cancellationToken);
        if (pair is null)
        {
            return EndpointHelpers.NotFound();
        }

        var (plan, shift) = pair.Value;
        var versionError = ValidateVersions(
            plan,
            shift,
            request.ExpectedScheduleVersion,
            request.ExpectedShiftVersion);
        if (versionError is not null)
        {
            return versionError;
        }

        if (plan.Status != ScheduleStatus.Draft)
        {
            return ImmutableOrStateError(plan.Status);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "REPLACEMENT_REASON_REQUIRED",
                    "A csere indoka kötelező.",
                    "reason")]);
        }

        var explanation = await dbContext.ShiftExplanations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.OrganizationId == actor.OrganizationId &&
                    item.SchedulePlanId == scheduleId &&
                    item.ShiftAssignmentId == shiftId,
                cancellationToken);
        var alternatives = explanation is null
            ? []
            : JsonSerializer.Deserialize<ScheduleAlternativeScore[]>(
                explanation.AlternativesJson,
                JsonOptions) ?? [];
        if (!alternatives.Any(item => item.EmployeeId == request.ReplacementEmployeeId))
        {
            return BusinessError(
                "REPLACEMENT_EMPLOYEE_NOT_HARD_VALID",
                "A kiválasztott dolgozó nem hard-valid alternatíva ehhez a műszakhoz.");
        }

        var replacementEmployee = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(
                employee =>
                    employee.Id == request.ReplacementEmployeeId &&
                    employee.OrganizationId == actor.OrganizationId &&
                    employee.IsActive &&
                    employee.IsSchedulable,
                cancellationToken);
        if (replacementEmployee is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (shift.Segments.Any(segment =>
                segment.TimeType is TimeType.Work or TimeType.Overtime))
        {
            var existingDailyAssignments = await dbContext.ShiftAssignments
                .AsNoTracking()
                .Where(item =>
                    item.OrganizationId == actor.OrganizationId &&
                    item.SchedulePlanId == scheduleId &&
                    item.Id != shiftId &&
                    item.EmployeeId == request.ReplacementEmployeeId &&
                    item.ChangeKind != ShiftChangeKind.Deleted &&
                    item.Date == shift.Date &&
                    item.Segments.Any(segment =>
                        segment.TimeType == TimeType.Work ||
                        segment.TimeType == TimeType.Overtime))
                .Select(item => new
                {
                    item.LocationId,
                    item.StartTime,
                    item.EndTime
                })
                .ToArrayAsync(cancellationToken);
            var dailyIssues = SchedulePlanRules.ValidateDailyAssignments(
                existingDailyAssignments
                    .Select(item => (
                        item.LocationId,
                        item.StartTime,
                        item.EndTime))
                    .Append((
                        shift.LocationId,
                        shift.StartTime,
                        shift.EndTime)));
            if (dailyIssues.Count > 0)
            {
                return BusinessError(
                    dailyIssues[0].Code,
                    dailyIssues[0].Message);
            }
        }

        var conflicts = await dbContext.ShiftAssignments
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.OrganizationId == actor.OrganizationId &&
                    item.SchedulePlanId == scheduleId &&
                    item.EmployeeId == request.ReplacementEmployeeId &&
                    item.ChangeKind != ShiftChangeKind.Deleted &&
                    item.Date == shift.Date &&
                    item.StartTime < shift.EndTime &&
                    shift.StartTime < item.EndTime,
                cancellationToken);
        if (conflicts)
        {
            return BusinessError(
                "REPLACEMENT_EMPLOYEE_TIME_CONFLICT",
                "A kiválasztott dolgozónak ütköző műszakja van.");
        }

        var now = timeProvider.GetUtcNow();
        shift.ChangeKind = ShiftChangeKind.Deleted;
        shift.UpdatedByUserId = actor.Id;
        shift.UpdatedAtUtc = now;
        var replacement = new ShiftAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            SchedulePlanId = plan.Id,
            EmployeeId = request.ReplacementEmployeeId,
            LocationId = shift.LocationId,
            Date = shift.Date,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            Source = ShiftAssignmentSource.Replacement,
            IsLocked = false,
            GeneratedByRunId = shift.GeneratedByRunId,
            ReplacesShiftId = shift.Id,
            ChangeKind = ShiftChangeKind.Modified,
            CreatedByUserId = actor.Id,
            CreatedAtUtc = now,
            UpdatedByUserId = actor.Id,
            UpdatedAtUtc = now
        };
        foreach (var segment in shift.Segments)
        {
            replacement.Segments.Add(new ShiftSegment
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                ShiftAssignmentId = replacement.Id,
                StartTime = segment.StartTime,
                EndTime = segment.EndTime,
                TimeType = segment.TimeType
            });
        }

        plan.UpdatedByUserId = actor.Id;
        plan.UpdatedAtUtc = now;
        dbContext.ShiftAssignments.Add(replacement);
        dbContext.GeneratedSuggestionDecisions.Add(new GeneratedSuggestionDecision
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            SchedulePlanId = plan.Id,
            ShiftAssignmentId = shift.Id,
            GenerationRunId = shift.GeneratedByRunId,
            DecisionType = GeneratedSuggestionDecisionType.Replace,
            ActorUserId = actor.Id,
            OccurredAtUtc = now,
            Reason = request.Reason.Trim(),
            ExclusionScope = SuggestionExclusionScope.Schedule
        });
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "ShiftAssignment.Replaced",
            "ShiftAssignment",
            replacement.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Műszak hard-valid alternatívára cserélve; replacedShift={shift.Id}.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A műszak vagy a beosztás mentés közben megváltozott.");
        }

        replacement.Employee = replacementEmployee;
        replacement.Location = shift.Location;
        return Results.Ok(ScheduleMapper.Map(replacement));
    }

    private static Task<IResult> SubmitReviewAsync(
        Guid scheduleId,
        ScheduleVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            scheduleId,
            request.ExpectedVersion,
            ScheduleStatus.UnderReview,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> ReturnDraftAsync(
        Guid scheduleId,
        ScheduleVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            scheduleId,
            request.ExpectedVersion,
            ScheduleStatus.Draft,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> ArchiveEmptyDraftAsync(
        Guid scheduleId,
        ScheduleVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var plan = await dbContext.SchedulePlans
            .Include(item => item.ShiftAssignments)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == scheduleId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (plan.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict("A beosztás a lekérés óta megváltozott.");
        }

        var activeShiftCount = plan.ShiftAssignments.Count(shift =>
            shift.ChangeKind != ShiftChangeKind.Deleted);
        if (plan.Status != ScheduleStatus.Draft || activeShiftCount != 0)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "EMPTY_DRAFT_ARCHIVE_NOT_ALLOWED",
                    "Csak Draft állapotú, nulla műszakos, nem publikált beosztás archiválható ezen a művelettel.",
                    "status")]);
        }

        var now = timeProvider.GetUtcNow();
        plan.Status = ScheduleStatus.Archived;
        plan.ArchivedByUserId = actor.Id;
        plan.ArchivedAtUtc = now;
        plan.UpdatedByUserId = actor.Id;
        plan.UpdatedAtUtc = now;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "SchedulePlan.EmptyDraftArchived",
            "SchedulePlan",
            plan.Id.ToString(),
            httpContext.TraceIdentifier,
            "Üres, nulla műszakos Draft archiválva.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict("A beosztás archiválás közben megváltozott.");
        }

        var saved = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            plan.Id,
            cancellationToken);
        return Results.Ok(ScheduleMapper.Map(saved!));
    }

    private static Task<IResult> ApproveAsync(
        Guid scheduleId,
        ScheduleVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            scheduleId,
            request.ExpectedVersion,
            ScheduleStatus.Approved,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> TransitionAsync(
        Guid scheduleId,
        uint expectedVersion,
        ScheduleStatus target,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var plan = await dbContext.SchedulePlans
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == scheduleId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (plan.Version != expectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A beosztás a lekérés óta megváltozott.");
        }

        var hasBlocking = plan.Issues.Any(issue =>
            issue.Severity == ScheduleIssueSeverity.Blocking &&
            !issue.IsResolved);
        var errors = SchedulePlanRules.ValidateTransition(
            plan.Status,
            target,
            hasBlocking);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors.Select(issue =>
                new ApiValidationError(issue.Code, issue.Message, "status")).ToArray());
        }

        var now = timeProvider.GetUtcNow();
        plan.Status = target;
        plan.UpdatedByUserId = actor.Id;
        plan.UpdatedAtUtc = now;
        switch (target)
        {
            case ScheduleStatus.Draft:
                plan.ReviewRequestedByUserId = null;
                plan.ReviewRequestedAtUtc = null;
                break;
            case ScheduleStatus.UnderReview:
                plan.ReviewRequestedByUserId = actor.Id;
                plan.ReviewRequestedAtUtc = now;
                break;
            case ScheduleStatus.Approved:
                plan.ApprovedByUserId = actor.Id;
                plan.ApprovedAtUtc = now;
                break;
        }

        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            $"SchedulePlan.{target}",
            "SchedulePlan",
            plan.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Beosztás állapota módosítva: {target}.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A beosztás mentés közben megváltozott.");
        }

        var saved = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            plan.Id,
            cancellationToken);
        return Results.Ok(ScheduleMapper.Map(saved!));
    }

    private static async Task<IResult> PublishAsync(
        Guid scheduleId,
        ScheduleVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var plan = await dbContext.SchedulePlans
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == scheduleId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (plan.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A beosztás a lekérés óta megváltozott.");
        }

        var hasBlocking = plan.Issues.Any(issue =>
            issue.Severity == ScheduleIssueSeverity.Blocking &&
            !issue.IsResolved);
        var errors = SchedulePlanRules.ValidateTransition(
            plan.Status,
            ScheduleStatus.Published,
            hasBlocking);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors.Select(issue =>
                new ApiValidationError(issue.Code, issue.Message, "status")).ToArray());
        }

        var now = timeProvider.GetUtcNow();
        var previous = await dbContext.SchedulePlans
            .Where(item =>
                item.OrganizationId == actor.OrganizationId &&
                item.Id != plan.Id &&
                item.PeriodStart == plan.PeriodStart &&
                item.PeriodEnd == plan.PeriodEnd &&
                item.Status == ScheduleStatus.Published)
            .ToArrayAsync(cancellationToken);
        foreach (var oldPlan in previous)
        {
            oldPlan.Status = ScheduleStatus.Archived;
            oldPlan.ArchivedByUserId = actor.Id;
            oldPlan.ArchivedAtUtc = now;
            oldPlan.UpdatedByUserId = actor.Id;
            oldPlan.UpdatedAtUtc = now;
            auditWriter.Add(
                actor.OrganizationId,
                actor.Id,
                "SchedulePlan.ArchivedByNewPublication",
                "SchedulePlan",
                oldPlan.Id.ToString(),
                httpContext.TraceIdentifier,
                $"Korábbi közzétett revízió archiválva; replacement={plan.Id}.");
        }

        var maximumRevision = await dbContext.SchedulePlans
            .Where(item =>
                item.OrganizationId == actor.OrganizationId &&
                item.PeriodStart == plan.PeriodStart &&
                item.PeriodEnd == plan.PeriodEnd)
            .MaxAsync(item => (int?)item.PublishedRevisionNumber, cancellationToken) ?? 0;
        plan.Status = ScheduleStatus.Published;
        plan.PublishedRevisionNumber = maximumRevision + 1;
        plan.PublishedByUserId = actor.Id;
        plan.PublishedAtUtc = now;
        plan.UpdatedByUserId = actor.Id;
        plan.UpdatedAtUtc = now;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "SchedulePlan.Published",
            "SchedulePlan",
            plan.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Beosztás közzétéve; revision={plan.PublishedRevisionNumber}.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A beosztás közzététel közben megváltozott.");
        }

        var saved = await LoadPlanAsync(
            dbContext,
            actor.OrganizationId,
            plan.Id,
            cancellationToken);
        return Results.Ok(ScheduleMapper.Map(saved!));
    }

    private static Task<IResult> ArchiveAsync(
        Guid scheduleId,
        ScheduleVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            scheduleId,
            request.ExpectedVersion,
            ScheduleStatus.Archived,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> RegenerateAsync(
        Guid scheduleId,
        RegenerateScheduleRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var plan = await dbContext.SchedulePlans
            .SingleOrDefaultAsync(
                item =>
                    item.Id == scheduleId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (plan.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A beosztás a lekérés óta megváltozott.");
        }

        if (plan.Status != ScheduleStatus.Draft)
        {
            return ImmutableOrStateError(plan.Status);
        }

        var idempotencyKey = IdempotencyKey(httpContext);
        var errors = ScheduleGenerationEndpoints.Validate(
            plan.PeriodStart,
            plan.PeriodEnd,
            request.MaxSolveSeconds,
            request.WorkerCount,
            request.Weights,
            idempotencyKey);
        errors.AddRange(await ValidateScopeAsync(
            request.Scope,
            plan,
            actor.OrganizationId,
            dbContext,
            cancellationToken));
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var keyHash = ScheduleGenerationEndpoints.HashKey(
            actor.OrganizationId,
            idempotencyKey!);
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

        var scope = new RegenerationScope(
            request.Scope.Type,
            request.Scope.DateFrom,
            request.Scope.DateTo,
            request.Scope.LocationId,
            request.Scope.Capability,
            request.Scope.TimeType,
            request.Scope.IssueIds ?? []);
        var options = ScheduleGenerationEndpoints.MapOptions(
            plan.PeriodStart,
            plan.PeriodEnd,
            request.DeterministicSeed,
            request.MaxSolveSeconds,
            request.WorkerCount,
            request.PendingLeaveHandling,
            request.Weights,
            scope);
        var optionsJson = JsonSerializer.Serialize(options, JsonOptions);
        var now = timeProvider.GetUtcNow();
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
            ScopeConcurrencyKey = ScheduleGenerationEndpoints.ScopeKey(
                plan.PeriodStart,
                plan.PeriodEnd)
        };
        plan.Status = ScheduleStatus.Generating;
        plan.GenerationOptionsSnapshot = optionsJson;
        plan.UpdatedByUserId = actor.Id;
        plan.UpdatedAtUtc = now;
        dbContext.ScheduleGenerationRuns.Add(run);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "ScheduleGeneration.RegenerationQueued",
            "ScheduleGenerationRun",
            run.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Részleges újragenerálás sorba állítva; scope={scope.Type}.");
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
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Már fut generálás erre a beosztási időszakra.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "SCHEDULE_GENERATION_ALREADY_ACTIVE"
                });
        }

        return Results.Accepted(
            $"/api/admin/schedule-generations/{run.Id}",
            ScheduleMapper.Map(run));
    }

    private static async Task<IResult> OwnScheduleAsync(
        DateOnly? periodStart,
        DateOnly? periodEnd,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        if (actor.EmployeeId is null)
        {
            return BusinessError(
                "LINKED_EMPLOYEE_REQUIRED",
                "A saját beosztáshoz kapcsolt dolgozói rekord szükséges.");
        }

        var query = dbContext.SchedulePlans
            .AsNoTracking()
            .Where(plan =>
                plan.OrganizationId == actor.OrganizationId &&
                plan.Status == ScheduleStatus.Published);
        if (periodStart is not null)
        {
            query = query.Where(plan => plan.PeriodEnd >= periodStart);
        }

        if (periodEnd is not null)
        {
            query = query.Where(plan => plan.PeriodStart <= periodEnd);
        }

        var plan = await query
            .OrderByDescending(item => item.PublishedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            return EndpointHelpers.NotFound();
        }

        var shifts = await dbContext.ShiftAssignments
            .AsNoTracking()
            .Include(shift => shift.Location)
            .Include(shift => shift.Segments)
            .Where(shift =>
                shift.OrganizationId == actor.OrganizationId &&
                shift.SchedulePlanId == plan.Id &&
                shift.EmployeeId == actor.EmployeeId &&
                shift.ChangeKind != ShiftChangeKind.Deleted)
            .OrderBy(shift => shift.Date)
            .ThenBy(shift => shift.StartTime)
            .ToArrayAsync(cancellationToken);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "SchedulePlan.OwnPublishedViewed",
            "SchedulePlan",
            plan.Id.ToString(),
            httpContext.TraceIdentifier,
            "Saját közzétett beosztás megtekintve.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new OwnScheduleResponse(
            plan.Id,
            plan.PeriodStart,
            plan.PeriodEnd,
            plan.PublishedRevisionNumber,
            plan.PublishedAtUtc ?? throw new InvalidOperationException(
                "A Published beosztás közzétételi időpontja hiányzik."),
            shifts.Select(shift => new OwnShiftResponse(
                shift.Id,
                shift.LocationId,
                shift.Location?.Name ?? string.Empty,
                shift.Date,
                shift.StartTime,
                shift.EndTime,
                shift.Segments
                    .OrderBy(segment => segment.StartTime)
                    .Select(segment => new ShiftSegmentResponse(
                        segment.Id,
                        segment.StartTime,
                        segment.EndTime,
                        segment.TimeType,
                        Minutes(segment.StartTime, segment.EndTime)))
                    .ToArray())).ToArray()));
    }

    private static async Task<List<ApiValidationError>> ValidateScopeAsync(
        RegenerationScopeRequest scope,
        SchedulePlan plan,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new List<ApiValidationError>();
        bool InPeriod(DateOnly? date) =>
            date is not null &&
            plan.PeriodStart <= date &&
            date <= plan.PeriodEnd;
        switch (scope.Type)
        {
            case RegenerationScopeType.FullPeriod:
                break;
            case RegenerationScopeType.Day:
                if (!InPeriod(scope.DateFrom))
                {
                    errors.Add(new(
                        "REGENERATION_DAY_INVALID",
                        "A nap scope dátuma a beosztási időszakba kell essen.",
                        "scope.dateFrom"));
                }

                break;
            case RegenerationScopeType.DateRange:
                if (!InPeriod(scope.DateFrom) ||
                    !InPeriod(scope.DateTo) ||
                    scope.DateFrom > scope.DateTo)
                {
                    errors.Add(new(
                        "REGENERATION_DATE_RANGE_INVALID",
                        "A dátumtartomány rendezett és a beosztási időszakon belüli legyen.",
                        "scope"));
                }

                break;
            case RegenerationScopeType.Week:
                if (!InPeriod(scope.DateFrom) ||
                    scope.DateFrom?.AddDays(6) > plan.PeriodEnd)
                {
                    errors.Add(new(
                        "REGENERATION_WEEK_INVALID",
                        "A hétscope kezdete és teljes hét napja a beosztási időszakba essen.",
                        "scope.dateFrom"));
                }

                break;
            case RegenerationScopeType.Location:
                if (scope.LocationId is null ||
                    !await dbContext.Locations.AnyAsync(
                        location =>
                            location.Id == scope.LocationId &&
                            location.OrganizationId == organizationId,
                        cancellationToken))
                {
                    errors.Add(new(
                        "REGENERATION_LOCATION_INVALID",
                        "A telephelyscope ismeretlen vagy más szervezethez tartozik.",
                        "scope.locationId"));
                }

                break;
            case RegenerationScopeType.CapabilityAndTimeType:
                if (scope.Capability is null && scope.TimeType is null)
                {
                    errors.Add(new(
                        "REGENERATION_CAPABILITY_OR_TIME_TYPE_REQUIRED",
                        "Capability/time type scope-hoz legalább az egyik érték kötelező.",
                        "scope"));
                }

                break;
            case RegenerationScopeType.Issues:
                var issueIds = scope.IssueIds ?? [];
                var matchingCount = await dbContext.ScheduleIssues.CountAsync(
                    issue =>
                        issue.OrganizationId == organizationId &&
                        issue.SchedulePlanId == plan.Id &&
                        issueIds.Contains(issue.Id),
                    cancellationToken);
                if (issueIds.Count == 0 || matchingCount != issueIds.Distinct().Count())
                {
                    errors.Add(new(
                        "REGENERATION_ISSUES_INVALID",
                        "Az issue scope minden eleme ehhez a beosztáshoz tartozzon.",
                        "scope.issueIds"));
                }

                break;
        }

        return errors;
    }

    private static async Task<IResult> SaveShiftAsync(
        ShiftAssignment shift,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A műszak vagy a beosztás mentés közben megváltozott.");
        }

        shift.Employee ??= await dbContext.Employees
            .AsNoTracking()
            .SingleAsync(
                item =>
                    item.Id == shift.EmployeeId &&
                    item.OrganizationId == shift.OrganizationId,
                cancellationToken);
        shift.Location ??= await dbContext.Locations
            .AsNoTracking()
            .SingleAsync(
                item =>
                    item.Id == shift.LocationId &&
                    item.OrganizationId == shift.OrganizationId,
                cancellationToken);
        return Results.Ok(ScheduleMapper.Map(shift));
    }

    private static async Task<(SchedulePlan Plan, ShiftAssignment Shift)?>
        LoadEditableShiftAsync(
            PatikaDbContext dbContext,
            Guid organizationId,
            Guid scheduleId,
            Guid shiftId,
            CancellationToken cancellationToken)
    {
        var plan = await dbContext.SchedulePlans
            .SingleOrDefaultAsync(
                item =>
                    item.Id == scheduleId &&
                    item.OrganizationId == organizationId,
                cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var shift = await dbContext.ShiftAssignments
            .Include(item => item.Segments)
            .Include(item => item.Employee)
            .Include(item => item.Location)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == shiftId &&
                    item.SchedulePlanId == scheduleId &&
                    item.OrganizationId == organizationId,
                cancellationToken);
        return shift is null ? null : (plan, shift);
    }

    private static IResult? ValidateVersions(
        SchedulePlan plan,
        ShiftAssignment shift,
        uint expectedPlanVersion,
        uint expectedShiftVersion)
    {
        if (plan.Version != expectedPlanVersion ||
            shift.Version != expectedShiftVersion)
        {
            return EndpointHelpers.Conflict(
                "A műszak vagy a beosztás a lekérés óta megváltozott.");
        }

        return null;
    }

    private static IResult ImmutableOrStateError(ScheduleStatus status) =>
        status == ScheduleStatus.Published
            ? BusinessError(
                "PUBLISHED_SCHEDULE_IMMUTABLE",
                "A közzétett beosztás nem módosítható; készítsen új Draft klónt.")
            : BusinessError(
                "SCHEDULE_NOT_EDITABLE",
                "Korlátozott korrekció csak Draft beosztáson végezhető.");

    private static IResult BusinessError(string code, string message) =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "A művelet üzleti szabály miatt nem hajtható végre.",
            detail: message,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private static Task<ApplicationUser?> ActorAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);

    private static Task<SchedulePlan?> LoadPlanAsync(
        PatikaDbContext dbContext,
        Guid organizationId,
        Guid scheduleId,
        CancellationToken cancellationToken,
        bool tracking = false)
    {
        var query = ScheduleMapper.Query(dbContext);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            plan =>
                plan.Id == scheduleId &&
                plan.OrganizationId == organizationId,
            cancellationToken);
    }

    private static IEnumerable<DateOnly> Dates(DateOnly start, DateOnly end)
    {
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static bool SameShift(ShiftAssignment left, ShiftAssignment right) =>
        left.EmployeeId == right.EmployeeId && SameSlot(left, right);

    private static bool SameSlot(ShiftAssignment left, ShiftAssignment right) =>
        left.LocationId == right.LocationId &&
        left.Date == right.Date &&
        left.StartTime == right.StartTime &&
        left.EndTime == right.EndTime;

    private static bool SameSlot(ShiftAssignment left, ScheduleChangeResponse right) =>
        left.LocationId == right.LocationId &&
        left.Date == right.Date &&
        left.StartTime == right.StartTime &&
        left.EndTime == right.EndTime;

    private static int Minutes(TimeOnly start, TimeOnly end) =>
        ToMinute(end) - ToMinute(start);

    private static int ToMinute(TimeOnly time) => time.Hour * 60 + time.Minute;

    private static string? IdempotencyKey(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var value)
            ? WebUtility.HtmlDecode(value.ToString())
            : null;
}
