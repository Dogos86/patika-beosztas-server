using System.Net;
using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
[DoNotParallelize]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "MSTest invokes the asynchronous TestCleanup method after every test.")]
public sealed class SecurityBoundaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private ApiFactory application = null!;
    private HttpClient client = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        application = new ApiFactory(PostgreSqlTestEnvironment.GetConnectionString());
        client = application.CreateHttpsClient();
        await application.ResetAndSeedDatabaseAsync();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        if (client is not null)
        {
            client.Dispose();
        }

        if (application is not null)
        {
            await application.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task LoginSessionLogoutAndInvalidPasswordHaveExpectedBehavior()
    {
        using var invalidResponse = await LoginAsync(
            "admin@test.invalid",
            "hibas-jelszo");
        Assert.AreEqual(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);

        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var loginSession = await loginResponse.Content.ReadFromJsonAsync<SessionResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.IsNotNull(loginSession);
        Assert.AreEqual(IntegrationTestData.OrganizationId, loginSession.OrganizationId);
        Assert.AreEqual("Első teszt szervezet", loginSession.OrganizationName);
        Assert.AreEqual("Europe/Budapest", loginSession.OrganizationTimeZoneId);
        Assert.AreEqual(IntegrationTestData.AdminEmployeeId, loginSession.LinkedEmployee?.Id);
        Assert.IsTrue(loginSession.LinkedEmployee?.IsSchedulable);
        Assert.Contains(ApplicationPermission.ManageUsers, loginSession.Permissions);

        using var sessionResponse = await client.GetAsync("/api/auth/session");
        Assert.AreEqual(HttpStatusCode.OK, sessionResponse.StatusCode);

        using var logoutResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/auth/logout",
            body: null);
        Assert.AreEqual(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var endedSessionResponse = await client.GetAsync("/api/auth/session");
        Assert.AreEqual(HttpStatusCode.Unauthorized, endedSessionResponse.StatusCode);
    }

    [TestMethod]
    public async Task InactiveUserAndInactiveOrganizationCannotLogin()
    {
        using var inactiveUserResponse = await LoginAsync(
            "inaktiv@test.invalid",
            IntegrationTestData.Password);
        using var inactiveOrganizationResponse = await LoginAsync(
            "inaktiv-szervezet@test.invalid",
            IntegrationTestData.Password);

        Assert.AreEqual(HttpStatusCode.Unauthorized, inactiveUserResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, inactiveOrganizationResponse.StatusCode);
    }

    [TestMethod]
    public async Task CredentialCookieDoesNotBypassCsrfProtection()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);
        var sessionCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-PatikaSession=", StringComparison.Ordinal));
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", sessionCookie, StringComparison.OrdinalIgnoreCase);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/employees",
            new CreateEmployeeRequest(
                "CSRF Teszt",
                "CSRF Teszt",
                ProfessionalRole.Assistant,
                true,
                true,
                false,
                false,
                null,
                null,
                null,
                null,
                [],
                [],
                []),
            JsonOptions);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "INVALID_CSRF_TOKEN",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PublicIdentityValidationMessagesAreHungarian()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var weakPasswordResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/users",
            new CreateUserRequest(
                "uj-felhasznalo@test.invalid",
                "Új Felhasználó",
                "gyenge",
                null,
                [],
                true));
        var weakPasswordBody = await weakPasswordResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(
            HttpStatusCode.UnprocessableEntity,
            weakPasswordResponse.StatusCode);
        Assert.Contains("A jelszónak", weakPasswordBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Passwords must", weakPasswordBody, StringComparison.Ordinal);

        using var duplicateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/users",
            new CreateUserRequest(
                "admin@test.invalid",
                "Másik Admin",
                IntegrationTestData.Password,
                null,
                [],
                true));
        var duplicateBody = await duplicateResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, duplicateResponse.StatusCode);
        Assert.Contains(
            "már létezik",
            duplicateBody,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "is already taken",
            duplicateBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task RegularEmployeeCannotUseAdminEndpoint()
    {
        using var loginResponse = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);

        using var response = await client.GetAsync("/api/admin/employees");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task AdminCanUseEmployeeAndLocationEndpoints()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);

        using var employeeResponse = await client.GetAsync("/api/admin/employees");
        using var locationResponse = await client.GetAsync("/api/admin/locations");

        Assert.AreEqual(HttpStatusCode.OK, employeeResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, locationResponse.StatusCode);
    }

    [TestMethod]
    public async Task OtherOrganizationRecordsAreNeitherReadableNorWritable()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);

        using var employeeResponse = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}");
        using var locationResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.OtherLocationId}",
            new UpdateLocationRequest(
                "Megváltoztatott név",
                LocationType.Branch,
                null,
                false,
                1));
        using var userResponse = await client.GetAsync(
            $"/api/admin/users/{IntegrationTestData.InactiveOrganizationUserId}");

        Assert.AreEqual(HttpStatusCode.NotFound, employeeResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, locationResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, userResponse.StatusCode);
    }

    [TestMethod]
    public async Task ClientSuppliedOrganizationAndActorIdsCannotOverrideSession()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);

        var payload = $$"""
            {
              "organizationId": "{{IntegrationTestData.OtherOrganizationId}}",
              "actorUserId": "{{IntegrationTestData.RegularUserId}}",
              "createdByUserId": "{{IntegrationTestData.RegularUserId}}",
              "fullName": "Új Teszt Dolgozó",
              "displayName": "Új Dolgozó",
              "professionalRole": "Assistant",
              "isActive": true,
              "isSchedulable": true,
              "includeInAutoFill": true,
              "countsAsPharmacist": false,
              "monthlyMinutesLimit": 9600,
              "maxDailyMinutes": 720,
              "locations": [],
              "timeWindows": [],
              "allowedTimeTypes": ["Work"]
            }
            """;
        using var response = await SendRawJsonWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/employees",
            payload);
        var employee = await response.Content.ReadFromJsonAsync<EmployeeResponse>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(employee);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var storedEmployee = await dbContext.Employees.SingleAsync(
            item => item.Id == employee.Id);
        var audit = await dbContext.AuditLogs.SingleAsync(
            item => item.EntityId == employee.Id.ToString());
        Assert.AreEqual(IntegrationTestData.OrganizationId, storedEmployee.OrganizationId);
        Assert.AreEqual(IntegrationTestData.AdminUserId, audit.ActorUserId);
    }

    [TestMethod]
    public async Task EmployeeValidationRejectsInvalidTimeWindow()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var request = new CreateEmployeeRequest(
            "Hibás Ablak",
            "Hibás Ablak",
            ProfessionalRole.Assistant,
            true,
            true,
            true,
            false,
            null,
            null,
            null,
            null,
            [],
            [
                new EmployeeTimeWindowRequest(
                    DayOfWeek.Monday,
                    new TimeOnly(12, 0),
                    new TimeOnly(8, 0),
                    EmployeeTimeWindowType.Forbidden)
            ],
            [TimeType.Work]);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/employees",
            request);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(
            "TIME_WINDOW_ORDER",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task EmployeeValidationRejectsLimitsFutureBirthDateAndInvalidAutofillFlags()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var request = new CreateEmployeeRequest(
            "Érvénytelen Dolgozó",
            "Érvénytelen",
            ProfessionalRole.Assistant,
            false,
            false,
            true,
            false,
            0,
            1_441,
            new DateOnly(9999, 12, 31),
            null,
            [],
            [],
            []);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/employees",
            request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("MONTHLY_MINUTES_OUT_OF_RANGE", body, StringComparison.Ordinal);
        Assert.Contains("MAX_DAILY_MINUTES_OUT_OF_RANGE", body, StringComparison.Ordinal);
        Assert.Contains("BIRTH_DATE_IN_FUTURE", body, StringComparison.Ordinal);
        Assert.Contains(
            "AUTOFILL_REQUIRES_ACTIVE_SCHEDULABLE_EMPLOYEE",
            body,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task EmployeeStringsAreNormalizedBeforePersistenceAndResponse()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var request = new CreateEmployeeRequest(
            "  Normalizált Teljes Név  ",
            "  Normalizált Név  ",
            ProfessionalRole.Assistant,
            true,
            true,
            false,
            false,
            null,
            null,
            null,
            "   ",
            [],
            [],
            []);

        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/employees",
            request);
        var employee = await response.Content.ReadFromJsonAsync<EmployeeResponse>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(employee);
        Assert.AreEqual("Normalizált Teljes Név", employee.FullName);
        Assert.AreEqual("Normalizált Név", employee.DisplayName);
        Assert.IsNull(employee.ExternalPayrollId);
    }

    [TestMethod]
    public async Task IntegerEmployeeEnumsAreRejected()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var payload = """
            {
              "fullName": "Integer Enum Teszt",
              "displayName": "Integer Enum Teszt",
              "professionalRole": 3,
              "isActive": true,
              "isSchedulable": true,
              "includeInAutoFill": false,
              "countsAsPharmacist": false,
              "locations": [],
              "timeWindows": [],
              "allowedTimeTypes": [0]
            }
            """;

        using var response = await SendRawJsonWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/employees",
            payload);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task EmployeeCreateUpdateAndConcurrencyConflictWork()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var createRequest = new CreateEmployeeRequest(
            "Módosítható Dolgozó",
            "Módosítható",
            ProfessionalRole.Pharmacist,
            true,
            true,
            true,
            true,
            10_080,
            720,
            null,
            null,
            [],
            [],
            [TimeType.Work]);
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/employees",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EmployeeResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.IsNotNull(created);

        var updateRequest = new UpdateEmployeeRequest(
            created.FullName,
            "Frissített név",
            created.ProfessionalRole,
            created.IsActive,
            created.IsSchedulable,
            created.IncludeInAutoFill,
            created.CountsAsPharmacist,
            created.MonthlyMinutesLimit,
            created.MaxDailyMinutes,
            created.BirthDate,
            created.ExternalPayrollId,
            [],
            [],
            created.AllowedTimeTypes,
            created.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{created.Id}",
            updateRequest);
        var updated = await updateResponse.Content.ReadFromJsonAsync<EmployeeResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.IsNotNull(updated);
        Assert.AreEqual("Frissített név", updated.DisplayName);
        Assert.AreNotEqual(created.Version, updated.Version);

        using var conflictResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{created.Id}",
            updateRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [TestMethod]
    public async Task LocationCrudDeactivationConcurrencyAndAuditWork()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/locations",
            new CreateLocationRequest(
                "Új központ",
                LocationType.Central,
                "Teszt cím",
                true));
        var created = await createResponse.Content.ReadFromJsonAsync<LocationResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.IsNotNull(created);

        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{created.Id}",
            new UpdateLocationRequest(
                created.Name,
                created.Type,
                created.Address,
                false,
                created.Version));
        var updated = await updateResponse.Content.ReadFromJsonAsync<LocationResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.IsNotNull(updated);
        Assert.IsFalse(updated.IsActive);
        Assert.AreNotEqual(created.Version, updated.Version);

        using var conflictResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{created.Id}",
            new UpdateLocationRequest(
                "Elavult módosítás",
                LocationType.Branch,
                null,
                true,
                created.Version));
        Assert.AreEqual(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(
            item =>
                item.EntityId == created.Id.ToString() &&
                item.Action == "Location.Deactivated"));
    }

    [TestMethod]
    public async Task PermissionUpdateTakesEffectAndLastUserManagerIsProtected()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var regularUser = await client.GetFromJsonAsync<UserResponse>(
            $"/api/admin/users/{IntegrationTestData.RegularUserId}",
            JsonOptions);
        Assert.IsNotNull(regularUser);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/users/{IntegrationTestData.RegularUserId}/permissions",
            new UpdateUserPermissionsRequest(
                [
                    ApplicationPermission.ViewOwnSchedule,
                    ApplicationPermission.ManageEmployees
                ],
                regularUser.Version));
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        using var staleVersionResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/users/{IntegrationTestData.RegularUserId}/permissions",
            new UpdateUserPermissionsRequest(
                [
                    ApplicationPermission.ViewOwnSchedule,
                    ApplicationPermission.ManageEmployees
                ],
                regularUser.Version));
        Assert.AreEqual(HttpStatusCode.Conflict, staleVersionResponse.StatusCode);
        Assert.Contains(
            "CONCURRENCY_CONFLICT",
            await staleVersionResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var adminUser = await client.GetFromJsonAsync<UserResponse>(
            $"/api/admin/users/{IntegrationTestData.AdminUserId}",
            JsonOptions);
        Assert.IsNotNull(adminUser);
        using var lastManagerResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/users/{IntegrationTestData.AdminUserId}/permissions",
            new UpdateUserPermissionsRequest(
                [
                    ApplicationPermission.ManageEmployees,
                    ApplicationPermission.ManageLocations
                ],
                adminUser.Version));
        Assert.AreEqual(
            HttpStatusCode.UnprocessableEntity,
            lastManagerResponse.StatusCode);

        using var regularClient = application.CreateHttpsClient();
        using var regularLoginResponse = await LoginAsync(
            regularClient,
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        using var employeeResponse = await regularClient.GetAsync("/api/admin/employees");
        Assert.AreEqual(HttpStatusCode.OK, employeeResponse.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(
            item =>
                item.EntityId == IntegrationTestData.RegularUserId.ToString() &&
                item.Action == "User.PermissionsUpdated"));
    }

    [TestMethod]
    public async Task UserEmployeeLinkAndStatusCanBeManagedWithinOrganization()
    {
        using var loginResponse = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        var inactiveUser = await client.GetFromJsonAsync<UserResponse>(
            $"/api/admin/users/{IntegrationTestData.InactiveUserId}",
            JsonOptions);
        Assert.IsNotNull(inactiveUser);
        using var linkResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/users/{IntegrationTestData.InactiveUserId}/employee-link",
            new UpdateUserEmployeeLinkRequest(
                IntegrationTestData.OfflineEmployeeId,
                inactiveUser.Version));
        var linkedUser = await linkResponse.Content.ReadFromJsonAsync<UserResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, linkResponse.StatusCode);
        Assert.IsNotNull(linkedUser);
        Assert.AreEqual(
            IntegrationTestData.OfflineEmployeeId,
            linkedUser.LinkedEmployee?.Id);

        using var statusResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/users/{IntegrationTestData.InactiveUserId}/status",
            new UpdateUserStatusRequest(true, linkedUser.Version));
        var activeUser = await statusResponse.Content.ReadFromJsonAsync<UserResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.IsNotNull(activeUser);
        Assert.IsTrue(activeUser.IsActive);

        using var crossTenantLinkResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/users/{IntegrationTestData.InactiveUserId}/employee-link",
            new UpdateUserEmployeeLinkRequest(
                IntegrationTestData.OtherEmployeeId,
                activeUser.Version));
        Assert.AreEqual(
            HttpStatusCode.UnprocessableEntity,
            crossTenantLinkResponse.StatusCode);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        LoginAsync(client, email, password);

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient targetClient,
        string email,
        string password)
    {
        var token = await GetCsrfTokenAsync(targetClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(email, password),
                options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        return await targetClient.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        object? body)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(method, path)
        {
            Content = body is null
                ? new StringContent(string.Empty, Encoding.UTF8, "application/json")
                : JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendRawJsonWithCsrfAsync(
        HttpMethod method,
        string path,
        string json)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        return await client.SendAsync(request);
    }

    private static async Task<CsrfTokenResponse> GetCsrfTokenAsync(HttpClient targetClient)
    {
        var token = await targetClient.GetFromJsonAsync<CsrfTokenResponse>(
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
