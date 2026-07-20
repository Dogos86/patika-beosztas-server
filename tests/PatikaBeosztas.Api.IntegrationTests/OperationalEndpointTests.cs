using System.Net;
using System.Text.Json;
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
}
