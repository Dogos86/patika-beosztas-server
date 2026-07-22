using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using PatikaBeosztas.Api.Endpoints;
using PatikaBeosztas.Api.Security;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;
using PatikaBeosztas.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Patika Beosztás API";
        document.Info.Version = "0.3.0-phase2b";
        document.Info.Description =
            "Szervezethez kötött, cookie-authentikált gyógyszertári adminisztrációs, " +
            "munkapreferencia-, távollét-, nyitvatartás-, lefedettség- és munkaprofil-kezelő API; " +
            "a frontend HTTPS-en, az API-val azonos site alatt, credentials: include beállítással hívja; " +
            "a mutációkhoz CSRF-token és az optimista konkurenciát használó kéréseknél verzió szükséges.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["cookieAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "__Host-PatikaSession",
            Description = "HttpOnly, Secure Identity munkamenet-cookie."
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<AntiforgeryRequiredMetadata>().Any())
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-CSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "A GET /api/auth/csrf válaszában kapott request token.",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String
                }
            });
        }

        var authorization = metadata
            .OfType<IAuthorizeData>()
            .ToArray();
        if (authorization.Length == 0)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("cookieAuth", context.Document)] = []
        });
        var policies = authorization
            .Select(item => item.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (policies.Length > 0)
        {
            operation.Description =
                $"{operation.Description}{Environment.NewLine}Szükséges policy: {string.Join(", ", policies)}.";
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddInfrastructure();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-PatikaSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Hitelesítés szükséges.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "AUTHENTICATION_REQUIRED"
                })
            .ExecuteAsync(context.HttpContext),
        OnRedirectToAccessDenied = context => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Nincs jogosultság a művelethez.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "FORBIDDEN"
                })
            .ExecuteAsync(context.HttpContext)
    };
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-PatikaCsrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Enum.GetValues<ApplicationPermission>())
    {
        options.AddPolicy(
            PermissionPolicies.For(permission),
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
    }
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing") &&
    string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DefaultConnection")))
{
    throw new InvalidOperationException(
        "A ConnectionStrings:DefaultConnection konfiguráció kötelező.");
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapAuthEndpoints();
app.MapEmployeeEndpoints();
app.MapLocationEndpoints();
app.MapUserEndpoints();
app.MapWorkPreferenceEndpoints();
app.MapLeaveRequestEndpoints();
app.MapLocationOpeningEndpoints();
app.MapLocationShiftTemplateEndpoints();
app.MapCoverageRequirementEndpoints();
app.MapEmployeeCapabilityEndpoints();
app.MapEmployeeWorkProfileEndpoints();
app.MapEmployeeShiftQuotaRuleEndpoints();

await app.Services.InitializeDevelopmentDatabaseAsync(
    app.Environment,
    app.Configuration,
    app.Lifetime.ApplicationStopping);

app.Run();

public partial class Program;
