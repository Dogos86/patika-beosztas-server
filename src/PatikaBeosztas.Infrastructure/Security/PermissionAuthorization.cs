using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Infrastructure.Security;

public sealed record PermissionRequirement(ApplicationPermission Permission)
    : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(
    PatikaDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var userId))
        {
            return;
        }

        var isAllowed = await dbContext.Users
            .Where(user => user.Id == userId && user.IsActive)
            .Where(user => user.Organization != null && user.Organization.IsActive)
            .AnyAsync(user => user.Permissions.Any(permission =>
                permission.OrganizationId == user.OrganizationId &&
                permission.Permission == requirement.Permission));

        if (isAllowed)
        {
            context.Succeed(requirement);
        }
    }
}
