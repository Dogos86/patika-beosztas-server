using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class LocationOpeningEndpoints
{
    public static IEndpointRouteBuilder MapLocationOpeningEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/admin/locations/{locationId:guid}/weekly-opening")
            .WithTags("Location opening hours")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageLocations));
        group.MapGet("", GetAsync)
            .WithSummary("Telephely heti nyitvatartásának lekérése")
            .Produces<LocationWeeklyOpeningResponse>()
            .ProducesStandardErrors();
        group.MapPut("", PutAsync)
            .RequireAntiforgery()
            .WithSummary("Telephely heti nyitvatartásának létrehozása vagy módosítása")
            .Produces<LocationWeeklyOpeningResponse>()
            .Produces<LocationWeeklyOpeningResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors(includeConflict: true);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid locationId,
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
                item =>
                    item.Id == locationId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (location is null)
        {
            return EndpointHelpers.NotFound();
        }

        var opening = await Query(dbContext)
            .SingleOrDefaultAsync(
                item =>
                    item.LocationId == locationId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        return opening is null
            ? EndpointHelpers.NotFound()
            : Results.Ok(Map(opening));
    }

    private static async Task<IResult> PutAsync(
        Guid locationId,
        UpdateLocationWeeklyOpeningRequest request,
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

        var errors = InputValidation.ValidateOpeningWeek(request.Days);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var location = await dbContext.Locations
            .SingleOrDefaultAsync(
                item =>
                    item.Id == locationId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (location is null)
        {
            return EndpointHelpers.NotFound();
        }

        var opening = await dbContext.LocationWeeklyOpenings
            .Include(item => item.Intervals)
            .SingleOrDefaultAsync(
                item =>
                    item.LocationId == locationId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        var isNew = opening is null;
        if (opening is null)
        {
            if (request.ExpectedVersion is not null)
            {
                return EndpointHelpers.Conflict(
                    "A nyitvatartás még nem létezik; létrehozáshoz ne adjon meg verziót.");
            }

            var now = timeProvider.GetUtcNow();
            opening = new LocationWeeklyOpening
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                LocationId = locationId,
                Location = location,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.LocationWeeklyOpenings.Add(opening);
        }
        else
        {
            if (request.ExpectedVersion is null || opening.Version != request.ExpectedVersion)
            {
                return EndpointHelpers.Conflict(
                    "A nyitvatartás a lekérés óta megváltozott. Töltse újra az adatokat.");
            }

            dbContext.OpeningIntervals.RemoveRange(opening.Intervals);
            opening.Intervals.Clear();
            opening.UpdatedAtUtc = timeProvider.GetUtcNow();
        }

        if (opening is null)
        {
            throw new InvalidOperationException("A nyitvatartási aggregate nem jött létre.");
        }

        var addedIntervals = ApplyDays(opening, request.Days);
        dbContext.OpeningIntervals.AddRange(addedIntervals);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            isNew ? "LocationWeeklyOpening.Created" : "LocationWeeklyOpening.Updated",
            "LocationWeeklyOpening",
            opening.Id.ToString(),
            httpContext.TraceIdentifier,
            "Telephely heti nyitvatartása mentve.");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A nyitvatartás mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var saved = await Query(dbContext)
            .SingleAsync(item => item.Id == opening.Id, cancellationToken);
        return isNew
            ? Results.Created(
                $"/api/admin/locations/{locationId}/weekly-opening",
                Map(saved))
            : Results.Ok(Map(saved));
    }

    private static IQueryable<LocationWeeklyOpening> Query(PatikaDbContext dbContext) =>
        dbContext.LocationWeeklyOpenings
            .AsNoTracking()
            .Include(opening => opening.Location)
            .Include(opening => opening.Intervals);

    private static List<OpeningInterval> ApplyDays(
        LocationWeeklyOpening opening,
        IReadOnlyList<OpeningDayRequest> days)
    {
        var addedIntervals = new List<OpeningInterval>();
        foreach (var day in days)
        {
            opening.SetMode(day.DayOfWeek, day.Mode);
            foreach (var interval in day.Intervals)
            {
                var addedInterval = new OpeningInterval
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = opening.OrganizationId,
                    LocationWeeklyOpeningId = opening.Id,
                    DayOfWeek = day.DayOfWeek,
                    StartTime = interval.StartTime,
                    EndTime = interval.EndTime
                };
                opening.Intervals.Add(addedInterval);
                addedIntervals.Add(addedInterval);
            }
        }

        return addedIntervals;
    }

    private static LocationWeeklyOpeningResponse Map(LocationWeeklyOpening opening) =>
        new(
            opening.Id,
            opening.LocationId,
            opening.Location?.Name ?? string.Empty,
            opening.Location?.IsActive ?? false,
            Enum.GetValues<DayOfWeek>()
                .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
                .Select(day => new OpeningDayResponse(
                    day,
                    opening.GetMode(day),
                    opening.Intervals
                        .Where(interval => interval.DayOfWeek == day)
                        .OrderBy(interval => interval.StartTime)
                        .Select(interval => new OpeningIntervalResponse(
                            interval.Id,
                            interval.StartTime,
                            interval.EndTime))
                        .ToArray()))
                .ToArray(),
            opening.Location?.IsActive == false
                ? ["INACTIVE_LOCATION_EXCLUDED_FROM_PLANNING"]
                : [],
            opening.Version,
            opening.CreatedAtUtc,
            opening.UpdatedAtUtc);
}
