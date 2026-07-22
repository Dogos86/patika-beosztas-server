using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class EmployeeWorkProfileEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeWorkProfileEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/admin/employees/{employeeId:guid}/work-profile")
            .WithTags("Employee work profiles")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageEmployees));
        group.MapGet("", GetAsync)
            .WithSummary("Dolgozó munkaprofiljának lekérése")
            .Produces<EmployeeWorkProfileResponse>()
            .ProducesStandardErrors();
        group.MapPut("", PutAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozó munkaprofiljának létrehozása vagy módosítása")
            .Produces<EmployeeWorkProfileResponse>()
            .Produces<EmployeeWorkProfileResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors(includeConflict: true);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid employeeId,
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

        var profile = await Query(dbContext)
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (profile is not null)
        {
            return Results.Ok(Map(profile));
        }

        return EndpointHelpers.NotFound();
    }

    private static async Task<IResult> PutAsync(
        Guid employeeId,
        UpdateEmployeeWorkProfileRequest request,
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

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                item =>
                    item.Id == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (employee is null)
        {
            return EndpointHelpers.NotFound();
        }

        var errors = InputValidation.ValidateWorkProfile(
            request,
            employee.IsActive,
            employee.IsSchedulable);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var profile = await dbContext.EmployeeWorkProfiles
            .SingleOrDefaultAsync(
                item =>
                    item.EmployeeId == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        var isNew = profile is null;
        var now = timeProvider.GetUtcNow();
        if (profile is null)
        {
            if (request.ExpectedVersion is not null)
            {
                return EndpointHelpers.Conflict(
                    "A munkaprofil még nem létezik; létrehozáshoz ne adjon meg verziót.");
            }

            profile = new EmployeeWorkProfile
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                EmployeeId = employeeId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.EmployeeWorkProfiles.Add(profile);
        }
        else
        {
            if (request.ExpectedVersion is null || profile.Version != request.ExpectedVersion)
            {
                return EndpointHelpers.Conflict(
                    "A munkaprofil a lekérés óta megváltozott. Töltse újra az adatokat.");
            }

            profile.UpdatedAtUtc = now;
        }

        if (profile is null)
        {
            throw new InvalidOperationException("A dolgozói munkaprofil nem jött létre.");
        }

        Apply(profile, request);
        employee.MonthlyMinutesLimit = request.ContractedMonthlyMinutes;
        employee.MaxDailyMinutes = request.MaximumDailyMinutes;
        employee.IncludeInAutoFill = request.IncludeInAutoFill;
        employee.UpdatedAtUtc = now;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            isNew ? "EmployeeWorkProfile.Created" : "EmployeeWorkProfile.Updated",
            "EmployeeWorkProfile",
            profile.Id.ToString(),
            httpContext.TraceIdentifier,
            "Dolgozói munkaprofil mentve; a kompatibilitási perclimitek szinkronizálva.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A munkaprofil mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var saved = await Query(dbContext)
            .SingleAsync(item => item.Id == profile.Id, cancellationToken);
        return isNew
            ? Results.Created(
                $"/api/admin/employees/{employeeId}/work-profile",
                Map(saved))
            : Results.Ok(Map(saved));
    }

    private static void Apply(
        EmployeeWorkProfile profile,
        UpdateEmployeeWorkProfileRequest request)
    {
        profile.ContractedMonthlyMinutes = request.ContractedMonthlyMinutes;
        profile.ContractedWeeklyMinutes = request.ContractedWeeklyMinutes;
        profile.StandardShiftMinutes = request.StandardShiftMinutes;
        profile.MinimumShiftMinutes = request.MinimumShiftMinutes;
        profile.MaximumRegularShiftMinutes = request.MaximumRegularShiftMinutes;
        profile.MaximumDailyMinutes = request.MaximumDailyMinutes;
        profile.AllowsLongShift = request.AllowsLongShift;
        profile.MaximumLongShiftMinutes = NormalizeConditionalLimit(
            request.AllowsLongShift,
            request.MaximumLongShiftMinutes);
        profile.AllowsFullOpeningHoursShift = request.AllowsFullOpeningHoursShift;
        profile.AllowsOvertime = request.AllowsOvertime;
        profile.MaximumOvertimeMinutesPerMonth = NormalizeConditionalLimit(
            request.AllowsOvertime,
            request.MaximumOvertimeMinutesPerMonth);
        profile.AllowsOnCallDuty = request.AllowsOnCallDuty;
        profile.MaximumOnCallAssignmentsPerMonth = NormalizeConditionalLimit(
            request.AllowsOnCallDuty,
            request.MaximumOnCallAssignmentsPerMonth);
        profile.AllowsStandby = request.AllowsStandby;
        profile.MaximumStandbyAssignmentsPerMonth = NormalizeConditionalLimit(
            request.AllowsStandby,
            request.MaximumStandbyAssignmentsPerMonth);
        profile.AllowsSaturday = request.AllowsSaturday;
        profile.MaximumSaturdaysPerMonth = NormalizeConditionalLimit(
            request.AllowsSaturday,
            request.MaximumSaturdaysPerMonth);
        profile.AllowsSunday = request.AllowsSunday;
        profile.MaximumSundaysPerMonth = NormalizeConditionalLimit(
            request.AllowsSunday,
            request.MaximumSundaysPerMonth);
        profile.IncludeInAutoFill = request.IncludeInAutoFill;
    }

    private static int? NormalizeConditionalLimit(bool isAllowed, int? value) =>
        isAllowed ? value : null;

    private static IQueryable<EmployeeWorkProfile> Query(PatikaDbContext dbContext) =>
        dbContext.EmployeeWorkProfiles
            .AsNoTracking()
            .Include(profile => profile.Employee);

    private static EmployeeWorkProfileResponse Map(EmployeeWorkProfile profile) =>
        new(
            profile.Id,
            profile.EmployeeId,
            profile.Employee?.DisplayName ?? string.Empty,
            profile.ContractedMonthlyMinutes,
            profile.ContractedWeeklyMinutes,
            profile.StandardShiftMinutes,
            profile.MinimumShiftMinutes,
            profile.MaximumRegularShiftMinutes,
            profile.MaximumDailyMinutes,
            profile.AllowsLongShift,
            profile.MaximumLongShiftMinutes,
            profile.AllowsFullOpeningHoursShift,
            profile.AllowsOvertime,
            profile.MaximumOvertimeMinutesPerMonth,
            profile.AllowsOnCallDuty,
            profile.MaximumOnCallAssignmentsPerMonth,
            profile.AllowsStandby,
            profile.MaximumStandbyAssignmentsPerMonth,
            profile.AllowsSaturday,
            profile.MaximumSaturdaysPerMonth,
            profile.AllowsSunday,
            profile.MaximumSundaysPerMonth,
            profile.IncludeInAutoFill,
            profile.Version,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
}
