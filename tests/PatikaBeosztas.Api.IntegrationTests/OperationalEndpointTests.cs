using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class OperationalEndpointTests
{
    [TestMethod]
    public async Task HealthEndpointReturnsHealthyResponse()
    {
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", body);
    }

    [TestMethod]
    public async Task OpenApiDocumentIsAvailableWithoutBusinessEndpoints()
    {
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(document.RootElement.TryGetProperty("openapi", out _));
        Assert.IsTrue(document.RootElement.TryGetProperty("paths", out var paths));
        Assert.IsFalse(paths.EnumerateObject().Any(path =>
            path.Name.StartsWith("/api/", StringComparison.Ordinal)));
    }
}

