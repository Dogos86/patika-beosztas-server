using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Ok(new CsrfTokenResponse(
                    tokens.RequestToken ?? string.Empty,
                    tokens.HeaderName ?? "X-CSRF-TOKEN"));
            })
            .AllowAnonymous()
            .WithSummary("CSRF-token kérése állapotmódosító hívásokhoz")
            .Produces<CsrfTokenResponse>();

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("login")
            .RequireAntiforgery()
            .WithSummary("Bejelentkezés email-címmel és jelszóval")
            .Produces<SessionResponse>()
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesStandardErrors();

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .RequireAntiforgery()
            .WithSummary("Kijelentkezés")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesStandardErrors();

        group.MapGet("/session", GetSessionAsync)
            .RequireAuthorization()
            .WithSummary("Aktuális hitelesített munkamenet")
            .Produces<SessionResponse>()
            .ProducesStandardErrors();

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return EndpointHelpers.Unauthorized();
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return EndpointHelpers.Unauthorized();
        }

        var organizationIsActive = await dbContext.Organizations
            .AnyAsync(
                organization =>
                    organization.Id == user.OrganizationId &&
                    organization.IsActive,
                cancellationToken);
        if (!organizationIsActive)
        {
            return EndpointHelpers.Unauthorized();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return EndpointHelpers.Unauthorized();
        }

        auditWriter.Add(
            user.OrganizationId,
            user.Id,
            "Auth.Login",
            "ApplicationUser",
            user.Id.ToString(),
            httpContext.TraceIdentifier,
            "Sikeres bejelentkezés.");
        await dbContext.SaveChangesAsync(cancellationToken);

        var session = await BuildSessionAsync(user.Id, dbContext, cancellationToken);
        return session is null ? EndpointHelpers.Unauthorized() : Results.Ok(session);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PatikaDbContext dbContext,
        AuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        var actor = await EndpointHelpers.GetActorAsync(
            httpContext,
            userManager,
            dbContext,
            cancellationToken);
        if (actor is not null)
        {
            auditWriter.Add(
                actor.OrganizationId,
                actor.Id,
                "Auth.Logout",
                "ApplicationUser",
                actor.Id.ToString(),
                httpContext.TraceIdentifier,
                "Kijelentkezés.");
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetSessionAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var idValue = userManager.GetUserId(httpContext.User);
        if (!Guid.TryParse(idValue, out var userId))
        {
            return EndpointHelpers.Unauthorized();
        }

        var session = await BuildSessionAsync(userId, dbContext, cancellationToken);
        return session is null ? EndpointHelpers.Unauthorized() : Results.Ok(session);
    }

    internal static async Task<SessionResponse?> BuildSessionAsync(
        Guid userId,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(item => item.Organization)
            .Include(item => item.Employee)
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == userId &&
                    item.IsActive &&
                    item.Organization != null &&
                    item.Organization.IsActive,
                cancellationToken);
        if (user is null)
        {
            return null;
        }

        var linkedEmployee = user.Employee is null
            ? null
            : new LinkedEmployeeSummary(
                user.Employee.Id,
                user.Employee.DisplayName,
                user.Employee.ProfessionalRole,
                user.Employee.IsActive,
                user.Employee.IsSchedulable);
        return new SessionResponse(
            user.Id,
            user.OrganizationId,
            user.Organization!.Name,
            user.Organization.TimeZoneId,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.Permissions
                .Where(permission => permission.OrganizationId == user.OrganizationId)
                .Select(permission => permission.Permission)
                .Order()
                .ToArray(),
            linkedEmployee);
    }
}
