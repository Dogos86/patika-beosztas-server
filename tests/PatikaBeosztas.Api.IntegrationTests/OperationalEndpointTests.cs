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
    public async Task OpenApiDocumentsPhaseOneEndpointsAndCookieSecurity()
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
