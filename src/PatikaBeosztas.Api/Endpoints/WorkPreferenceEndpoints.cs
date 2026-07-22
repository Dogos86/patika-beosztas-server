using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class WorkPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapWorkPreferenceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var self = endpoints.MapGroup("/api/me/work-preferences")
            .WithTags("Work preferences")
            .RequireAuthorization();
        self.MapGet("", ListOwnAsync)
            .WithSummary("Saját munkavégzési kérések és szabályok listázása")
            .Produces<IReadOnlyList<WorkPreferenceResponse>>()
            .ProducesStandardErrors();
        self.MapPost("", CreateOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját munkavégzési kérés vagy szabály létrehozása")
            .Produces<WorkPreferenceResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();
        self.MapPut("/{id:guid}", UpdateOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját munkavégzési kérés vagy szabály módosítása")
            .Produces<WorkPreferenceResponse>()
            .ProducesStandardErrors(includeConflict: true);
        self.MapPost("/{id:guid}/deactivate", DeactivateOwnAsync)
            .RequireAntiforgery()
            .WithSummary("Saját munkavégzési kérés vagy szabály inaktiválása")
            .Produces<WorkPreferenceResponse>()
            .ProducesStandardErrors(includeConflict: true);

        var adminEmployee = endpoints.MapGroup(
                "/api/admin/employees/{employeeId:guid}/work-preferences")
            .WithTags("Work preferences")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageWorkPreferences));
        adminEmployee.MapGet("", ListForEmployeeAsync)
            .WithSummary("Dolgozó munkavégzési kéréseinek és szabályainak listázása")
            .Produces<IReadOnlyList<WorkPreferenceResponse>>()
            .ProducesStandardErrors();
        adminEmployee.MapPost("", CreateForEmployeeAsync)
            .RequireAntiforgery()
            .WithSummary("Munkavégzési kérés vagy szabály rögzítése dolgozó számára")
            .Produces<WorkPreferenceResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();

        var admin = endpoints.MapGroup("/api/admin/work-preferences")
            .WithTags("Work preferences")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageWorkPreferences));
        admin.MapPut("/{id:guid}", UpdateForEmployeeAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozó munkavégzési kérésének vagy szabályának módosítása")
            .Produces<WorkPreferenceResponse>()
            .ProducesStandardErrors(includeConflict: true);
        admin.MapPost("/{id:guid}/deactivate", DeactivateForEmployeeAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozó munkavégzési kérésének vagy szabályának inaktiválása")
            .Produces<WorkPreferenceResponse>()
            .ProducesStandardErrors(includeConflict: true);

        return endpoints;
    }

    private static async Task<IResult> ListOwnAsync(
        bool? includeInactive,
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

        var preferences = await Query(dbContext)
            .Where(preference =>
                preference.OrganizationId == actor.OrganizationId &&
                preference.EmployeeId == actor.EmployeeId &&
                (includeInactive == true || preference.IsActive))
            .OrderBy(preference => preference.DateFrom)
            .ThenBy(preference => preference.StartTime)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(preferences.Select(Map).ToArray());
    }

    private static async Task<IResult> ListForEmployeeAsync(
        Guid employeeId,
        bool? includeInactive,
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

        if (!await EmployeeExistsAsync(
                employeeId,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var preferences = await Query(dbContext)
            .Where(preference =>
                preference.OrganizationId == actor.OrganizationId &&
                preference.EmployeeId == employeeId &&
                (includeInactive == true || preference.IsActive))
            .OrderBy(preference => preference.DateFrom)
            .ThenBy(preference => preference.StartTime)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(preferences.Select(Map).ToArray());
    }

    private static async Task<IResult> CreateOwnAsync(
        CreateWorkPreferenceRequest request,
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
        CreateWorkPreferenceRequest request,
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

    private static Task<IResult> UpdateOwnAsync(
        Guid id,
        UpdateWorkPreferenceRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            id,
            request,
            selfOnly: true,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> UpdateForEmployeeAsync(
        Guid id,
        UpdateWorkPreferenceRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            id,
            request,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> DeactivateOwnAsync(
        Guid id,
        DeactivateWorkPreferenceRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        DeactivateAsync(
            id,
            request,
            selfOnly: true,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static Task<IResult> DeactivateForEmployeeAsync(
        Guid id,
        DeactivateWorkPreferenceRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        DeactivateAsync(
            id,
            request,
            selfOnly: false,
            httpContext,
            userManager,
            dbContext,
            auditWriter,
            timeProvider,
            cancellationToken);

    private static async Task<IResult> CreateAsync(
        Guid employeeId,
        CreateWorkPreferenceRequest request,
        ApplicationUser actor,
        HttpContext httpContext,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = InputValidation.ValidateWorkPreference(
            request.DateFrom,
            request.DateTo,
            request.IsFullDay,
            request.StartTime,
            request.EndTime,
            request.Note);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var locationError = await ValidateLocationAsync(
            request.LocationId,
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        if (locationError is not null)
        {
            return EndpointHelpers.ValidationProblem([locationError]);
        }

        var now = timeProvider.GetUtcNow();
        var preference = new WorkPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            EmployeeId = employeeId,
            Type = request.Type,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            DayOfWeek = request.DayOfWeek,
            IsFullDay = request.IsFullDay,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            LocationId = request.LocationId,
            Note = NormalizeOptional(request.Note),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.WorkPreferences.Add(preference);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "WorkPreference.Created",
            "WorkPreference",
            preference.Id.ToString(),
            httpContext.TraceIdentifier,
            "Munkavégzési kérés vagy szabály létrehozva.");
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await Query(dbContext)
            .SingleAsync(item => item.Id == preference.Id, cancellationToken);
        return Results.Created(
            $"/api/me/work-preferences/{preference.Id}",
            Map(created));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateWorkPreferenceRequest request,
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

        var errors = InputValidation.ValidateWorkPreference(
            request.DateFrom,
            request.DateTo,
            request.IsFullDay,
            request.StartTime,
            request.EndTime,
            request.Note);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var preference = await Query(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId &&
                    (!selfOnly || item.EmployeeId == actor.EmployeeId),
                cancellationToken);
        if (preference is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (!preference.IsActive)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "WORK_PREFERENCE_INACTIVE",
                    "Az inaktivált munkavégzési kérés vagy szabály nem módosítható.",
                    "isActive")]);
        }

        if (preference.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A munkavégzési kérés a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        var locationError = await ValidateLocationAsync(
            request.LocationId,
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        if (locationError is not null)
        {
            return EndpointHelpers.ValidationProblem([locationError]);
        }

        preference.Type = request.Type;
        preference.DateFrom = request.DateFrom;
        preference.DateTo = request.DateTo;
        preference.DayOfWeek = request.DayOfWeek;
        preference.IsFullDay = request.IsFullDay;
        preference.StartTime = request.StartTime;
        preference.EndTime = request.EndTime;
        preference.LocationId = request.LocationId;
        preference.Note = NormalizeOptional(request.Note);
        preference.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "WorkPreference.Updated",
            "WorkPreference",
            preference.Id.ToString(),
            httpContext.TraceIdentifier,
            "Munkavégzési kérés vagy szabály módosítva.");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A munkavégzési kérés mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var updated = await Query(dbContext)
            .SingleAsync(item => item.Id == preference.Id, cancellationToken);
        return Results.Ok(Map(updated));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        DeactivateWorkPreferenceRequest request,
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

        var preference = await Query(dbContext, tracking: true)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId &&
                    (!selfOnly || item.EmployeeId == actor.EmployeeId),
                cancellationToken);
        if (preference is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (preference.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A munkavégzési kérés a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (!preference.IsActive)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "WORK_PREFERENCE_ALREADY_INACTIVE",
                    "A munkavégzési kérés vagy szabály már inaktív.",
                    "isActive")]);
        }

        preference.IsActive = false;
        preference.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "WorkPreference.Deactivated",
            "WorkPreference",
            preference.Id.ToString(),
            httpContext.TraceIdentifier,
            "Munkavégzési kérés vagy szabály inaktiválva.");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A munkavégzési kérés mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var deactivated = await Query(dbContext)
            .SingleAsync(item => item.Id == preference.Id, cancellationToken);
        return Results.Ok(Map(deactivated));
    }

    private static IQueryable<WorkPreference> Query(
        PatikaDbContext dbContext,
        bool tracking = false)
    {
        var query = tracking
            ? dbContext.WorkPreferences
            : dbContext.WorkPreferences.AsNoTracking();
        return query
            .Include(preference => preference.Employee)
            .Include(preference => preference.Location);
    }

    private static WorkPreferenceResponse Map(WorkPreference preference) =>
        new(
            preference.Id,
            preference.EmployeeId,
            preference.Employee?.DisplayName ?? string.Empty,
            preference.Type,
            preference.DateFrom,
            preference.DateTo,
            preference.DayOfWeek,
            preference.IsFullDay,
            preference.StartTime,
            preference.EndTime,
            preference.LocationId,
            preference.Location?.Name,
            preference.Note,
            preference.IsActive,
            preference.Version,
            preference.CreatedAtUtc,
            preference.UpdatedAtUtc);

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

    private static async Task<ApiValidationError?> ValidateLocationAsync(
        Guid? locationId,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (locationId is null ||
            await dbContext.Locations.AnyAsync(
                location =>
                    location.Id == locationId &&
                    location.OrganizationId == organizationId,
                cancellationToken))
        {
            return null;
        }

        return new ApiValidationError(
            "LOCATION_NOT_FOUND",
            "A megadott telephely nem található a szervezetben.",
            "locationId");
    }

    private static IResult EmployeeLinkRequired() =>
        EndpointHelpers.ValidationProblem(
            [new ApiValidationError(
                "EMPLOYEE_LINK_REQUIRED",
                "A saját művelethez kapcsolt dolgozói profil szükséges.",
                "employeeId")]);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
