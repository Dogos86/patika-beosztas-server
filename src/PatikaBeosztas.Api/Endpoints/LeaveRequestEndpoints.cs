using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class LeaveRequestEndpoints
{
    public static IEndpointRouteBuilder MapLeaveRequestEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var self = endpoints.MapGroup("/api/me/leave-requests")
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageOwnLeaveRequests));
        self.MapGet("", ListOwnAsync)
            .WithSummary("Saját távolléti kérelmek és betegállományok listázása")
            .Produces<IReadOnlyList<LeaveRequestResponse>>()
            .ProducesStandardErrors();
        self.MapPost("", CreateOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját távolléti kérelem vagy betegállomány létrehozása")
            .Produces<LeaveRequestResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();
        self.MapPut("/{id:guid}", UpdateOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját szerkeszthető távollét módosítása")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);
        self.MapPost("/{id:guid}/submit", SubmitOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját távolléti kérelem beküldése")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);
        self.MapPost("/{id:guid}/withdraw", WithdrawOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját távolléti kérelem visszavonása")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);

        var adminRead = endpoints.MapGroup("/api/admin/leave-requests")
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageAllLeaveRequests));
        adminRead.MapGet("", ListAllAsync)
            .WithSummary("Szervezeti távolléti kérelmek listázása")
            .Produces<IReadOnlyList<LeaveRequestResponse>>()
            .ProducesStandardErrors();
        adminRead.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Távolléti kérelem részletes lekérése")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors();

        var adminCreate = endpoints.MapGroup(
                "/api/admin/employees/{employeeId:guid}/leave-requests")
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.RecordLeaveForOthers));
        adminCreate.MapPost("", CreateForEmployeeAsync)
            .RequireAntiforgery()
            .WithSummary("Távollét rögzítése dolgozó nevében")
            .Produces<LeaveRequestResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();

        endpoints.MapPost("/api/admin/leave-requests/{id:guid}/submit", SubmitForEmployeeAsync)
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.RecordLeaveForOthers))
            .RequireAntiforgery()
            .WithSummary("Admin által rögzített normál kérelem beküldése")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPost("/api/admin/leave-requests/{id:guid}/record", RecordSickLeaveAsync)
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.RecordLeaveForOthers))
            .RequireAntiforgery()
            .WithSummary("Bejelentett betegállomány adminisztratív rögzítése")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPost("/api/admin/leave-requests/{id:guid}/close", CloseSickLeaveAsync)
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.RecordLeaveForOthers))
            .RequireAntiforgery()
            .WithSummary("Rögzített betegállomány lezárása")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPost("/api/admin/leave-requests/{id:guid}/decision", DecideAsync)
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ApproveLeaveRequests))
            .RequireAntiforgery()
            .WithSummary("Függő távolléti kérelem jóváhagyása vagy elutasítása")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);
        endpoints.MapPost("/api/admin/leave-requests/{id:guid}/cancel", CancelAsync)
            .WithTags("Leave requests")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ApproveLeaveRequests))
            .RequireAntiforgery()
            .WithSummary("Jóváhagyott távollét megszüntetése")
            .Produces<LeaveRequestResponse>()
            .ProducesStandardErrors(includeConflict: true);

        return endpoints;
    }

    private static async Task<IResult> ListOwnAsync(
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

        if (actor.EmployeeId is null)
        {
            return EmployeeLinkRequired();
        }

        var requests = await Query(dbContext)
            .Where(request =>
                request.OrganizationId == actor.OrganizationId &&
                request.EmployeeId == actor.EmployeeId)
            .OrderByDescending(request => request.DateFrom)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(requests.Select(Map).ToArray());
    }

    private static async Task<IResult> ListAllAsync(
        Guid? employeeId,
        LeaveRequestStatus? status,
        LeaveType? type,
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

        if (employeeId is not null &&
            !await EmployeeExistsAsync(
                employeeId.Value,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var query = Query(dbContext)
            .Where(request => request.OrganizationId == actor.OrganizationId);
        if (employeeId is not null)
        {
            query = query.Where(request => request.EmployeeId == employeeId);
        }

        if (status is not null)
        {
            query = query.Where(request => request.Status == status);
        }

        if (type is not null)
        {
            query = query.Where(request => request.Type == type);
        }

        var requests = await query
            .OrderByDescending(request => request.DateFrom)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(requests.Select(Map).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid id,
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

        var request = await Query(dbContext)
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        return request is null
            ? EndpointHelpers.NotFound()
            : Results.Ok(Map(request));
    }

    private static async Task<IResult> CreateOwnAsync(
        CreateLeaveRequest request,
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

        return actor.EmployeeId is null
            ? EmployeeLinkRequired()
            : await CreateAsync(
                actor.EmployeeId.Value,
                request,
                actor,
                httpContext,
                dbContext,
                auditWriter,
                timeProvider,
                cancellationToken);
    }

    private static async Task<IResult> CreateForEmployeeAsync(
        Guid employeeId,
        CreateLeaveRequest request,
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

        if (!await EmployeeExistsAsync(
            employeeId,
            actor.OrganizationId,
            dbContext,
            cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        return await CreateAsync(
            employeeId,
            request,
            actor,
            httpContext,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);
    }

    private static async Task<IResult> UpdateOwnAsync(
        Guid id,
        UpdateLeaveRequest request,
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

        if (actor.EmployeeId is null)
        {
            return EmployeeLinkRequired();
        }

        var leave = await Query(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId &&
                    item.EmployeeId == actor.EmployeeId,
                cancellationToken);
        if (leave is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (!LeaveRequestRules.CanEdit(leave.Type, leave.Status))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "LEAVE_REQUEST_NOT_EDITABLE",
                    "A távollét a jelenlegi állapotában nem módosítható.",
                    "status")]);
        }

        if (leave.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A távollét a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        var errors = InputValidation.ValidateLeaveRequest(
            leave.Type,
            request.DateFrom,
            request.DateTo,
            request.IsFullDay,
            request.StartTime,
            request.EndTime,
            request.EmployeeNote);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        leave.DateFrom = request.DateFrom;
        leave.DateTo = request.DateTo;
        leave.IsFullDay = request.IsFullDay;
        leave.StartTime = request.StartTime;
        leave.EndTime = request.EndTime;
        leave.EmployeeNote = NormalizeOptional(request.EmployeeNote);
        leave.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "LeaveRequest.Updated",
            "LeaveRequest",
            leave.Id.ToString(),
            httpContext.TraceIdentifier,
            "Saját távolléti adatok módosítva.");

        return await SaveAndMapAsync(
            leave,
            dbContext,
            "A távollét mentés közben megváltozott. Töltse újra az adatokat.",
            cancellationToken);
    }

    private static Task<IResult> SubmitOwnAsync(
        Guid id,
        LeaveVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            id,
            request.ExpectedVersion,
            LeaveRequestStatus.Pending,
            reason: null,
            dateTo: null,
            selfOnly: true,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> WithdrawOwnAsync(
        Guid id,
        LeaveVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            id,
            request.ExpectedVersion,
            LeaveRequestStatus.Withdrawn,
            reason: null,
            dateTo: null,
            selfOnly: true,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> SubmitForEmployeeAsync(
        Guid id,
        LeaveVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            id,
            request.ExpectedVersion,
            LeaveRequestStatus.Pending,
            reason: null,
            dateTo: null,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> RecordSickLeaveAsync(
        Guid id,
        LeaveVersionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            id,
            request.ExpectedVersion,
            LeaveRequestStatus.Recorded,
            reason: null,
            dateTo: null,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> CloseSickLeaveAsync(
        Guid id,
        CloseSickLeaveRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            id,
            request.ExpectedVersion,
            LeaveRequestStatus.Closed,
            reason: null,
            request.DateTo,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> DecideAsync(
        Guid id,
        LeaveDecisionRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            id,
            request.ExpectedVersion,
            request.Decision == LeaveDecision.Approve
                ? LeaveRequestStatus.Approved
                : LeaveRequestStatus.Rejected,
            request.Reason,
            dateTo: null,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> CancelAsync(
        Guid id,
        CancelLeaveRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            id,
            request.ExpectedVersion,
            LeaveRequestStatus.Cancelled,
            request.Reason,
            dateTo: null,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> CreateAsync(
        Guid employeeId,
        CreateLeaveRequest request,
        ApplicationUser actor,
        HttpContext httpContext,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = InputValidation.ValidateLeaveRequest(
            request.Type,
            request.DateFrom,
            request.DateTo,
            request.IsFullDay,
            request.StartTime,
            request.EndTime,
            request.EmployeeNote);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var now = timeProvider.GetUtcNow();
        var initialStatus = LeaveRequestRules.InitialStatus(request.Type);
        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            EmployeeId = employeeId,
            CreatedByUserId = actor.Id,
            Type = request.Type,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            IsFullDay = request.IsFullDay,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = initialStatus,
            EmployeeNote = NormalizeOptional(request.EmployeeNote),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        leave.StatusHistory.Add(new LeaveStatusHistory
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            LeaveRequestId = leave.Id,
            FromStatus = null,
            ToStatus = initialStatus,
            ActorUserId = actor.Id,
            OccurredAtUtc = now
        });
        dbContext.LeaveRequests.Add(leave);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            request.Type == LeaveType.SickLeave
                ? "LeaveRequest.SickLeaveReported"
                : "LeaveRequest.Created",
            "LeaveRequest",
            leave.Id.ToString(),
            httpContext.TraceIdentifier,
            request.Type == LeaveType.SickLeave
                ? "Betegállomány bejelentve; diagnózis nem került tárolásra."
                : "Távolléti kérelem piszkozatként létrehozva.");
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await Query(dbContext)
            .SingleAsync(item => item.Id == leave.Id, cancellationToken);
        return Results.Created($"/api/me/leave-requests/{leave.Id}", Map(created));
    }

    private static async Task<IResult> TransitionAsync(
        Guid id,
        uint expectedVersion,
        LeaveRequestStatus targetStatus,
        string? reason,
        DateOnly? dateTo,
        bool selfOnly,
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

        if (selfOnly && actor.EmployeeId is null)
        {
            return EmployeeLinkRequired();
        }

        var leave = await Query(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId &&
                    (!selfOnly || item.EmployeeId == actor.EmployeeId),
                cancellationToken);
        if (leave is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (leave.Version != expectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A távollét a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        var effectiveDateTo = targetStatus == LeaveRequestStatus.Closed
            ? dateTo
            : leave.DateTo;
        var errors = InputValidation.ValidateLeaveTransition(
            leave.Status,
            targetStatus,
            effectiveDateTo,
            reason);
        if (targetStatus == LeaveRequestStatus.Closed && errors.Count == 0)
        {
            errors = InputValidation.ValidateLeaveRequest(
                leave.Type,
                leave.DateFrom,
                effectiveDateTo,
                leave.IsFullDay,
                leave.StartTime,
                leave.EndTime,
                leave.EmployeeNote);
        }

        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var previousStatus = leave.Status;
        var now = timeProvider.GetUtcNow();
        leave.Status = targetStatus;
        leave.DateTo = effectiveDateTo;
        leave.UpdatedAtUtc = now;
        if (targetStatus is LeaveRequestStatus.Approved or
            LeaveRequestStatus.Rejected or
            LeaveRequestStatus.Cancelled)
        {
            leave.DecisionReason = NormalizeOptional(reason);
            leave.DecidedByUserId = actor.Id;
            leave.DecidedAtUtc = now;
        }

        leave.StatusHistory.Add(new LeaveStatusHistory
        {
            Id = Guid.NewGuid(),
            OrganizationId = leave.OrganizationId,
            LeaveRequestId = leave.Id,
            FromStatus = previousStatus,
            ToStatus = targetStatus,
            ActorUserId = actor.Id,
            OccurredAtUtc = now,
            Reason = NormalizeOptional(reason)
        });
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            $"LeaveRequest.{targetStatus}",
            "LeaveRequest",
            leave.Id.ToString(),
            httpContext.TraceIdentifier,
            $"Távolléti állapot módosítva: {previousStatus} → {targetStatus}.");

        return await SaveAndMapAsync(
            leave,
            dbContext,
            "A távollét mentés közben megváltozott. Töltse újra az adatokat.",
            cancellationToken);
    }

    private static async Task<IResult> SaveAndMapAsync(
        LeaveRequest leave,
        PatikaDbContext dbContext,
        string conflictDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(conflictDetail);
        }

        return Results.Ok(Map(leave));
    }

    private static IQueryable<LeaveRequest> Query(
        PatikaDbContext dbContext,
        bool tracking = false)
    {
        var query = tracking
            ? dbContext.LeaveRequests
            : dbContext.LeaveRequests.AsNoTracking();
        return query
            .Include(request => request.Employee)
            .Include(request => request.StatusHistory);
    }

    private static LeaveRequestResponse Map(LeaveRequest request) =>
        new(
            request.Id,
            request.EmployeeId,
            request.Employee?.DisplayName ?? string.Empty,
            request.Type,
            request.DateFrom,
            request.DateTo,
            request.IsFullDay,
            request.StartTime,
            request.EndTime,
            request.Status,
            request.EmployeeNote,
            request.DecisionReason,
            request.StatusHistory
                .OrderBy(history => history.OccurredAtUtc)
                .Select(history => new LeaveStatusHistoryResponse(
                    history.FromStatus,
                    history.ToStatus,
                    history.OccurredAtUtc,
                    history.Reason))
                .ToArray(),
            request.Version,
            request.CreatedAtUtc,
            request.UpdatedAtUtc);

    private static Task<bool> EmployeeExistsAsync(
        Guid employeeId,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Employees.AnyAsync(
            employee =>
                employee.Id == employeeId &&
                employee.OrganizationId == organizationId,
            cancellationToken);

    private static IResult EmployeeLinkRequired() =>
        EndpointHelpers.ValidationProblem(
            [new ApiValidationError(
                "EMPLOYEE_LINK_REQUIRED",
                "A saját művelethez kapcsolt dolgozói profil szükséges.",
                "employeeId")]);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
