using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class LocationShiftTemplateEndpoints
{
    public static IEndpointRouteBuilder MapLocationShiftTemplateEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var byLocation = endpoints.MapGroup(
                "/api/admin/locations/{locationId:guid}/shift-templates")
            .WithTags("Location shift templates")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageLocations));
        byLocation.MapGet("", ListAsync)
            .WithSummary("Telephely műszaksablonjainak listázása")
            .Produces<IReadOnlyList<LocationShiftTemplateResponse>>()
            .ProducesStandardErrors();
        byLocation.MapPost("", CreateAsync)
            .RequireAntiforgery()
            .WithSummary("Telephelyi műszaksablon létrehozása")
            .Produces<LocationShiftTemplateResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();

        var byId = endpoints.MapGroup("/api/admin/location-shift-templates")
            .WithTags("Location shift templates")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageLocations));
        byId.MapPut("/{id:guid}", UpdateAsync)
            .RequireAntiforgery()
            .WithSummary("Telephelyi műszaksablon módosítása")
            .Produces<LocationShiftTemplateResponse>()
            .ProducesStandardErrors(includeConflict: true);
        byId.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .RequireAntiforgery()
            .WithSummary("Telephelyi műszaksablon inaktiválása")
            .Produces<LocationShiftTemplateResponse>()
            .ProducesStandardErrors(includeConflict: true);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid locationId,
        bool? includeInactive,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        if (!await LocationExistsAsync(
                locationId,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var templates = await Query(dbContext)
            .Where(template =>
                template.OrganizationId == actor.OrganizationId &&
                template.LocationId == locationId &&
                (includeInactive == true || template.IsActive))
            .OrderBy(template => template.Name)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(templates.Select(Map).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        Guid locationId,
        CreateLocationShiftTemplateRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var errors = InputValidation.ValidateShiftTemplate(
            request.Name,
            request.Weekdays,
            request.StartTime,
            request.EndTime);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        if (!await LocationExistsAsync(
                locationId,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var now = timeProvider.GetUtcNow();
        var template = new LocationShiftTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            LocationId = locationId,
            Name = request.Name.Trim(),
            Category = request.Category,
            WeekdayMask = WeekdayMaskRules.ToMask(request.Weekdays),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = request.IsActive,
            RequiredCapability = request.RequiredCapability,
            TimeType = request.TimeType,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.LocationShiftTemplates.Add(template);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "LocationShiftTemplate.Created",
            "LocationShiftTemplate",
            template.Id.ToString(),
            httpContext.TraceIdentifier,
            "Telephelyi műszaksablon létrehozva.");
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await Query(dbContext)
            .SingleAsync(item => item.Id == template.Id, cancellationToken);
        return Results.Created(
            $"/api/admin/location-shift-templates/{template.Id}",
            Map(created));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateLocationShiftTemplateRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var errors = InputValidation.ValidateShiftTemplate(
            request.Name,
            request.Weekdays,
            request.StartTime,
            request.EndTime);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var template = await dbContext.LocationShiftTemplates
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (template is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (template.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A műszaksablon a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        template.Name = request.Name.Trim();
        template.Category = request.Category;
        template.WeekdayMask = WeekdayMaskRules.ToMask(request.Weekdays);
        template.StartTime = request.StartTime;
        template.EndTime = request.EndTime;
        template.IsActive = request.IsActive;
        template.RequiredCapability = request.RequiredCapability;
        template.TimeType = request.TimeType;
        template.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "LocationShiftTemplate.Updated",
            "LocationShiftTemplate",
            template.Id.ToString(),
            httpContext.TraceIdentifier,
            "Telephelyi műszaksablon módosítva.");

        return await SaveAndMapAsync(
            template,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        DeactivateLocationShiftTemplateRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is null)
        {
            return EndpointHelpers.Unauthorized();
        }

        var template = await dbContext.LocationShiftTemplates
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (template is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (template.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A műszaksablon a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (!template.IsActive)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "SHIFT_TEMPLATE_ALREADY_INACTIVE",
                    "A műszaksablon már inaktív.",
                    "isActive")]);
        }

        template.IsActive = false;
        template.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "LocationShiftTemplate.Deactivated",
            "LocationShiftTemplate",
            template.Id.ToString(),
            httpContext.TraceIdentifier,
            "Telephelyi műszaksablon inaktiválva.");
        return await SaveAndMapAsync(
            template,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult> SaveAndMapAsync(
        LocationShiftTemplate template,
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
                "A műszaksablon mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var saved = await Query(dbContext)
            .SingleAsync(item => item.Id == template.Id, cancellationToken);
        return Results.Ok(Map(saved));
    }

    private static IQueryable<LocationShiftTemplate> Query(PatikaDbContext dbContext) =>
        dbContext.LocationShiftTemplates
            .AsNoTracking()
            .Include(template => template.Location);

    private static LocationShiftTemplateResponse Map(LocationShiftTemplate template) =>
        new(
            template.Id,
            template.LocationId,
            template.Location?.Name ?? string.Empty,
            template.Category,
            template.Name,
            WeekdayMaskRules.FromMask(template.WeekdayMask),
            template.StartTime,
            template.EndTime,
            template.IsActive,
            template.RequiredCapability,
            template.Version,
            template.CreatedAtUtc,
            template.UpdatedAtUtc,
            template.TimeType);

    private static Task<ApplicationUser?> GetActorAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);

    private static Task<bool> LocationExistsAsync(
        Guid locationId,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Locations.AnyAsync(
            location =>
                location.Id == locationId &&
                location.OrganizationId == organizationId,
            cancellationToken);
}
