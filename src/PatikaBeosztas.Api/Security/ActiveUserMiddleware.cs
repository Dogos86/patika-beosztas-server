using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Security;

public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        PatikaDbContext dbContext,
        SignInManager<ApplicationUser> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = signInManager.UserManager.GetUserId(context.User);
            var isActive = Guid.TryParse(userId, out var parsedUserId) &&
                await dbContext.Users
                    .Where(user => user.Id == parsedUserId && user.IsActive)
                    .AnyAsync(user => user.Organization != null && user.Organization.IsActive);
            if (!isActive)
            {
                await signInManager.SignOutAsync();
                await Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "A munkamenet nem használható.",
                    detail: "A felhasználó vagy a szervezet inaktív.")
                    .ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }
}
