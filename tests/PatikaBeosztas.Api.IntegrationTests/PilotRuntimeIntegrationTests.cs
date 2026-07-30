using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class PilotRuntimeIntegrationTests
{
    [TestMethod]
    public async Task MigrationsAreIdempotentAndLeaveNoPendingMigration()
    {
        await using var application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString(),
            disableScheduleGenerationWorker: true);
        _ = application.Server;
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.MigrateAsync();

        Assert.IsFalse(
            (await dbContext.Database.GetPendingMigrationsAsync()).Any());
        Assert.IsTrue(
            (await dbContext.Database.GetAppliedMigrationsAsync()).Any());
    }

    [TestMethod]
    public async Task FirstAdminBootstrapIsAtomicIdempotentAndUsesStrongPassword()
    {
        await using var application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString(),
            disableScheduleGenerationWorker: true);
        _ = application.Server;
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();

        var bootstrapper =
            scope.ServiceProvider.GetRequiredService<PilotAdminBootstrapper>();
        const string password = "Pilot-Admin-Strong123!";
        var request = new PilotAdminBootstrapRequest(
            "Pilot Gyógyszertár",
            "pilot.admin@example.invalid",
            "Pilot Admin",
            password);

        var first = await bootstrapper.BootstrapAsync(request);
        var second = await bootstrapper.BootstrapAsync(request);

        Assert.IsTrue(first.Created);
        Assert.IsFalse(second.Created);
        Assert.AreEqual(first.OrganizationId, second.OrganizationId);
        Assert.AreEqual(first.UserId, second.UserId);
        Assert.AreEqual(1, await dbContext.Organizations.CountAsync());
        Assert.AreEqual(1, await dbContext.Users.CountAsync());
        Assert.AreEqual(
            Enum.GetValues<ApplicationPermission>().Length,
            await dbContext.UserPermissions.CountAsync());
        Assert.AreEqual(
            1,
            await dbContext.AuditLogs.CountAsync(log =>
                log.Action == "Pilot.AdminBootstrapped"));

        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(first.UserId.ToString());
        Assert.IsNotNull(user);
        Assert.IsTrue(await userManager.CheckPasswordAsync(user, password));
    }

    [TestMethod]
    public async Task ForwardedClientIpPartitionsLoginRateLimit()
    {
        await using var application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString(),
            disableScheduleGenerationWorker: true);
        await application.ResetAndSeedDatabaseAsync();
        using var client = application.CreateHttpsClient();
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>(
            "/api/auth/csrf");
        Assert.IsNotNull(csrf);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await SendFailedLoginAsync(
                client,
                csrf,
                "203.0.113.10");
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var limited = await SendFailedLoginAsync(
            client,
            csrf,
            "203.0.113.10");
        using var otherClient = await SendFailedLoginAsync(
            client,
            csrf,
            "203.0.113.11");

        Assert.AreEqual(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, otherClient.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendFailedLoginAsync(
        HttpClient client,
        CsrfTokenResponse csrf,
        string forwardedFor)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(
                    "nincs-ilyen-felhasznalo@example.invalid",
                    "Hibas-Jelszo123!"))
        };
        request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return await client.SendAsync(request);
    }
}
