using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;
using DomainLocation = PatikaBeosztas.Domain.Location;

namespace PatikaBeosztas.Api.Endpoints;

public static class LocationEndpoints
{
    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/locations")
            .WithTags("Locations")
            .RequireAuthorization(PermissionPolicies.For(ApplicationPermission.ManageLocations));

        group.MapGet("", ListAsync)
            .WithSummary("Telephelyek szervezeten belüli listázása")
            .Produces<PagedResponse<LocationResponse>>()
            .ProducesStandardErrors();
        group.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Telephely részletes lekérése")
            .Produces<LocationResponse>()
            .ProducesStandardErrors();
        group.MapPost("", CreateAsync)
            .RequireAntiforgery()
            .WithSummary("Telephely létrehozása")
            .Produces<LocationResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();
        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAntiforgery()
            .WithSummary("Telephely módosítása vagy deaktiválása")
            .Produces<LocationResponse>()
            .ProducesStandardErrors(includeConflict: true);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        int page = 1,
        int pageSize = 25,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
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

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Locations
            .AsNoTracking()
            .Where(location => location.OrganizationId == actor.OrganizationId);
        if (!includeInactive)
        {
            query = query.Where(location => location.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(location => EF.Functions.ILike(location.Name, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var locations = await query
            .OrderBy(location => location.Name)
            .ThenBy(location => location.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(location => new LocationResponse(
                location.Id,
                location.Name,
                location.Type,
                location.Address,
                location.IsActive,
                location.Version,
                location.CreatedAtUtc,
                location.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<LocationResponse>(
            locations,
            page,
            pageSize,
            totalCount));
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

        var location = await dbContext.Locations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        return location is null
            ? EndpointHelpers.NotFound()
            : Results.Ok(MapLocation(location));
    }

    private static async Task<IResult> CreateAsync(
        CreateLocationRequest request,
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

        var errors = InputValidation.ValidateLocation(request.Name, request.Address);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var now = timeProvider.GetUtcNow();
        var location = new DomainLocation
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Address = NormalizeOptional(request.Address),
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Locations.Add(location);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "Location.Created",
            "Location",
            location.Id.ToString(),
            httpContext.TraceIdentifier,
            "Telephely létrehozva.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(location).ReloadAsync(cancellationToken);

        return Results.Created(
            $"/api/admin/locations/{location.Id}",
            MapLocation(location));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateLocationRequest request,
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

        var errors = InputValidation.ValidateLocation(request.Name, request.Address);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var location = await dbContext.Locations.SingleOrDefaultAsync(
            item => item.Id == id && item.OrganizationId == actor.OrganizationId,
            cancellationToken);
        if (location is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (location.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A telephely adatai a lekérés óta megváltoztak. Töltse újra az adatokat.");
        }

        location.Name = request.Name.Trim();
        location.Type = request.Type;
        location.Address = NormalizeOptional(request.Address);
        location.IsActive = request.IsActive;
        location.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            request.IsActive ? "Location.Updated" : "Location.Deactivated",
            "Location",
            location.Id.ToString(),
            httpContext.TraceIdentifier,
            request.IsActive
                ? "Telephely adatai módosítva."
                : "Telephely deaktiválva; történeti adatai megmaradnak.");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContext.Entry(location).ReloadAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A telephely adatai mentés közben megváltoztak. Töltse újra az adatokat.");
        }

        return Results.Ok(MapLocation(location));
    }

    private static LocationResponse MapLocation(DomainLocation location) =>
        new(
            location.Id,
            location.Name,
            location.Type,
            location.Address,
            location.IsActive,
            location.Version,
            location.CreatedAtUtc,
            location.UpdatedAtUtc);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
