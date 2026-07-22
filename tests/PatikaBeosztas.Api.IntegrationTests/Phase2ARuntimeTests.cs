using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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
public sealed class Phase2ARuntimeTests
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
    public async Task SelfWorkPreferenceLifecycleUsesLinkedEmployeeConcurrencyAndAudit()
    {
        using var login = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/me/work-preferences",
            new CreateWorkPreferenceRequest(
                WorkPreferenceType.Preferred,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                DayOfWeek.Monday,
                false,
                new TimeOnly(8, 15),
                new TimeOnly(12, 45),
                IntegrationTestData.LocalLocationId,
                "  Délelőtt szeretnék dolgozni.  "));
        var created = await createResponse.Content.ReadFromJsonAsync<WorkPreferenceResponse>(
            JsonOptions);

        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.IsNotNull(created);
        Assert.AreEqual(IntegrationTestData.RegularEmployeeId, created.EmployeeId);
        Assert.AreEqual("Délelőtt szeretnék dolgozni.", created.Note);
        Assert.IsTrue(created.IsActive);

        var update = new UpdateWorkPreferenceRequest(
            WorkPreferenceType.Avoid,
            created.DateFrom,
            created.DateTo,
            created.DayOfWeek,
            created.IsFullDay,
            created.StartTime,
            created.EndTime,
            created.LocationId,
            "Lehetőleg ne ekkor.",
            created.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/me/work-preferences/{created.Id}",
            update);
        var updated = await updateResponse.Content.ReadFromJsonAsync<WorkPreferenceResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.IsNotNull(updated);
        Assert.AreEqual(WorkPreferenceType.Avoid, updated.Type);
        Assert.AreNotEqual(created.Version, updated.Version);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/me/work-preferences/{created.Id}",
            update);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var deactivateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/me/work-preferences/{created.Id}/deactivate",
            new DeactivateWorkPreferenceRequest(updated.Version));
        var deactivated = await deactivateResponse.Content
            .ReadFromJsonAsync<WorkPreferenceResponse>(JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.IsNotNull(deactivated);
        Assert.IsFalse(deactivated.IsActive);

        using var listResponse = await client.GetAsync(
            $"/api/me/work-preferences?includeInactive=true&employeeId={IntegrationTestData.AdminEmployeeId}");
        var items = await listResponse.Content
            .ReadFromJsonAsync<WorkPreferenceResponse[]>(JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.IsNotNull(items);
        Assert.HasCount(1, items);
        Assert.AreEqual(IntegrationTestData.RegularEmployeeId, items[0].EmployeeId);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var auditActions = await dbContext.AuditLogs
            .Where(log => log.EntityId == created.Id.ToString())
            .Select(log => log.Action)
            .ToArrayAsync();
        CollectionAssert.Contains(auditActions, "WorkPreference.Created");
        CollectionAssert.Contains(auditActions, "WorkPreference.Updated");
        CollectionAssert.Contains(auditActions, "WorkPreference.Deactivated");
    }

    [TestMethod]
    public async Task WorkPreferenceSelfAndTenantBoundariesAreEnforced()
    {
        Guid adminPreferenceId;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            var now = DateTimeOffset.UtcNow;
            var preference = new WorkPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = IntegrationTestData.OrganizationId,
                EmployeeId = IntegrationTestData.AdminEmployeeId,
                Type = WorkPreferenceType.Fixed,
                DateFrom = new DateOnly(2026, 8, 3),
                DateTo = new DateOnly(2026, 8, 3),
                IsFullDay = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.WorkPreferences.Add(preference);
            await dbContext.SaveChangesAsync();
            adminPreferenceId = preference.Id;
        }

        using var employeeLogin = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        using var foreignSelfResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/me/work-preferences/{adminPreferenceId}",
            new UpdateWorkPreferenceRequest(
                WorkPreferenceType.Available,
                new DateOnly(2026, 8, 3),
                new DateOnly(2026, 8, 3),
                null,
                true,
                null,
                null,
                null,
                null,
                1));
        using var unauthorizedAdminResponse = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.AdminEmployeeId}/work-preferences");

        Assert.AreEqual(HttpStatusCode.NotFound, foreignSelfResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, unauthorizedAdminResponse.StatusCode);

        using var adminLogin = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var crossTenantList = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/work-preferences");
        using var crossTenantCreate = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/work-preferences",
            new CreateWorkPreferenceRequest(
                WorkPreferenceType.Available,
                new DateOnly(2026, 8, 3),
                new DateOnly(2026, 8, 3),
                null,
                true,
                null,
                null,
                null,
                null));

        Assert.AreEqual(HttpStatusCode.NotFound, crossTenantList.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenantCreate.StatusCode);
    }

    [TestMethod]
    public async Task AdminCanManageAnotherEmployeesWorkPreference()
    {
        using var adminLogin = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.OfflineEmployeeId}/work-preferences",
            new CreateWorkPreferenceRequest(
                WorkPreferenceType.Fixed,
                new DateOnly(2026, 8, 4),
                new DateOnly(2026, 8, 4),
                null,
                true,
                null,
                null,
                IntegrationTestData.LocalLocationId,
                "Rögzített munkanap"));
        var created = await createResponse.Content.ReadFromJsonAsync<WorkPreferenceResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.IsNotNull(created);
        Assert.AreEqual(IntegrationTestData.OfflineEmployeeId, created.EmployeeId);

        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/work-preferences/{created.Id}",
            new UpdateWorkPreferenceRequest(
                WorkPreferenceType.Available,
                created.DateFrom,
                created.DateTo,
                created.DayOfWeek,
                created.IsFullDay,
                created.StartTime,
                created.EndTime,
                created.LocationId,
                "Admin által módosítva",
                created.Version));
        var updated = await updateResponse.Content.ReadFromJsonAsync<WorkPreferenceResponse>(
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.IsNotNull(updated);
        Assert.AreEqual(WorkPreferenceType.Available, updated.Type);

        using var deactivateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/work-preferences/{created.Id}/deactivate",
            new DeactivateWorkPreferenceRequest(updated.Version));
        var deactivated = await deactivateResponse.Content
            .ReadFromJsonAsync<WorkPreferenceResponse>(JsonOptions);
        Assert.AreEqual(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.IsNotNull(deactivated);
        Assert.IsFalse(deactivated.IsActive);
    }

    [TestMethod]
    public async Task WorkPreferenceMutationRequiresCsrf()
    {
        using var login = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        using var response = await client.PostAsJsonAsync(
            "/api/me/work-preferences",
            new CreateWorkPreferenceRequest(
                WorkPreferenceType.Available,
                new DateOnly(2026, 8, 3),
                new DateOnly(2026, 8, 3),
                null,
                true,
                null,
                null,
                null,
                null),
            JsonOptions);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "INVALID_CSRF_TOKEN",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task AnnualLeaveLifecyclePersistsHistoryAuditAndConcurrency()
    {
        using var employeeLogin = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        var draft = await CreateLeaveAsync(new CreateLeaveRequest(
            LeaveType.AnnualLeave,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 5),
            true,
            null,
            null,
            "Éves szabadság"));
        Assert.AreEqual(LeaveRequestStatus.Draft, draft.Status);
        Assert.AreEqual(IntegrationTestData.RegularEmployeeId, draft.EmployeeId);

        using var submitResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/me/leave-requests/{draft.Id}/submit",
            new LeaveVersionRequest(draft.Version));
        var pending = await IntegrationJson.ReadSuccessAsync<LeaveRequestResponse>(
            submitResponse);
        Assert.AreEqual(HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.IsNotNull(pending);
        Assert.AreEqual(LeaveRequestStatus.Pending, pending.Status);

        using var staleSubmit = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/me/leave-requests/{draft.Id}/submit",
            new LeaveVersionRequest(draft.Version));
        Assert.AreEqual(HttpStatusCode.Conflict, staleSubmit.StatusCode);

        using var adminLogin = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var decisionResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/leave-requests/{draft.Id}/decision",
            new LeaveDecisionRequest(
                LeaveDecision.Approve,
                "Jóváhagyva.",
                pending.Version));
        var approved = await IntegrationJson.ReadSuccessAsync<LeaveRequestResponse>(
            decisionResponse);
        Assert.AreEqual(HttpStatusCode.OK, decisionResponse.StatusCode);
        Assert.IsNotNull(approved);
        Assert.AreEqual(LeaveRequestStatus.Approved, approved.Status);
        Assert.HasCount(3, approved.StatusHistory);
        CollectionAssert.AreEqual(
            new[]
            {
                LeaveRequestStatus.Draft,
                LeaveRequestStatus.Pending,
                LeaveRequestStatus.Approved
            },
            approved.StatusHistory.Select(history => history.ToStatus).ToArray());

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            3,
            await dbContext.LeaveStatusHistories.CountAsync(
                history => history.LeaveRequestId == draft.Id));
        Assert.AreEqual(
            3,
            await dbContext.AuditLogs.CountAsync(
                log => log.EntityId == draft.Id.ToString()));
        var history = await dbContext.LeaveStatusHistories.FirstAsync(
            item => item.LeaveRequestId == draft.Id);
        history.Reason = "Nem módosítható";
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => dbContext.SaveChangesAsync());
    }

    [TestMethod]
    public async Task LeaveOwnershipAdminPermissionAndTenantBoundariesAreEnforced()
    {
        using var adminLogin = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var adminCreateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.OfflineEmployeeId}/leave-requests",
            new CreateLeaveRequest(
                LeaveType.UnpaidLeave,
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 10),
                true,
                null,
                null,
                null));
        var otherLeave = await IntegrationJson.ReadSuccessAsync<LeaveRequestResponse>(
            adminCreateResponse);
        Assert.AreEqual(HttpStatusCode.Created, adminCreateResponse.StatusCode);
        Assert.IsNotNull(otherLeave);

        using var crossTenantCreate = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/leave-requests",
            new CreateLeaveRequest(
                LeaveType.AnnualLeave,
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 10),
                true,
                null,
                null,
                null));
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenantCreate.StatusCode);

        using var employeeLogin = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        using var foreignUpdate = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/me/leave-requests/{otherLeave.Id}",
            new UpdateLeaveRequest(
                otherLeave.DateFrom,
                otherLeave.DateTo,
                otherLeave.IsFullDay,
                otherLeave.StartTime,
                otherLeave.EndTime,
                "Jogosulatlan módosítás",
                otherLeave.Version));
        using var adminList = await client.GetAsync("/api/admin/leave-requests");

        Assert.AreEqual(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, adminList.StatusCode);
    }

    [TestMethod]
    public async Task OpenEndedSickLeaveFollowsReportedRecordedClosedWorkflow()
    {
        using var employeeLogin = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        var reported = await CreateLeaveAsync(new CreateLeaveRequest(
            LeaveType.SickLeave,
            new DateOnly(2026, 9, 15),
            null,
            true,
            null,
            null,
            null));
        Assert.AreEqual(LeaveRequestStatus.Reported, reported.Status);
        Assert.IsNull(reported.DateTo);

        using var adminLogin = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var recordResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/leave-requests/{reported.Id}/record",
            new LeaveVersionRequest(reported.Version));
        var recorded = await IntegrationJson.ReadSuccessAsync<LeaveRequestResponse>(
            recordResponse);
        Assert.AreEqual(HttpStatusCode.OK, recordResponse.StatusCode);
        Assert.IsNotNull(recorded);
        Assert.AreEqual(LeaveRequestStatus.Recorded, recorded.Status);

        using var closeResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/leave-requests/{reported.Id}/close",
            new CloseSickLeaveRequest(
                new DateOnly(2026, 9, 18),
                recorded.Version));
        var closed = await IntegrationJson.ReadSuccessAsync<LeaveRequestResponse>(
            closeResponse);
        Assert.AreEqual(HttpStatusCode.OK, closeResponse.StatusCode);
        Assert.IsNotNull(closed);
        Assert.AreEqual(LeaveRequestStatus.Closed, closed.Status);
        Assert.AreEqual(new DateOnly(2026, 9, 18), closed.DateTo);
        Assert.HasCount(3, closed.StatusHistory);
    }

    [TestMethod]
    public async Task InvalidLeaveTransitionAndMissingCsrfAreRejected()
    {
        using var employeeLogin = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        var draft = await CreateLeaveAsync(new CreateLeaveRequest(
            LeaveType.AnnualLeave,
            new DateOnly(2026, 9, 20),
            new DateOnly(2026, 9, 20),
            true,
            null,
            null,
            null));

        using var missingCsrf = await client.PostAsJsonAsync(
            $"/api/me/leave-requests/{draft.Id}/submit",
            new LeaveVersionRequest(draft.Version),
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var adminLogin = await LoginAsync(
            "admin@test.invalid",
            IntegrationTestData.Password);
        using var invalidDecision = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/leave-requests/{draft.Id}/decision",
            new LeaveDecisionRequest(
                LeaveDecision.Approve,
                null,
                draft.Version));
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, invalidDecision.StatusCode);
        Assert.Contains(
            "INVALID_LEAVE_STATUS_TRANSITION",
            await invalidDecision.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    private async Task<LeaveRequestResponse> CreateLeaveAsync(CreateLeaveRequest request)
    {
        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/me/leave-requests",
            request);
        var result = await IntegrationJson.ReadSuccessAsync<LeaveRequestResponse>(response);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return result;
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

}
