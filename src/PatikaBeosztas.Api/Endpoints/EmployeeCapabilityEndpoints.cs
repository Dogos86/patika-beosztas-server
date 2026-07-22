using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class EmployeeCapabilityEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeCapabilityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
                "/api/admin/employees/{employeeId:guid}/capabilities")
            .WithTags("Employee capabilities")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageEmployees));
        group.MapGet("", GetAsync)
            .WithSummary("Dolgozó kompetenciáinak lekérése")
            .Produces<EmployeeCapabilitiesResponse>()
            .ProducesStandardErrors();
        group.MapPut("", PutAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozó kompetenciáinak cseréje")
            .Produces<EmployeeCapabilitiesResponse>()
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

        var employee = await Query(dbContext)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        return employee is null
            ? EndpointHelpers.NotFound()
            : Results.Ok(Map(employee));
    }

    private static async Task<IResult> PutAsync(
        Guid employeeId,
        UpdateEmployeeCapabilitiesRequest request,
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

        var errors = InputValidation.ValidateCapabilities(request.Capabilities);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var employee = await dbContext.Employees
            .Include(item => item.Capabilities)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == employeeId &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (employee is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (employee.Version != request.ExpectedEmployeeVersion)
        {
            return EndpointHelpers.Conflict(
                "A dolgozó kompetenciái a lekérés óta megváltoztak. Töltse újra az adatokat.");
        }

        var requested = request.Capabilities.ToHashSet();
        foreach (var existing in employee.Capabilities.ToArray())
        {
            if (!requested.Remove(existing.Capability))
            {
                dbContext.EmployeeCapabilities.Remove(existing);
                employee.Capabilities.Remove(existing);
            }
        }

        var now = timeProvider.GetUtcNow();
        foreach (var capability in requested)
        {
            employee.Capabilities.Add(new EmployeeCapability
            {
                OrganizationId = actor.OrganizationId,
                EmployeeId = employee.Id,
                Capability = capability,
                AssignedAtUtc = now
            });
        }

        employee.UpdatedAtUtc = now;
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "EmployeeCapabilities.Updated",
            "Employee",
            employee.Id.ToString(),
            httpContext.TraceIdentifier,
            "Dolgozói kompetenciák módosítva.");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return EndpointHelpers.Conflict(
                "A dolgozó kompetenciái mentés közben megváltoztak. Töltse újra az adatokat.");
        }

        var saved = await Query(dbContext)
            .SingleAsync(item => item.Id == employee.Id, cancellationToken);
        return Results.Ok(Map(saved));
    }

    private static IQueryable<Employee> Query(PatikaDbContext dbContext) =>
        dbContext.Employees
            .AsNoTracking()
            .Include(employee => employee.Capabilities);

    private static EmployeeCapabilitiesResponse Map(Employee employee)
    {
        var assigned = employee.Capabilities
            .Select(capability => capability.Capability)
            .Order()
            .ToArray();
        var effective = StaffingCapabilityRules.ResolveEffective(
                assigned,
                employee.ProfessionalRole,
                employee.CountsAsPharmacist)
            .Order()
            .ToArray();
        return new(
            employee.Id,
            employee.DisplayName,
            assigned,
            effective,
            employee.CountsAsPharmacist,
            employee.Version);
    }
}
