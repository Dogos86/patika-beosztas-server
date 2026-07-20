using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.Endpoints;

internal static class EndpointHelpers
{
    public static async Task<ApplicationUser?> GetActorAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var idValue = userManager.GetUserId(httpContext.User);
        return Guid.TryParse(idValue, out var userId)
            ? await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    user => user.Id == userId && user.IsActive,
                    cancellationToken)
            : null;
    }

    public static IResult ValidationProblem(IReadOnlyCollection<ApiValidationError> errors) =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "A kérés üzleti validációja sikertelen.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "VALIDATION_FAILED",
                ["errors"] = errors
            });

    public static IResult Conflict(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Konkurens módosítás történt.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "CONCURRENCY_CONFLICT"
            });

    public static IResult NotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "A kért erőforrás nem található.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "NOT_FOUND"
            });

    public static IResult Unauthorized() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Sikertelen bejelentkezés.",
            detail: "A megadott hitelesítési adatok nem fogadhatók el.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "INVALID_CREDENTIALS"
            });

    public static RouteHandlerBuilder ProducesStandardErrors(
        this RouteHandlerBuilder builder,
        bool includeConflict = false)
    {
        builder.Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);
        if (includeConflict)
        {
            builder.Produces<ProblemDetails>(StatusCodes.Status409Conflict);
        }

        return builder;
    }

    public static RouteHandlerBuilder RequireAntiforgery(this RouteHandlerBuilder builder) =>
        builder
            .WithMetadata(AntiforgeryRequiredMetadata.Instance)
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
}

internal sealed class AntiforgeryRequiredMetadata
{
    public static AntiforgeryRequiredMetadata Instance { get; } = new();

    private AntiforgeryRequiredMetadata()
    {
    }
}

internal sealed class AntiforgeryEndpointFilter(
    IAntiforgery antiforgery)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Érvénytelen CSRF-token.",
                detail: "Kérjen új tokent az /api/auth/csrf végpontról.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "INVALID_CSRF_TOKEN"
                });
        }

        return await next(context);
    }
}
