using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class EmployeeShiftQuotaRuleEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeShiftQuotaRuleEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var byEmployee = endpoints.MapGroup(
                "/api/admin/employees/{employeeId:guid}/shift-quota-rules")
            .WithTags("Employee shift quota rules")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageEmployees));
        byEmployee.MapGet("", ListAsync)
            .WithSummary("Dolgozó műszakkvóta-szabályainak listázása")
            .Produces<IReadOnlyList<EmployeeShiftQuotaRuleResponse>>()
            .ProducesStandardErrors();
        byEmployee.MapPost("", CreateAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozói műszakkvóta-szabály létrehozása")
            .Produces<EmployeeShiftQuotaRuleResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();

        var byId = endpoints.MapGroup("/api/admin/employee-shift-quota-rules")
            .WithTags("Employee shift quota rules")
            .RequireAuthorization(
                PermissionPolicies.For(ApplicationPermission.ManageEmployees));
        byId.MapPut("/{id:guid}", UpdateAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozói műszakkvóta-szabály módosítása")
            .Produces<EmployeeShiftQuotaRuleResponse>()
            .ProducesStandardErrors(includeConflict: true);
        byId.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .RequireAntiforgery()
            .WithSummary("Dolgozói műszakkvóta-szabály inaktiválása")
            .Produces<EmployeeShiftQuotaRuleResponse>()
            .ProducesStandardErrors(includeConflict: true);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid employeeId,
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

        if (!await EmployeeExistsAsync(
                employeeId,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var rules = await Query(dbContext)
            .Where(rule =>
                rule.OrganizationId == actor.OrganizationId &&
                rule.EmployeeId == employeeId &&
                (includeInactive == true || rule.IsActive))
            .OrderBy(rule => rule.Dimension)
            .ThenBy(rule => rule.Period)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(rules.Select(Map).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        Guid employeeId,
        CreateEmployeeShiftQuotaRuleRequest request,
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

        var errors = InputValidation.ValidateShiftQuota(
            request.Minimum,
            request.Target,
            request.Maximum);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        if (!await EmployeeExistsAsync(
                employeeId,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.NotFound();
        }

        var duplicate = await dbContext.EmployeeShiftQuotaRules.AnyAsync(
            rule =>
                rule.OrganizationId == actor.OrganizationId &&
                rule.EmployeeId == employeeId &&
                rule.Dimension == request.Dimension &&
                rule.Period == request.Period,
            cancellationToken);
        if (duplicate)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "SHIFT_QUOTA_ALREADY_EXISTS",
                    "Ehhez a dimenzióhoz és időszakhoz már tartozik szabály; módosítsa a meglévőt.",
                    "dimension")]);
        }

        var now = timeProvider.GetUtcNow();
        var rule = new EmployeeShiftQuotaRule
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            EmployeeId = employeeId,
            Dimension = request.Dimension,
            Period = request.Period,
            Minimum = request.Minimum,
            Target = request.Target,
            Maximum = request.Maximum,
            Severity = request.Severity,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.EmployeeShiftQuotaRules.Add(rule);
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "EmployeeShiftQuotaRule.Created",
            "EmployeeShiftQuotaRule",
            rule.Id.ToString(),
            httpContext.TraceIdentifier,
            "Dolgozói műszakkvóta-szabály létrehozva.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var created = await Query(dbContext)
            .SingleAsync(item => item.Id == rule.Id, cancellationToken);
        return Results.Created(
            $"/api/admin/employee-shift-quota-rules/{rule.Id}",
            Map(created));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateEmployeeShiftQuotaRuleRequest request,
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

        var errors = InputValidation.ValidateShiftQuota(
            request.Minimum,
            request.Target,
            request.Maximum);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var rule = await dbContext.EmployeeShiftQuotaRules
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (rule is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (rule.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A műszakkvóta-szabály a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        var duplicate = await dbContext.EmployeeShiftQuotaRules.AnyAsync(
            item =>
                item.Id != rule.Id &&
                item.OrganizationId == actor.OrganizationId &&
                item.EmployeeId == rule.EmployeeId &&
                item.Dimension == request.Dimension &&
                item.Period == request.Period,
            cancellationToken);
        if (duplicate)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "SHIFT_QUOTA_ALREADY_EXISTS",
                    "Ehhez a dimenzióhoz és időszakhoz már tartozik szabály.",
                    "dimension")]);
        }

        rule.Dimension = request.Dimension;
        rule.Period = request.Period;
        rule.Minimum = request.Minimum;
        rule.Target = request.Target;
        rule.Maximum = request.Maximum;
        rule.Severity = request.Severity;
        rule.IsActive = request.IsActive;
        rule.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "EmployeeShiftQuotaRule.Updated",
            "EmployeeShiftQuotaRule",
            rule.Id.ToString(),
            httpContext.TraceIdentifier,
            "Dolgozói műszakkvóta-szabály módosítva.");
        return await SaveAndMapAsync(rule, dbContext, cancellationToken);
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        DeactivateEmployeeShiftQuotaRuleRequest request,
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

        var rule = await dbContext.EmployeeShiftQuotaRules
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (rule is null)
        {
            return EndpointHelpers.NotFound();
        }

        if (rule.Version != request.ExpectedVersion)
        {
            return EndpointHelpers.Conflict(
                "A műszakkvóta-szabály a lekérés óta megváltozott. Töltse újra az adatokat.");
        }

        if (!rule.IsActive)
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "SHIFT_QUOTA_ALREADY_INACTIVE",
                    "A műszakkvóta-szabály már inaktív.",
                    "isActive")]);
        }

        rule.IsActive = false;
        rule.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "EmployeeShiftQuotaRule.Deactivated",
            "EmployeeShiftQuotaRule",
            rule.Id.ToString(),
            httpContext.TraceIdentifier,
            "Dolgozói műszakkvóta-szabály inaktiválva.");
        return await SaveAndMapAsync(rule, dbContext, cancellationToken);
    }

    private static async Task<IResult> SaveAndMapAsync(
        EmployeeShiftQuotaRule rule,
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
                "A műszakkvóta-szabály mentés közben megváltozott. Töltse újra az adatokat.");
        }

        var saved = await Query(dbContext)
            .SingleAsync(item => item.Id == rule.Id, cancellationToken);
        return Results.Ok(Map(saved));
    }

    private static IQueryable<EmployeeShiftQuotaRule> Query(PatikaDbContext dbContext) =>
        dbContext.EmployeeShiftQuotaRules
            .AsNoTracking()
            .Include(rule => rule.Employee);

    private static EmployeeShiftQuotaRuleResponse Map(EmployeeShiftQuotaRule rule) =>
        new(
            rule.Id,
            rule.EmployeeId,
            rule.Employee?.DisplayName ?? string.Empty,
            rule.Dimension,
            rule.Period,
            rule.Minimum,
            rule.Target,
            rule.Maximum,
            rule.Severity,
            rule.IsActive,
            rule.Version,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc);

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
}
