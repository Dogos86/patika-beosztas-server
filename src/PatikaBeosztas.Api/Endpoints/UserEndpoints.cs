using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/users")
            .WithTags("Users")
            .RequireAuthorization(PermissionPolicies.For(ApplicationPermission.ManageUsers));

        group.MapGet("", ListAsync)
            .WithSummary("Felhasználói fiókok szervezeten belüli listázása")
            .Produces<PagedResponse<UserResponse>>()
            .ProducesStandardErrors();
        group.MapPost("", CreateAsync)
            .RequireAntiforgery()
            .WithSummary("Helyi Identity-fiók létrehozása")
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesStandardErrors();
        group.MapPut("/{id:guid}/permissions", UpdatePermissionsAsync)
            .RequireAntiforgery()
            .WithSummary("Felhasználói permissionök cseréje")
            .Produces<UserResponse>()
            .ProducesStandardErrors();
        group.MapPut("/{id:guid}/employee-link", UpdateEmployeeLinkAsync)
            .RequireAntiforgery()
            .WithSummary("Felhasználó és dolgozó összekapcsolása vagy leválasztása")
            .Produces<UserResponse>()
            .ProducesStandardErrors();
        group.MapPut("/{id:guid}/status", UpdateStatusAsync)
            .RequireAntiforgery()
            .WithSummary("Felhasználói fiók aktiválása vagy deaktiválása")
            .Produces<UserResponse>()
            .ProducesStandardErrors();

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
        var query = dbContext.Users
            .AsNoTracking()
            .Where(user => user.OrganizationId == actor.OrganizationId);
        if (!includeInactive)
        {
            query = query.Where(user => user.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(user =>
                EF.Functions.ILike(user.DisplayName, pattern) ||
                (user.Email != null && EF.Functions.ILike(user.Email, pattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await IncludeUserDetails(query)
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<UserResponse>(
            users.Select(MapUser).ToArray(),
            page,
            pageSize,
            totalCount));
    }

    private static async Task<IResult> CreateAsync(
        CreateUserRequest request,
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

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return EndpointHelpers.ValidationProblem(errors);
        }

        var employeeError = await ValidateEmployeeLinkAsync(
            request.EmployeeId,
            null,
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        if (employeeError is not null)
        {
            return EndpointHelpers.ValidationProblem([employeeError]);
        }

        var now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = false,
            DisplayName = request.DisplayName.Trim(),
            IsActive = request.IsActive,
            EmployeeId = request.EmployeeId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var createResult = await userManager.CreateAsync(user, request.InitialPassword);
        if (!createResult.Succeeded)
        {
            var identityErrors = createResult.Errors
                .Select(error => new ApiValidationError(
                    error.Code,
                    error.Description,
                    error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase)
                        ? "initialPassword"
                        : "email"))
                .ToArray();
            await transaction.RollbackAsync(cancellationToken);
            return EndpointHelpers.ValidationProblem(identityErrors);
        }

        dbContext.UserPermissions.AddRange((request.Permissions ?? [])
            .Distinct()
            .Select(permission => new UserPermission
            {
                OrganizationId = actor.OrganizationId,
                UserId = user.Id,
                Permission = permission
            }));
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "User.Created",
            "ApplicationUser",
            user.Id.ToString(),
            httpContext.TraceIdentifier,
            "Felhasználói fiók létrehozva; jelszó nem került az auditba.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var responseUser = await IncludeUserDetails(dbContext.Users.AsNoTracking())
            .AsSplitQuery()
            .SingleAsync(item => item.Id == user.Id, cancellationToken);
        return Results.Created($"/api/admin/users/{user.Id}", MapUser(responseUser));
    }

    private static async Task<IResult> UpdatePermissionsAsync(
        Guid id,
        UpdateUserPermissionsRequest request,
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

        var user = await IncludeUserDetails(dbContext.Users)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (user is null)
        {
            return EndpointHelpers.NotFound();
        }

        var requestedPermissions = request.Permissions.Distinct().ToArray();
        var removesManageUsers =
            user.Permissions.Any(permission =>
                permission.Permission == ApplicationPermission.ManageUsers) &&
            !requestedPermissions.Contains(ApplicationPermission.ManageUsers);
        if (user.IsActive &&
            removesManageUsers &&
            !await HasOtherActiveUserManagerAsync(
                user.Id,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "LAST_ACTIVE_USER_MANAGER",
                    "A szervezetben legalább egy aktív ManageUsers jogosultságú felhasználónak maradnia kell.",
                    "permissions")]);
        }

        var requestedSet = requestedPermissions.ToHashSet();
        foreach (var existing in user.Permissions.ToArray())
        {
            if (!requestedSet.Remove(existing.Permission))
            {
                dbContext.UserPermissions.Remove(existing);
                user.Permissions.Remove(existing);
            }
        }

        foreach (var permission in requestedSet)
        {
            user.Permissions.Add(new UserPermission
            {
                OrganizationId = actor.OrganizationId,
                UserId = user.Id,
                Permission = permission
            });
        }

        user.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            "User.PermissionsUpdated",
            "ApplicationUser",
            user.Id.ToString(),
            httpContext.TraceIdentifier,
            "Felhasználói permissionök lecserélve.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(MapUser(user));
    }

    private static async Task<IResult> UpdateEmployeeLinkAsync(
        Guid id,
        UpdateUserEmployeeLinkRequest request,
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

        var user = await IncludeUserDetails(dbContext.Users)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (user is null)
        {
            return EndpointHelpers.NotFound();
        }

        var employeeError = await ValidateEmployeeLinkAsync(
            request.EmployeeId,
            user.Id,
            actor.OrganizationId,
            dbContext,
            cancellationToken);
        if (employeeError is not null)
        {
            return EndpointHelpers.ValidationProblem([employeeError]);
        }

        user.EmployeeId = request.EmployeeId;
        user.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            request.EmployeeId is null ? "User.EmployeeUnlinked" : "User.EmployeeLinked",
            "ApplicationUser",
            user.Id.ToString(),
            httpContext.TraceIdentifier,
            request.EmployeeId is null
                ? "Dolgozói kapcsolat megszüntetve."
                : "Dolgozói kapcsolat beállítva.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var employeeReference = dbContext.Entry(user).Reference(item => item.Employee);
        employeeReference.IsLoaded = false;
        await employeeReference.LoadAsync(cancellationToken);

        return Results.Ok(MapUser(user));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
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

        var user = await IncludeUserDetails(dbContext.Users)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OrganizationId == actor.OrganizationId,
                cancellationToken);
        if (user is null)
        {
            return EndpointHelpers.NotFound();
        }

        var hasManageUsers = user.Permissions.Any(permission =>
            permission.Permission == ApplicationPermission.ManageUsers);
        if (!request.IsActive &&
            user.IsActive &&
            hasManageUsers &&
            !await HasOtherActiveUserManagerAsync(
                user.Id,
                actor.OrganizationId,
                dbContext,
                cancellationToken))
        {
            return EndpointHelpers.ValidationProblem(
                [new ApiValidationError(
                    "LAST_ACTIVE_USER_MANAGER",
                    "Az utolsó aktív ManageUsers jogosultságú felhasználó nem deaktiválható.",
                    "isActive")]);
        }

        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = timeProvider.GetUtcNow();
        auditWriter.Add(
            actor.OrganizationId,
            actor.Id,
            request.IsActive ? "User.Activated" : "User.Deactivated",
            "ApplicationUser",
            user.Id.ToString(),
            httpContext.TraceIdentifier,
            request.IsActive ? "Felhasználó aktiválva." : "Felhasználó deaktiválva.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(MapUser(user));
    }

    private static IQueryable<ApplicationUser> IncludeUserDetails(
        IQueryable<ApplicationUser> query) =>
        query
            .Include(user => user.Employee)
            .Include(user => user.Permissions);

    private static UserResponse MapUser(ApplicationUser user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.IsActive,
            user.Employee is null
                ? null
                : new LinkedEmployeeSummary(
                    user.Employee.Id,
                    user.Employee.DisplayName,
                    user.Employee.ProfessionalRole,
                    user.Employee.IsActive,
                    user.Employee.IsSchedulable),
            user.Permissions
                .Where(permission => permission.OrganizationId == user.OrganizationId)
                .Select(permission => permission.Permission)
                .Order()
                .ToArray(),
            user.CreatedAtUtc,
            user.UpdatedAtUtc);

    private static List<ApiValidationError> ValidateCreateRequest(
        CreateUserRequest request)
    {
        var errors = new List<ApiValidationError>();
        if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            errors.Add(new("INVALID_EMAIL", "Érvényes email-cím szükséges.", "email"));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            request.DisplayName.Trim().Length > 100)
        {
            errors.Add(new(
                "INVALID_DISPLAY_NAME",
                "A megjelenítési név 1–100 karakter hosszú legyen.",
                "displayName"));
        }

        if (string.IsNullOrWhiteSpace(request.InitialPassword))
        {
            errors.Add(new(
                "PASSWORD_REQUIRED",
                "A kezdeti jelszó megadása kötelező.",
                "initialPassword"));
        }

        return errors;
    }

    private static async Task<ApiValidationError?> ValidateEmployeeLinkAsync(
        Guid? employeeId,
        Guid? userId,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (employeeId is null)
        {
            return null;
        }

        var employeeExists = await dbContext.Employees.AnyAsync(
            employee =>
                employee.Id == employeeId &&
                employee.OrganizationId == organizationId,
            cancellationToken);
        if (!employeeExists)
        {
            return new ApiValidationError(
                "EMPLOYEE_OUTSIDE_ORGANIZATION",
                "A kapcsolandó dolgozó nem található az aktuális szervezetben.",
                "employeeId");
        }

        var linkedElsewhere = await dbContext.Users.AnyAsync(
            user =>
                user.OrganizationId == organizationId &&
                user.EmployeeId == employeeId &&
                user.Id != userId,
            cancellationToken);
        return linkedElsewhere
            ? new ApiValidationError(
                "EMPLOYEE_ALREADY_LINKED",
                "A dolgozó már másik felhasználóhoz kapcsolódik.",
                "employeeId")
            : null;
    }

    private static Task<bool> HasOtherActiveUserManagerAsync(
        Guid excludedUserId,
        Guid organizationId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            user =>
                user.OrganizationId == organizationId &&
                user.Id != excludedUserId &&
                user.IsActive &&
                user.Permissions.Any(permission =>
                    permission.OrganizationId == organizationId &&
                    permission.Permission == ApplicationPermission.ManageUsers),
            cancellationToken);
}
