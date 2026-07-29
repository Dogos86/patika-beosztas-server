using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class CoverageRequirementEndpoints
{
    public static IEndpointRouteBuilder MapCoverageRequirementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/coverage-requirements")
            .WithTags("Coverage requirements")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageCoverageRules));
        group.MapGet("", ListAsync)
            .WithSummary("Lefedettségi szabályok listázása és szűrése")
            .Produces<IReadOnlyList<CoverageRequirementResponse>>()
            .ProducesStandardErrors();
        group.MapPost("", CreateAsync)
            .RequireAntiforgery()
            .WithSummary("Lefedettségi szabály létrehozása")
            .Produces<CoverageRequirementResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();
        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAntiforgery()
            .WithSummary("Lefedettségi szabály módosítása")
            .Produces<CoverageRequirementResponse>()
            .ProducesStandardErrors(includeConflict: true);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .RequireAntiforgery()
            .WithSummary("Lefedettségi szabály inaktiválása")
            .Produces<CoverageRequirementResponse>()
            .ProducesStandardErrors(includeConflict: true);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid? locationId,
        DayOfWeek? dayOfWeek,
        StaffingCapability? capability,
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

        if (locationId is not null &&
            !await LocationExistsAsync(
                locationId.Value,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var query = Query(dbContext)
            .Where(requirement => requirement.OrganizationId == actor.OrganizationId);
        if (locationId is not null)
        {
            query = query.Where(requirement => requirement.LocationId == locationId);
        }

        if (dayOfWeek is not null)
        {
            query = query.Where(requirement => requirement.DayOfWeek == dayOfWeek);
        }

        if (capability is not null)
        {
            query = query.Where(requirement => requirement.RequiredCapability == capability);
        }

        if (includeInactive != true)
        {
            query = query.Where(requirement => requirement.IsActive);
        }

        var requirements = await query
            .OrderBy(requirement => requirement.Location!.Name)
            .ThenBy(requirement => requirement.DayOfWeek)
            .ThenBy(requirement => requirement.StartTime)
            .ToArrayAsync(cancellationToken);
        var openings = await LoadOpeningsAsync(
            requirements.Select(requirement => requirement.LocationId),
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        return Results.Ok(requirements
            .Select(requirement => Map(
                requirement,
                openings.GetValueOrDefault(requirement.LocationId)))
            .ToArray());
    }

    private static async Task<IResult> CreateAsync(
        CreateCoverageRequirementRequest request,
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

        var errors = InputValidation.ValidateCoverageRequirement(
            request.StartTime,
            request.EndTime,
            request.RequiredCount);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        if (!await LocationExistsAsync(
                request.LocationId,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var now = timeProvider.GetUtcNow();
        var requirement = new CoverageRequirement
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            LocationId = request.LocationId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RequiredCapability = request.RequiredCapability,
            RequiredCount = request.RequiredCount,
            Severity = request.Severity,
            IsActive = request.IsActive,
            TimeType = request.TimeType,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.CoverageRequirements.Add(requirement);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "CoverageRequirement.Created",
            "CoverageRequirement",
            requirement.Id.ToString(),
            httpContext.TraceIdentifier,
            "Lefedettségi szabály létrehozva.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var created = await Query(dbContext)
            .SingleAsync(item => item.Id == requirement.Id, cancellationToken);
        var opening = await LoadOpeningAsync(
            created.LocationId,
            created.OrganizationId,
            dbContext,
            cancellationToken);
        return Results.Created(
            $"/api/admin/coverage-requirements/{requirement.Id}",
            Map(created, opening));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCoverageRequirementRequest request,
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

        var errors = InputValidation.ValidateCoverageRequirement(
            request.StartTime,
            request.EndTime,
            request.RequiredCount);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var requirement = await dbContext.CoverageRequirements
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (requirement is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (requirement.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A lefedettségi szabály a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (!await LocationExistsAsync(
                request.LocationId,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        requirement.LocationId = request.LocationId;
        requirement.DayOfWeek = request.DayOfWeek;
        requirement.StartTime = request.StartTime;
        requirement.EndTime = request.EndTime;
        requirement.RequiredCapability = request.RequiredCapability;
        requirement.RequiredCount = request.RequiredCount;
        requirement.Severity = request.Severity;
        requirement.IsActive = request.IsActive;
        requirement.TimeType = request.TimeType;
        requirement.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "CoverageRequirement.Updated",
            "CoverageRequirement",
            requirement.Id.ToString(),
            httpContext.TraceIdentifier,
            "Lefedettségi szabály módosítva.");
        return await SaveAndMapAsync(
            requirement,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        DeactivateCoverageRequirementRequest request,
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

        var requirement = await dbContext.CoverageRequirements
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (requirement is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (requirement.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A lefedettségi szabály a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (!requirement.IsActive)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "COVERAGE_REQUIREMENT_ALREADY_INACTIVE",
                    "A lefedettségi szabály már inaktív.",
                    "isActive")]);
        }

        requirement.IsActive = false;
        requirement.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "CoverageRequirement.Deactivated",
            "CoverageRequirement",
            requirement.Id.ToString(),
            httpContext.TraceIdentifier,
            "Lefedettségi szabály inaktiválva.");
        return await SaveAndMapAsync(
            requirement,
            dbContext,
            cancellationToken);
    }

    private static async Task<IResult> SaveAndMapAsync(
        CoverageRequirement requirement,
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
                "A lefedettségi szabály mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var saved = await Query(dbContext)
            .SingleAsync(item => item.Id == requirement.Id, cancellationToken);
        var opening = await LoadOpeningAsync(
            saved.LocationId,
            saved.OrganizationId,
            dbContext,
            cancellationToken);
        return Results.Ok(Map(saved, opening));
    }

    private static IQueryable<CoverageRequirement> Query(PatikaDbContext dbContext) =>
        dbContext.CoverageRequirements
            .AsNoTracking()
            .Include(requirement => requirement.Location);

    private static async Task<Dictionary<Guid, LocationWeeklyOpening>> LoadOpeningsAsync(
        IEnumerable<Guid> locationIds,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ids = locationIds.Distinct().ToArray();
        return await dbContext.LocationWeeklyOpenings
            .AsNoTracking()
            .Include(opening => opening.Intervals)
            .Where(opening =>
                opening.OrganizationId == organizationId &&
                ids.Contains(opening.LocationId))
            .ToDictionaryAsync(opening => opening.LocationId, cancellationToken);
    }

    private static Task<LocationWeeklyOpening?> LoadOpeningAsync(
        Guid locationId,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.LocationWeeklyOpenings
            .AsNoTracking()
            .Include(opening => opening.Intervals)
            .SingleOrDefaultAsync(
                opening =>
                    opening.LocationId == locationId &&
                    opening.OrganizationId == organizationId,
                cancellationToken);

    private static CoverageRequirementResponse Map(
        CoverageRequirement requirement,
        LocationWeeklyOpening? opening) =>
        new(
            requirement.Id,
            requirement.LocationId,
            requirement.Location?.Name ?? string.Empty,
            requirement.Location?.IsActive ?? false,
            requirement.DayOfWeek,
            requirement.StartTime,
            requirement.EndTime,
            requirement.RequiredCapability,
            requirement.RequiredCount,
            requirement.Severity,
            requirement.IsActive,
            GetWarnings(requirement, opening),
            requirement.Version,
            requirement.CreatedAtUtc,
            requirement.UpdatedAtUtc,
            requirement.TimeType);

    private static List<string> GetWarnings(
        CoverageRequirement requirement,
        LocationWeeklyOpening? opening)
    {
        var warnings = new List<string>();
        if (requirement.Location?.IsActive == false)
        {
            warnings.Add("INACTIVE_LOCATION_EXCLUDED_FROM_PLANNING");
        }

        if (!requirement.IsActive || requirement.Location?.IsActive != true)
        {
            return warnings;
        }

        if (opening is null)
        {
            warnings.Add("OPENING_HOURS_NOT_CONFIGURED");
            return warnings;
        }

        var day = new OpeningDayDefinition(
            requirement.DayOfWeek,
            opening.GetMode(requirement.DayOfWeek),
            opening.Intervals
                .Where(interval => interval.DayOfWeek == requirement.DayOfWeek)
                .OrderBy(interval => interval.StartTime)
                .Select(interval => new OpeningIntervalDefinition(
                    interval.StartTime,
                    interval.EndTime))
                .ToArray());
        if (!OpeningHoursRules.Contains(day, requirement.StartTime, requirement.EndTime))
        {
            warnings.Add("COVERAGE_OUTSIDE_OPENING_HOURS");
        }

        return warnings;
    }

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
