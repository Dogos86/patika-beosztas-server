using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public sealed class Phase2BRuntimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationJson.Options;

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
    public async Task WeeklyOpeningSupportsAllModesMultipleIntervalsConcurrencyAndAudit()
    {
        await LoginAdminAsync();
        var createRequest = new UpdateLocationWeeklyOpeningRequest(
            OpeningWeek(
                new OpeningDayRequest(
                    DayOfWeek.Monday,
                    OpeningDayMode.Open24Hours,
                    []),
                new OpeningDayRequest(
                    DayOfWeek.Tuesday,
                    OpeningDayMode.CustomIntervals,
                    [
                        new OpeningIntervalRequest(
                            new TimeOnly(0, 0),
                            new TimeOnly(12, 0)),
                        new OpeningIntervalRequest(
                            new TimeOnly(13, 0),
                            null)
                    ])),
            ExpectedVersion: null);

        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening",
            createRequest);
        var created = await ReadAsync<LocationWeeklyOpeningResponse>(createResponse);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.AreEqual(OpeningDayMode.Open24Hours, Day(created, DayOfWeek.Monday).Mode);
        Assert.HasCount(2, Day(created, DayOfWeek.Tuesday).Intervals);
        Assert.IsNull(Day(created, DayOfWeek.Tuesday).Intervals[1].EndTime);

        using var currentResponse = await client.GetAsync(
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening");
        var current = await ReadAsync<LocationWeeklyOpeningResponse>(currentResponse);
        Assert.AreEqual(created.Version, current.Version);

        var updateRequest = createRequest with
        {
            Days = OpeningWeek(
                new OpeningDayRequest(
                    DayOfWeek.Monday,
                    OpeningDayMode.CustomIntervals,
                    [new OpeningIntervalRequest(
                        new TimeOnly(8, 0),
                        new TimeOnly(18, 0))])),
            ExpectedVersion = current.Version
        };
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening",
            updateRequest);
        var updated = await ReadAsync<LocationWeeklyOpeningResponse>(updateResponse);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.AreNotEqual(created.Version, updated.Version);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening",
            updateRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Contains(
            "CONCURRENCY_CONFLICT",
            await staleResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var getResponse = await client.GetAsync(
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            2,
            await dbContext.AuditLogs.CountAsync(log =>
                log.EntityId == created.Id.ToString()));
    }

    [TestMethod]
    public async Task OpeningValidationCsrfTenantBoundaryAndInactiveLocationAreEnforced()
    {
        await LoginAdminAsync();
        var overlap = new UpdateLocationWeeklyOpeningRequest(
            OpeningWeek(new OpeningDayRequest(
                DayOfWeek.Monday,
                OpeningDayMode.CustomIntervals,
                [
                    new OpeningIntervalRequest(
                        new TimeOnly(8, 0),
                        new TimeOnly(14, 0)),
                    new OpeningIntervalRequest(
                        new TimeOnly(12, 0),
                        new TimeOnly(18, 0))
                ])),
            ExpectedVersion: null);
        using var invalidResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening",
            overlap);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, invalidResponse.StatusCode);
        Assert.Contains(
            "OPENING_INTERVAL_OVERLAP",
            await invalidResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var missingCsrf = await client.PutAsJsonAsync(
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening",
            new UpdateLocationWeeklyOpeningRequest(OpeningWeek(), null),
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var crossTenant = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.OtherLocationId}/weekly-opening",
            new UpdateLocationWeeklyOpeningRequest(OpeningWeek(), null));
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);

        using var inactiveResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.InactiveLocalLocationId}/weekly-opening",
            new UpdateLocationWeeklyOpeningRequest(OpeningWeek(), null));
        var inactive = await ReadAsync<LocationWeeklyOpeningResponse>(inactiveResponse);
        Assert.AreEqual(HttpStatusCode.Created, inactiveResponse.StatusCode);
        CollectionAssert.Contains(
            inactive.Warnings.ToArray(),
            "INACTIVE_LOCATION_EXCLUDED_FROM_PLANNING");
    }

    [TestMethod]
    public async Task Phase2BAdminEndpointsRejectUserWithoutRequiredPermissions()
    {
        using var loginResponse = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, loginResponse.StatusCode);

        using var openingResponse = await client.GetAsync(
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening");
        using var coverageResponse = await client.GetAsync(
            "/api/admin/coverage-requirements");
        using var profileResponse = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/work-profile");

        Assert.AreEqual(HttpStatusCode.Forbidden, openingResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, coverageResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, profileResponse.StatusCode);
    }

    [TestMethod]
    public async Task ShiftTemplateLifecycleRequiresPermissionCsrfConcurrencyAndAudit()
    {
        await LoginAdminAsync();
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/shift-templates",
            new CreateLocationShiftTemplateRequest(
                "Reggeli sablon",
                ShiftTemplateCategory.Morning,
                [DayOfWeek.Monday, DayOfWeek.Tuesday],
                new TimeOnly(8, 0),
                new TimeOnly(14, 0),
                true,
                StaffingCapability.Pharmacist));
        var created = await ReadAsync<LocationShiftTemplateResponse>(createResponse);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

        var updateRequest = new UpdateLocationShiftTemplateRequest(
            "Hosszú sablon",
            ShiftTemplateCategory.Long,
            [DayOfWeek.Monday],
            new TimeOnly(8, 0),
            new TimeOnly(18, 0),
            true,
            null,
            created.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/location-shift-templates/{created.Id}",
            updateRequest);
        var updated = await ReadAsync<LocationShiftTemplateResponse>(updateResponse);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.AreEqual(ShiftTemplateCategory.Long, updated.Category);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/location-shift-templates/{created.Id}",
            updateRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var deactivateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/location-shift-templates/{created.Id}/deactivate",
            new DeactivateLocationShiftTemplateRequest(updated.Version));
        var deactivated = await ReadAsync<LocationShiftTemplateResponse>(deactivateResponse);
        Assert.IsFalse(deactivated.IsActive);

        using var crossTenant = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/locations/{IntegrationTestData.OtherLocationId}/shift-templates",
            new CreateLocationShiftTemplateRequest(
                "Tiltott",
                ShiftTemplateCategory.Custom,
                [DayOfWeek.Friday],
                new TimeOnly(8, 0),
                new TimeOnly(9, 0),
                true,
                null));
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            3,
            await dbContext.AuditLogs.CountAsync(log =>
                log.EntityId == created.Id.ToString()));
    }

    [TestMethod]
    public async Task CapabilitiesSeedImplicationsConcurrencyAuditAndTenantBoundaryWork()
    {
        await LoginAdminAsync();
        using var getResponse = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.AdminEmployeeId}/capabilities");
        var seeded = await ReadAsync<EmployeeCapabilitiesResponse>(getResponse);
        CollectionAssert.Contains(
            seeded.AssignedCapabilities.ToArray(),
            StaffingCapability.Pharmacist);

        var updateRequest = new UpdateEmployeeCapabilitiesRequest(
            [StaffingCapability.SpecialistAssistant],
            seeded.EmployeeVersion);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.AdminEmployeeId}/capabilities",
            updateRequest);
        var updated = await ReadAsync<EmployeeCapabilitiesResponse>(updateResponse);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        CollectionAssert.Contains(
            updated.EffectiveCapabilities.ToArray(),
            StaffingCapability.Assistant);
        CollectionAssert.Contains(
            updated.EffectiveCapabilities.ToArray(),
            StaffingCapability.Pharmacist);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.AdminEmployeeId}/capabilities",
            updateRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var crossTenant = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/capabilities");
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            1,
            await dbContext.AuditLogs.CountAsync(log =>
                log.EntityId == IntegrationTestData.AdminEmployeeId.ToString() &&
                log.Action == "EmployeeCapabilities.Updated"));
    }

    [TestMethod]
    public async Task CoverageWarningsFiltersInactiveLocationTenantAndAuditWork()
    {
        await LoginAdminAsync();
        await PutMondayOpeningAsync(new TimeOnly(8, 0), new TimeOnly(18, 0));

        using var outsideResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/coverage-requirements",
            new CreateCoverageRequirementRequest(
                IntegrationTestData.LocalLocationId,
                DayOfWeek.Monday,
                new TimeOnly(7, 0),
                new TimeOnly(9, 0),
                StaffingCapability.Pharmacist,
                1,
                CoverageSeverity.Blocking,
                true));
        var outside = await ReadAsync<CoverageRequirementResponse>(outsideResponse);
        Assert.AreEqual(HttpStatusCode.Created, outsideResponse.StatusCode);
        CollectionAssert.Contains(
            outside.Warnings.ToArray(),
            "COVERAGE_OUTSIDE_OPENING_HOURS");

        using var insideResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/coverage-requirements",
            new CreateCoverageRequirementRequest(
                IntegrationTestData.LocalLocationId,
                DayOfWeek.Monday,
                new TimeOnly(12, 0),
                new TimeOnly(16, 0),
                StaffingCapability.Pharmacist,
                2,
                CoverageSeverity.Warning,
                true));
        var inside = await ReadAsync<CoverageRequirementResponse>(insideResponse);
        Assert.IsEmpty(inside.Warnings);

        using var inactiveLocationResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/coverage-requirements",
            new CreateCoverageRequirementRequest(
                IntegrationTestData.InactiveLocalLocationId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0),
                StaffingCapability.Assistant,
                1,
                CoverageSeverity.Warning,
                true));
        var inactive = await ReadAsync<CoverageRequirementResponse>(inactiveLocationResponse);
        CollectionAssert.Contains(
            inactive.Warnings.ToArray(),
            "INACTIVE_LOCATION_EXCLUDED_FROM_PLANNING");

        using var listResponse = await client.GetAsync(
            $"/api/admin/coverage-requirements?locationId={IntegrationTestData.LocalLocationId}" +
            "&dayOfWeek=Monday&capability=Pharmacist");
        var listed = await ReadAsync<CoverageRequirementResponse[]>(listResponse);
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.HasCount(2, listed);

        var updateRequest = new UpdateCoverageRequirementRequest(
            outside.LocationId,
            outside.DayOfWeek,
            new TimeOnly(8, 0),
            new TimeOnly(10, 0),
            outside.RequiredCapability,
            3,
            outside.Severity,
            true,
            outside.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/coverage-requirements/{outside.Id}",
            updateRequest);
        var updated = await ReadAsync<CoverageRequirementResponse>(updateResponse);
        Assert.AreEqual(3, updated.RequiredCount);
        Assert.IsEmpty(updated.Warnings);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/coverage-requirements/{outside.Id}",
            updateRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var crossTenant = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/coverage-requirements",
            new CreateCoverageRequirementRequest(
                IntegrationTestData.OtherLocationId,
                DayOfWeek.Monday,
                new TimeOnly(8, 0),
                new TimeOnly(9, 0),
                StaffingCapability.Pharmacist,
                1,
                CoverageSeverity.Blocking,
                true));
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            2,
            await dbContext.AuditLogs.CountAsync(log =>
                log.EntityId == outside.Id.ToString()));
    }

    [TestMethod]
    public async Task WorkProfileBoundariesUpsertConcurrencyCsrfSyncAndAuditWork()
    {
        await LoginAdminAsync();
        var request = ValidWorkProfile();
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/work-profile",
            request);
        var created = await ReadAsync<EmployeeWorkProfileResponse>(createResponse);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.AreEqual(9_600, created.ContractedMonthlyMinutes);

        var updateRequest = request with
        {
            ContractedMonthlyMinutes = 10_000,
            MaximumDailyMinutes = 660,
            MaximumLongShiftMinutes = 660,
            ExpectedVersion = created.Version
        };
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/work-profile",
            updateRequest);
        var updated = await ReadAsync<EmployeeWorkProfileResponse>(updateResponse);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.AreEqual(10_000, updated.ContractedMonthlyMinutes);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/work-profile",
            updateRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var invalidResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.OfflineEmployeeId}/work-profile",
            request with
            {
                MinimumShiftMinutes = 600,
                StandardShiftMinutes = 480,
                MaximumRegularShiftMinutes = 400
            });
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, invalidResponse.StatusCode);

        using var missingCsrf = await client.PutAsJsonAsync(
            $"/api/admin/employees/{IntegrationTestData.OfflineEmployeeId}/work-profile",
            request,
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var crossTenant = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/work-profile",
            request);
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var employee = await dbContext.Employees.SingleAsync(
            item => item.Id == IntegrationTestData.RegularEmployeeId);
        Assert.AreEqual(10_000, employee.MonthlyMinutesLimit);
        Assert.AreEqual(660, employee.MaxDailyMinutes);
        Assert.AreEqual(
            2,
            await dbContext.AuditLogs.CountAsync(log =>
                log.EntityId == created.Id.ToString()));
    }

    [TestMethod]
    public async Task ShiftQuotaCrudValidationConcurrencyTenantAndAuditWork()
    {
        await LoginAdminAsync();
        var request = new CreateEmployeeShiftQuotaRuleRequest(
            ShiftQuotaDimension.SaturdayShift,
            QuotaPeriod.Month,
            0,
            1,
            2,
            QuotaSeverity.Required,
            true);
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/shift-quota-rules",
            request);
        var created = await ReadAsync<EmployeeShiftQuotaRuleResponse>(createResponse);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

        using var duplicateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/shift-quota-rules",
            request);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, duplicateResponse.StatusCode);

        var updateRequest = new UpdateEmployeeShiftQuotaRuleRequest(
            created.Dimension,
            created.Period,
            1,
            2,
            3,
            QuotaSeverity.Preferred,
            true,
            created.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employee-shift-quota-rules/{created.Id}",
            updateRequest);
        var updated = await ReadAsync<EmployeeShiftQuotaRuleResponse>(updateResponse);
        Assert.AreEqual(2, updated.Target);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employee-shift-quota-rules/{created.Id}",
            updateRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var deactivateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employee-shift-quota-rules/{created.Id}/deactivate",
            new DeactivateEmployeeShiftQuotaRuleRequest(updated.Version));
        var deactivated = await ReadAsync<EmployeeShiftQuotaRuleResponse>(deactivateResponse);
        Assert.IsFalse(deactivated.IsActive);

        using var crossTenant = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/shift-quota-rules",
            request);
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            3,
            await dbContext.AuditLogs.CountAsync(log =>
                log.EntityId == created.Id.ToString()));
    }

    private async Task PutMondayOpeningAsync(TimeOnly startTime, TimeOnly endTime)
    {
        using var response = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/locations/{IntegrationTestData.LocalLocationId}/weekly-opening",
            new UpdateLocationWeeklyOpeningRequest(
                OpeningWeek(new OpeningDayRequest(
                    DayOfWeek.Monday,
                    OpeningDayMode.CustomIntervals,
                    [new OpeningIntervalRequest(startTime, endTime)])),
                null));
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static UpdateEmployeeWorkProfileRequest ValidWorkProfile() =>
        new(
            ContractedMonthlyMinutes: 9_600,
            ContractedWeeklyMinutes: 2_400,
            StandardShiftMinutes: 480,
            MinimumShiftMinutes: 240,
            MaximumRegularShiftMinutes: 600,
            MaximumDailyMinutes: 720,
            AllowsLongShift: true,
            MaximumLongShiftMinutes: 720,
            AllowsFullOpeningHoursShift: false,
            AllowsOvertime: true,
            MaximumOvertimeMinutesPerMonth: 600,
            AllowsOnCallDuty: true,
            MaximumOnCallAssignmentsPerMonth: 4,
            AllowsStandby: false,
            MaximumStandbyAssignmentsPerMonth: null,
            AllowsSaturday: true,
            MaximumSaturdaysPerMonth: 2,
            AllowsSunday: false,
            MaximumSundaysPerMonth: null,
            IncludeInAutoFill: true,
            ExpectedVersion: null);

    private static OpeningDayRequest[] OpeningWeek(
        params OpeningDayRequest[] overrides)
    {
        var byDay = overrides.ToDictionary(day => day.DayOfWeek);
        return Enum.GetValues<DayOfWeek>()
            .Select(day => byDay.GetValueOrDefault(day) ?? new OpeningDayRequest(
                day,
                OpeningDayMode.Closed,
                []))
            .ToArray();
    }

    private static OpeningDayResponse Day(
        LocationWeeklyOpeningResponse opening,
        DayOfWeek dayOfWeek) =>
        opening.Days.Single(day => day.DayOfWeek == dayOfWeek);

    private async Task LoginAdminAsync()
    {
        using var response = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var token = await GetCsrfTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(email, password),
                options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        object body)
    {
        var token = await GetCsrfTokenAsync();
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        return await client.SendAsync(request);
    }

    private async Task<CsrfTokenResponse> GetCsrfTokenAsync()
    {
        var token = await client.GetFromJsonAsync<CsrfTokenResponse>(
            "/api/auth/csrf",
            JsonOptions);
        Assert.IsNotNull(token);
        return token;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => await IntegrationJson.ReadSuccessAsync<T>(response);
}
