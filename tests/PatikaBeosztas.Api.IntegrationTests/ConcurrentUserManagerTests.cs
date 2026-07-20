using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
public sealed class ConcurrentUserManagerTests
{
    private static readonly Guid SecondAdminUserId =
        Guid.Parse("83000000-0000-0000-0000-000000000005");
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [TestMethod]
    public async Task ConcurrentAdminDeactivationLeavesOneActiveUserManager()
    {
        await using var application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString());
        await application.ResetAndSeedDatabaseAsync();
        await CreateSecondAdminAsync(application.Services);

        using var firstClient = application.CreateHttpsClient();
        using var secondClient = application.CreateHttpsClient();
        using var firstLogin = await LoginAsync(
            firstClient,
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var secondLogin = await LoginAsync(
            secondClient,
            "masodik-admin@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, firstLogin.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, secondLogin.StatusCode);

        var firstUser = await firstClient.GetFromJsonAsync<UserResponse>(
            $"/api/admin/users/{IntegrationTestData.AdminUserId}",
            JsonOptions);
        var secondUser = await secondClient.GetFromJsonAsync<UserResponse>(
            $"/api/admin/users/{SecondAdminUserId}",
            JsonOptions);
        Assert.IsNotNull(firstUser);
        Assert.IsNotNull(secondUser);

        using var firstRequest = await CreateStatusRequestAsync(
            firstClient,
            IntegrationTestData.AdminUserId,
            firstUser.Version);
        using var secondRequest = await CreateStatusRequestAsync(
            secondClient,
            SecondAdminUserId,
            secondUser.Version);
        var firstTask = firstClient.SendAsync(firstRequest);
        var secondTask = secondClient.SendAsync(secondRequest);
        var responses = await Task.WhenAll(firstTask, secondTask);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.AreEqual(
            1,
            responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(
            1,
            responses.Count(response =>
                response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict));

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var activeManagerCount = await dbContext.Users.CountAsync(user =>
            user.OrganizationId == IntegrationTestData.OrganizationId &&
            user.IsActive &&
            user.Permissions.Any(permission =>
                permission.OrganizationId == IntegrationTestData.OrganizationId &&
                permission.Permission == ApplicationPermission.ManageUsers));
        Assert.AreEqual(1, activeManagerCount);
    }

    private static async Task CreateSecondAdminAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            Id = SecondAdminUserId,
            OrganizationId = IntegrationTestData.OrganizationId,
            UserName = "masodik-admin@test.invalid",
            Email = "masodik-admin@test.invalid",
            EmailConfirmed = true,
            DisplayName = "Második Teszt Admin",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var result = await userManager.CreateAsync(user, IntegrationTestData.Password);
        Assert.IsTrue(result.Succeeded);
        dbContext.UserPermissions.Add(new UserPermission
        {
            OrganizationId = IntegrationTestData.OrganizationId,
            UserId = user.Id,
            Permission = ApplicationPermission.ManageUsers
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<HttpRequestMessage> CreateStatusRequestAsync(
        HttpClient client,
        Guid userId,
        uint expectedVersion)
    {
        var token = await GetCsrfTokenAsync(client);
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/admin/users/{userId}/status")
        {
            Content = JsonContent.Create(
                new UpdateUserStatusRequest(false, expectedVersion),
                options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        return request;
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(email, password),
                options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        return await client.SendAsync(request);
    }

    private static async Task<CsrfTokenResponse> GetCsrfTokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<CsrfTokenResponse>(
            "/api/auth/csrf",
            JsonOptions);
        Assert.IsNotNull(token);
        return token;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
