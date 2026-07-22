using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class OperationalEndpointTests
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=1;Database=unused;Username=unused;Password=unused";

    [TestMethod]
    public async Task HealthEndpointReturnsHealthyResponse()
    {
        await using var application = new ApiFactory(UnusedConnectionString);
        using var client = application.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", body);
    }

    [TestMethod]
    public async Task OpenApiDocumentsPhase2BEndpointsContractsAndCookieSecurity()
    {
        await using var application = new ApiFactory(UnusedConnectionString);
        using var client = application.CreateHttpsClient();

        using var response = await client.GetAsync(
            new Uri("/openapi/v1.json", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(paths.TryGetProperty("/api/auth/session", out _));
        Assert.IsTrue(paths.TryGetProperty("/api/admin/employees", out _));
        Assert.IsTrue(paths.TryGetProperty("/api/admin/locations", out _));
        Assert.IsTrue(paths.TryGetProperty("/api/admin/users", out _));
        Assert.IsTrue(paths.TryGetProperty("/api/admin/users/{id}", out _));
        var phase2APaths = new[]
        {
            "/api/me/work-preferences",
            "/api/me/work-preferences/{id}",
            "/api/me/work-preferences/{id}/deactivate",
            "/api/admin/employees/{employeeId}/work-preferences",
            "/api/admin/work-preferences/{id}",
            "/api/admin/work-preferences/{id}/deactivate",
            "/api/me/leave-requests",
            "/api/me/leave-requests/{id}",
            "/api/me/leave-requests/{id}/submit",
            "/api/me/leave-requests/{id}/withdraw",
            "/api/admin/leave-requests",
            "/api/admin/leave-requests/{id}",
            "/api/admin/employees/{employeeId}/leave-requests",
            "/api/admin/leave-requests/{id}/submit",
            "/api/admin/leave-requests/{id}/record",
            "/api/admin/leave-requests/{id}/close",
            "/api/admin/leave-requests/{id}/decision",
            "/api/admin/leave-requests/{id}/cancel"
        };
        foreach (var path in phase2APaths)
        {
            Assert.IsTrue(paths.TryGetProperty(path, out _), $"Hiányzó OpenAPI útvonal: {path}");
        }

        var phase2BPaths = new[]
        {
            "/api/admin/locations/{locationId}/weekly-opening",
            "/api/admin/locations/{locationId}/shift-templates",
            "/api/admin/location-shift-templates/{id}",
            "/api/admin/location-shift-templates/{id}/deactivate",
            "/api/admin/coverage-requirements",
            "/api/admin/coverage-requirements/{id}",
            "/api/admin/coverage-requirements/{id}/deactivate",
            "/api/admin/employees/{employeeId}/capabilities",
            "/api/admin/employees/{employeeId}/work-profile",
            "/api/admin/employees/{employeeId}/shift-quota-rules",
            "/api/admin/employee-shift-quota-rules/{id}",
            "/api/admin/employee-shift-quota-rules/{id}/deactivate"
        };
        foreach (var path in phase2BPaths)
        {
            Assert.IsTrue(paths.TryGetProperty(path, out _), $"Hiányzó OpenAPI útvonal: {path}");
        }

        Assert.AreEqual(
            "0.3.0-phase2b",
            root.GetProperty("info").GetProperty("version").GetString());
        Assert.AreEqual(
            "__Host-PatikaSession",
            schemes.GetProperty("cookieAuth").GetProperty("name").GetString());
        Assert.IsTrue(
            paths.GetProperty("/api/admin/employees")
                .GetProperty("get")
                .GetProperty("security")
                .GetArrayLength() > 0);
        Assert.IsTrue(
            paths.GetProperty("/api/auth/login")
                .GetProperty("post")
                .GetProperty("parameters")
                .EnumerateArray()
                .Any(parameter =>
                    parameter.GetProperty("name").GetString() == "X-CSRF-TOKEN"));
        Assert.Contains("PharmacyManager", body, StringComparison.Ordinal);
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.IsTrue(
            schemas.GetProperty("UserResponse")
                .GetProperty("properties")
                .TryGetProperty("version", out _));
        var sessionProperties = schemas.GetProperty("SessionResponse")
            .GetProperty("properties");
        Assert.IsTrue(sessionProperties.TryGetProperty("organizationName", out _));
        Assert.IsTrue(sessionProperties.TryGetProperty("organizationTimeZoneId", out _));
        Assert.IsFalse(sessionProperties.TryGetProperty("role", out _));
        Assert.IsTrue(
            schemas.GetProperty("UpdateUserPermissionsRequest")
                .GetProperty("properties")
                .TryGetProperty("expectedVersion", out _));
        Assert.IsTrue(
            schemas.GetProperty("WorkPreferenceResponse")
                .GetProperty("properties")
                .TryGetProperty("version", out _));
        Assert.IsTrue(
            schemas.GetProperty("LeaveRequestResponse")
                .GetProperty("properties")
                .TryGetProperty("statusHistory", out _));
        Assert.IsFalse(
            schemas.GetProperty("CreateWorkPreferenceRequest")
                .GetProperty("properties")
                .TryGetProperty("employeeId", out _));
        Assert.IsFalse(
            schemas.GetProperty("CreateLeaveRequest")
                .GetProperty("properties")
                .TryGetProperty("employeeId", out _));
        Assert.IsFalse(
            schemas.GetProperty("CreateLeaveRequest")
                .GetProperty("properties")
                .TryGetProperty("diagnosis", out _));
        Assert.IsFalse(
            schemas.GetProperty("LeaveRequestResponse")
                .GetProperty("properties")
                .TryGetProperty("diagnosis", out _));
        Assert.IsTrue(
            paths.GetProperty("/api/me/leave-requests")
                .GetProperty("post")
                .GetProperty("parameters")
                .EnumerateArray()
                .Any(parameter =>
                    parameter.GetProperty("name").GetString() == "X-CSRF-TOKEN"));
        Assert.IsTrue(
            paths.GetProperty("/api/admin/locations/{locationId}/weekly-opening")
                .GetProperty("put")
                .GetProperty("parameters")
                .EnumerateArray()
                .Any(parameter =>
                    parameter.GetProperty("name").GetString() == "X-CSRF-TOKEN"));
        Assert.IsTrue(
            schemas.GetProperty("LocationWeeklyOpeningResponse")
                .GetProperty("properties")
                .TryGetProperty("version", out _));
        Assert.IsTrue(
            schemas.GetProperty("EmployeeWorkProfileResponse")
                .GetProperty("properties")
                .TryGetProperty("maximumDailyMinutes", out _));
        Assert.Contains("StaffingCapability", body, StringComparison.Ordinal);
        Assert.Contains("ManageCoverageRules", body, StringComparison.Ordinal);
        Assert.DoesNotContain("WeekendWork", body, StringComparison.Ordinal);
        Assert.Contains("ManageWorkPreferences", body, StringComparison.Ordinal);
        Assert.Contains("RecordLeaveForOthers", body, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnos", body, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task AnonymousAdminRequestReturnsProblemDetails()
    {
        await using var application = new ApiFactory(UnusedConnectionString);
        using var client = application.CreateHttpsClient();

        using var response = await client.GetAsync("/api/admin/employees");
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("AUTHENTICATION_REQUIRED", body, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ConfiguredFrontendOriginReceivesCredentialedCorsPreflight()
    {
        await using var application = new ApiFactory(UnusedConnectionString);
        using var client = application.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/admin/employees");
        request.Headers.Add("Origin", "https://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add(
            "Access-Control-Request-Headers",
            "content-type,x-csrf-token");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(
            "https://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.AreEqual(
            "true",
            response.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        using var rejectedRequest = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/admin/employees");
        rejectedRequest.Headers.Add("Origin", "https://nem-engedelyezett.example");
        rejectedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        using var rejectedResponse = await client.SendAsync(rejectedRequest);
        Assert.IsFalse(
            rejectedResponse.Headers.TryGetValues(
                "Access-Control-Allow-Origin",
                out _));
    }

    [TestMethod]
    public async Task HungarianIdentityErrorDescriberIsRegistered()
    {
        await using var application = new ApiFactory(UnusedConnectionString);
        _ = application.Server;
        await using var scope = application.Services.CreateAsyncScope();
        var describer = scope.ServiceProvider.GetRequiredService<IdentityErrorDescriber>();

        Assert.Contains(
            "már létezik",
            describer.DuplicateEmail("teszt@example.invalid").Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "érvénytelen vagy lejárt",
            describer.InvalidToken().Description,
            StringComparison.OrdinalIgnoreCase);
    }
}
