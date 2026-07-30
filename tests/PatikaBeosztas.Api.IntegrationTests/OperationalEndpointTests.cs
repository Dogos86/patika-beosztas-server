using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class OperationalEndpointTests
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=1;Database=unused;Username=unused;Password=unused";

    [TestMethod]
    public async Task LivenessDoesNotDependOnPostgreSql()
    {
        await using var application = new ApiFactory(UnusedConnectionString);
        using var client = application.CreateHttpsClient();

        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", body);
    }

    [TestMethod]
    public async Task ReadinessReturnsServiceUnavailableWithoutPostgreSql()
    {
        const string unavailableConnection =
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;" +
            "Password=unused;Timeout=1;Command Timeout=1";
        await using var application = new ApiFactory(
            unavailableConnection,
            disableScheduleGenerationWorker: true);
        using var client = application.CreateHttpsClient();

        using var response = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual("Unhealthy", body);
    }

    [TestMethod]
    public async Task ProductionDoesNotPublishOpenApiOrDemoLogin()
    {
        var keysPath = Path.Combine(
            Path.GetTempPath(),
            $"patika-production-test-{Guid.NewGuid():N}");
        try
        {
            await using var application = new ApiFactory(
                UnusedConnectionString,
                disableScheduleGenerationWorker: true,
                environmentName: "Production",
                openApiEnabled: false,
                dataProtectionKeysPath: keysPath);
            using var client = application.CreateHttpsClient();

            using var openApiResponse = await client.GetAsync("/openapi/v1.json");
            using var demoLoginResponse = await client.PostAsync(
                "/api/auth/demo",
                content: null);

            Assert.AreEqual(HttpStatusCode.NotFound, openApiResponse.StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, demoLoginResponse.StatusCode);
        }
        finally
        {
            if (Directory.Exists(keysPath))
            {
                Directory.Delete(keysPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task OpenApiDocumentsPhase3AEndpointsContractsAndCookieSecurity()
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
        var phase1Paths = new[]
        {
            "/api/auth/csrf",
            "/api/auth/login",
            "/api/auth/logout",
            "/api/auth/session",
            "/api/admin/employees",
            "/api/admin/employees/{id}",
            "/api/admin/locations",
            "/api/admin/locations/{id}",
            "/api/admin/users",
            "/api/admin/users/{id}",
            "/api/admin/users/{id}/permissions",
            "/api/admin/users/{id}/employee-link",
            "/api/admin/users/{id}/status"
        };
        foreach (var path in phase1Paths)
        {
            Assert.IsTrue(paths.TryGetProperty(path, out _), $"Hiányzó OpenAPI útvonal: {path}");
        }

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

        var phase3APaths = new[]
        {
            "/api/me/payroll-onboarding",
            "/api/me/tax-allowance-surveys",
            "/api/me/tax-allowance-surveys/{taxYear}",
            "/api/me/tax-allowance-surveys/{id}",
            "/api/me/tax-allowance-surveys/{id}/submit",
            "/api/admin/employees/{employeeId}/payroll-onboarding",
            "/api/admin/employees/{employeeId}/payroll-onboarding/complete",
            "/api/admin/employees/{employeeId}/payroll-onboarding/export",
            "/api/admin/employees/{employeeId}/payroll-profile",
            "/api/admin/employees/{employeeId}/tax-allowance-surveys/{taxYear}",
            "/api/admin/tax-allowance-surveys/{id}/submit",
            "/api/admin/tax-allowance-surveys/{id}/reopen",
            "/api/admin/tax-allowance-surveys/{id}/review",
            "/api/admin/tax-allowance-surveys/{id}/complete",
            "/api/admin/employees/{employeeId}/tax-declaration-requirements",
            "/api/admin/tax-declaration-requirements/{id}/status",
            "/api/admin/tax-declaration-requirements/{id}/override",
            "/api/admin/schedule-generations",
            "/api/admin/schedule-generations/{runId}",
            "/api/admin/schedule-generations/{runId}/cancel",
            "/api/admin/schedules",
            "/api/admin/schedules/{scheduleId}",
            "/api/admin/schedules/{scheduleId}/clone-draft",
            "/api/admin/schedules/{scheduleId}/employee-matrix",
            "/api/admin/schedules/{scheduleId}/location-coverage",
            "/api/admin/schedules/{scheduleId}/issues",
            "/api/admin/schedules/{scheduleId}/changes",
            "/api/admin/schedules/{scheduleId}/shifts/{shiftId}/explanation",
            "/api/admin/schedules/{scheduleId}/shifts/{shiftId}/alternatives",
            "/api/admin/schedules/{scheduleId}/shifts/{shiftId}/lock",
            "/api/admin/schedules/{scheduleId}/shifts/{shiftId}/unlock",
            "/api/admin/schedules/{scheduleId}/shifts/{shiftId}/reject",
            "/api/admin/schedules/{scheduleId}/shifts/{shiftId}/replace",
            "/api/admin/schedules/{scheduleId}/regenerate",
            "/api/admin/schedules/{scheduleId}/submit-review",
            "/api/admin/schedules/{scheduleId}/return-draft",
            "/api/admin/schedules/{scheduleId}/approve",
            "/api/admin/schedules/{scheduleId}/publish",
            "/api/admin/schedules/{scheduleId}/archive",
            "/api/me/schedule"
        };
        foreach (var path in phase3APaths)
        {
            Assert.IsTrue(paths.TryGetProperty(path, out _), $"Hiányzó OpenAPI útvonal: {path}");
        }

        Assert.AreEqual(
            "0.5.0-phase3a",
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
        var publicEnumTypes = new[]
        {
            typeof(ApplicationPermission),
            typeof(ProfessionalRole),
            typeof(LocationType),
            typeof(EmployeeTimeWindowType),
            typeof(TimeType),
            typeof(WorkPreferenceType),
            typeof(LeaveType),
            typeof(LeaveRequestStatus),
            typeof(LeaveDecision),
            typeof(DayOfWeek),
            typeof(OpeningDayMode),
            typeof(ShiftTemplateCategory),
            typeof(StaffingCapability),
            typeof(CoverageSeverity),
            typeof(ShiftQuotaDimension),
            typeof(QuotaPeriod),
            typeof(QuotaSeverity),
            typeof(EmployeePayrollProfileStatus),
            typeof(TaxAllowanceSurveyStatus),
            typeof(MonthlyAllowancePreference),
            typeof(MaritalStatus),
            typeof(SurveyAnswer),
            typeof(MotherAllowanceQualifyingChildrenCount),
            typeof(FamilyAllowanceClaimMode),
            typeof(Under25AllowanceOptOut),
            typeof(ForeignTaxResidencyOrSimilarForeignBenefit),
            typeof(TaxDeclarationType),
            typeof(TaxDeclarationRequirementStatus),
            typeof(ScheduleStatus),
            typeof(ScheduleGenerationStatus),
            typeof(ScheduleSolverStatus),
            typeof(ShiftAssignmentSource),
            typeof(ShiftChangeKind),
            typeof(ScheduleIssueSeverity),
            typeof(SuggestionExclusionScope),
            typeof(PendingLeaveHandlingMode),
            typeof(RegenerationScopeType)
        };
        foreach (var enumType in publicEnumTypes)
        {
            AssertStringEnumSchema(
                schemas.GetProperty(enumType.Name),
                Enum.GetNames(enumType));
        }
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

        var canonicalPath = Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "openapi.phase3a.json");
        Assert.IsTrue(File.Exists(canonicalPath), "Hiányzik a kanonikus runtime OpenAPI export.");
        var runtimeOpenApi = JsonNode.Parse(body);
        var canonicalOpenApi = JsonNode.Parse(await File.ReadAllTextAsync(canonicalPath));
        Assert.IsNotNull(runtimeOpenApi);
        Assert.IsNotNull(canonicalOpenApi);
        // The request host drives servers, so the test host and export host differ.
        runtimeOpenApi.AsObject().Remove("servers");
        canonicalOpenApi.AsObject().Remove("servers");
        Assert.IsTrue(
            JsonNode.DeepEquals(runtimeOpenApi, canonicalOpenApi),
            "A commitolt OpenAPI export a kérésfüggő servers mezőn túl eltér a runtime választól.");
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

    private static void AssertStringEnumSchema(
        JsonElement schema,
        string[] expectedValues)
    {
        Assert.AreEqual("string", schema.GetProperty("type").GetString());
        CollectionAssert.AreEqual(
            expectedValues,
            schema.GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
    }
}
