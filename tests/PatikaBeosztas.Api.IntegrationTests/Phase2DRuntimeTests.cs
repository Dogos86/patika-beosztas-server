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
public sealed class Phase2DRuntimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationJson.Options;
    private const string TaxIdentifier = "8456123789";

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
    public async Task PayrollProfileEncryptsMasksProtectsFullValueAndAuditsWithoutSensitiveData()
    {
        await LoginAdminAsync();
        var created = await PutProfileAsync();
        Assert.AreEqual(TaxIdentifier, created.TaxIdentificationNumber);
        Assert.AreEqual("******3789", created.MaskedTaxIdentificationNumber);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            var stored = await dbContext.EmployeePayrollProfiles
                .SingleAsync(item => item.Id == created.Id);
            Assert.AreNotEqual(TaxIdentifier, stored.TaxIdentificationNumberCiphertext);
            Assert.DoesNotContain(
                TaxIdentifier,
                stored.TaxIdentificationNumberCiphertext,
                StringComparison.Ordinal);
            Assert.AreNotEqual(TaxIdentifier, stored.TaxIdentificationNumberHash);
            var audit = await dbContext.AuditLogs
                .SingleAsync(log =>
                    log.EntityId == created.Id.ToString() &&
                    log.Action == "EmployeePayrollProfile.Created");
            Assert.DoesNotContain(
                TaxIdentifier,
                audit.ChangeSummary,
                StringComparison.Ordinal);

            var sensitivePermission = await dbContext.UserPermissions.SingleAsync(
                item =>
                    item.UserId == IntegrationTestData.AdminUserId &&
                    item.Permission == ApplicationPermission.ViewPayrollSensitiveData);
            dbContext.UserPermissions.Remove(sensitivePermission);
            await dbContext.SaveChangesAsync();
        }

        using var maskedResponse = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-profile");
        var masked = await ReadAsync<EmployeePayrollProfileResponse>(maskedResponse);
        Assert.IsNull(masked.TaxIdentificationNumber);
        Assert.AreEqual("******3789", masked.MaskedTaxIdentificationNumber);

        var update = new UpdateEmployeePayrollProfileRequest(
            "D-0002",
            TaxIdentificationNumber: null,
            new DateOnly(2026, 2, 1),
            "PAY-002",
            EmployeePayrollProfileStatus.UnderReview,
            created.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-profile",
            update);
        var updated = await ReadAsync<EmployeePayrollProfileResponse>(updateResponse);
        Assert.AreNotEqual(created.Version, updated.Version);
        Assert.IsNull(updated.TaxIdentificationNumber);

        using var staleResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-profile",
            update);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        await using var auditScope = application.Services.CreateAsyncScope();
        var auditContext =
            auditScope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var viewAudit = await auditContext.AuditLogs.SingleAsync(log =>
            log.EntityId == created.Id.ToString() &&
            log.Action == "EmployeePayrollProfile.Viewed");
        Assert.DoesNotContain(
            TaxIdentifier,
            viewAudit.ChangeSummary,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PayrollPermissionsCsrfAndTenantBoundaryAreIndependentFromScheduling()
    {
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            dbContext.UserPermissions.Add(new UserPermission
            {
                OrganizationId = IntegrationTestData.OrganizationId,
                UserId = IntegrationTestData.RegularUserId,
                Permission = ApplicationPermission.ManageSchedules
            });
            await dbContext.SaveChangesAsync();
        }

        using (var regularLogin = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password))
        {
            Assert.AreEqual(HttpStatusCode.OK, regularLogin.StatusCode);
        }

        using var forbidden = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-profile");
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await LoginAdminAsync();
        using var missingCsrf = await client.PutAsJsonAsync(
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-profile",
            ProfileRequest(),
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var crossTenant = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/payroll-profile",
            ProfileRequest());
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [TestMethod]
    public async Task SelfSurveyUsesLinkedEmployeeNeedsClarificationAndCannotEditAfterSubmit()
    {
        using var login = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        var createRequest = new CreateTaxAllowanceSurveyRequest(
            2026,
            new DateOnly(2026, 1, 1),
            ValidAnswers() with
            {
                PersonalAllowanceEligibility = SurveyAnswer.Unknown
            });
        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/me/tax-allowance-surveys",
            createRequest);
        var created = await ReadAsync<TaxAllowanceSurveyResponse>(createResponse);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.AreEqual(IntegrationTestData.RegularEmployeeId, created.EmployeeId);
        Assert.AreEqual(new DateOnly(2026, 12, 31), created.EffectiveTo);
        Assert.AreEqual(
            TaxAllowanceDecisionEngine.SourceMetadata,
            created.SourceMetadata);

        var update = new UpdateOwnTaxAllowanceSurveyRequest(
            new DateOnly(2026, 1, 1),
            createRequest.Answers,
            created.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/me/tax-allowance-surveys/{created.Id}",
            update);
        var updated = await ReadAsync<TaxAllowanceSurveyResponse>(updateResponse);

        using var submitResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/me/tax-allowance-surveys/{created.Id}/submit",
            new TaxSurveyVersionRequest(updated.Version));
        var submitted = await ReadAsync<TaxAllowanceSurveyResponse>(submitResponse);
        Assert.AreEqual(
            TaxAllowanceSurveyStatus.NeedsClarification,
            submitted.Status);
        Assert.HasCount(7, submitted.DeclarationRequirements);
        Assert.AreEqual("HU-2026.1", submitted.RuleSetVersion);

        using var editSubmitted = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/me/tax-allowance-surveys/{created.Id}",
            update with { ExpectedVersion = submitted.Version });
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, editSubmitted.StatusCode);

        using var summaryResponse = await client.GetAsync("/api/me/payroll-onboarding");
        var summary = await ReadAsync<PayrollOnboardingSummaryResponse>(summaryResponse);
        Assert.AreEqual(IntegrationTestData.RegularEmployeeId, summary.EmployeeId);
        Assert.IsNull(summary.PayrollProfile);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            3,
            await dbContext.AuditLogs.CountAsync(log =>
                log.EntityId == created.Id.ToString()));
    }

    [TestMethod]
    public async Task AdminReviewReopenOverrideDeclarationWorkflowAndCompletionWork()
    {
        await LoginAdminAsync();
        var profile = await PutProfileAsync();
        var answers = ValidAnswers() with
        {
            Under25AllowanceOptOut = Under25AllowanceOptOut.Yes
        };
        var survey = await PutAdminSurveyAsync(answers);
        survey = await PostSurveyWorkflowAsync(
            survey.Id,
            "submit",
            new TaxSurveyVersionRequest(survey.Version));
        Assert.AreEqual(TaxAllowanceSurveyStatus.Submitted, survey.Status);

        var personal = survey.DeclarationRequirements.Single(
            item => item.Type == TaxDeclarationType.PersonalAllowance);
        using var overrideResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/tax-declaration-requirements/{personal.Id}/override",
            new OverrideTaxDeclarationRequirementRequest(
                true,
                TaxDeclarationRequirementStatus.Required,
                "Bérszámfejtő által ellenőrzött kézi korrekció.",
                null,
                personal.Version));
        var overridden = await ReadAsync<TaxDeclarationRequirementResponse>(
            overrideResponse);
        Assert.IsTrue(overridden.ManualOverride);

        survey = await PostSurveyWorkflowAsync(
            survey.Id,
            "review",
            new ReviewTaxAllowanceSurveyRequest(
                "Belső ellenőrzés megtörtént.",
                survey.Version));
        survey = await PostSurveyWorkflowAsync(
            survey.Id,
            "reopen",
            new TaxSurveyVersionRequest(survey.Version));
        Assert.AreEqual(TaxAllowanceSurveyStatus.Draft, survey.Status);

        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/tax-allowance-surveys/2026",
            new UpdateTaxAllowanceSurveyRequest(
                survey.EffectiveFrom,
                answers,
                "Pontosított belső válaszok.",
                survey.Version));
        survey = await ReadAsync<TaxAllowanceSurveyResponse>(updateResponse);
        survey = await PostSurveyWorkflowAsync(
            survey.Id,
            "submit",
            new TaxSurveyVersionRequest(survey.Version));
        var preservedOverride = survey.DeclarationRequirements.Single(
            item => item.Type == TaxDeclarationType.PersonalAllowance);
        Assert.IsTrue(preservedOverride.ManualOverride);
        Assert.IsTrue(preservedOverride.RequiredDecision);

        survey = await PostSurveyWorkflowAsync(
            survey.Id,
            "review",
            new ReviewTaxAllowanceSurveyRequest(
                "Újraellenőrizve.",
                survey.Version));
        survey = await PostSurveyWorkflowAsync(
            survey.Id,
            "complete",
            new TaxSurveyVersionRequest(survey.Version));
        Assert.AreEqual(TaxAllowanceSurveyStatus.Completed, survey.Status);

        foreach (var requirement in survey.DeclarationRequirements
                     .Where(item => item.RequiredDecision))
        {
            var current = requirement;
            foreach (var status in new[]
                     {
                         TaxDeclarationRequirementStatus.ToSend,
                         TaxDeclarationRequirementStatus.Sent,
                         TaxDeclarationRequirementStatus.ReceivedOnya,
                         TaxDeclarationRequirementStatus.Verified,
                         TaxDeclarationRequirementStatus.Applied
                     })
            {
                using var statusResponse = await SendWithCsrfAsync(
                    HttpMethod.Put,
                    $"/api/admin/tax-declaration-requirements/{current.Id}/status",
                    new UpdateTaxDeclarationStatusRequest(
                        status,
                        null,
                        "Workflow teszt.",
                        current.Version));
                current = await ReadAsync<TaxDeclarationRequirementResponse>(
                    statusResponse);
            }
        }

        using var completeResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-onboarding/complete",
            new CompletePayrollOnboardingRequest(profile.Version));
        var completed = await ReadAsync<PayrollOnboardingSummaryResponse>(
            completeResponse);
        Assert.IsTrue(completed.IsComplete);
        Assert.AreEqual(0, completed.OutstandingDeclarationCount);
    }

    [TestMethod]
    public async Task JsonAndCsvExportsArePermissionProtectedAuditedAndDataMinimized()
    {
        await LoginAdminAsync();
        _ = await PutProfileAsync();
        _ = await PutAdminSurveyAsync(ValidAnswers());

        using var jsonResponse = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-onboarding/export?format=json");
        var json = await jsonResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, jsonResponse.StatusCode);
        StringAssert.Contains(json, "payroll-onboarding-export-v1");
        StringAssert.Contains(json, "Normál Dolgozó");
        Assert.DoesNotContain(TaxIdentifier, json, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnosis", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);

        using var csvResponse = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-onboarding/export?format=csv");
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, csvResponse.StatusCode);
        StringAssert.Contains(csv, "schemaVersion,generatedAtUtc,employeeId");
        StringAssert.Contains(csv, "\"Normál Dolgozó\"");
        Assert.DoesNotContain(TaxIdentifier, csv, StringComparison.Ordinal);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            Assert.AreEqual(
                2,
                await dbContext.AuditLogs.CountAsync(log =>
                    log.Action == "PayrollOnboarding.Exported" &&
                    log.EntityId == IntegrationTestData.RegularEmployeeId.ToString()));
        }

        using var regularLogin = await LoginAsync(
            "dolgozo@test.invalid",
            IntegrationTestData.Password);
        Assert.AreEqual(HttpStatusCode.OK, regularLogin.StatusCode);
        using var forbidden = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-onboarding/export");
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [TestMethod]
    public async Task SurveyConcurrencyTenantBoundaryAndCompositeForeignKeysAreEnforced()
    {
        await LoginAdminAsync();
        var created = await PutAdminSurveyAsync(ValidAnswers());
        var update = new UpdateTaxAllowanceSurveyRequest(
            created.EffectiveFrom,
            created.Answers,
            null,
            created.Version);
        using var updateResponse = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/tax-allowance-surveys/2026",
            update);
        var updated = await ReadAsync<TaxAllowanceSurveyResponse>(updateResponse);
        Assert.AreNotEqual(created.Version, updated.Version);

        using var stale = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/tax-allowance-surveys/2026",
            update);
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);

        using var crossTenant = await client.GetAsync(
            $"/api/admin/employees/{IntegrationTestData.OtherEmployeeId}/tax-allowance-surveys/2026");
        Assert.AreEqual(HttpStatusCode.NotFound, crossTenant.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        dbContext.EmployeePayrollProfiles.Add(new EmployeePayrollProfile
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            EmployeeNumber = "CROSS-TENANT",
            TaxIdentificationNumberCiphertext = "protected-test-value",
            TaxIdentificationNumberHash = new string('a', 64),
            EmploymentStartDate = new DateOnly(2026, 1, 1),
            Status = EmployeePayrollProfileStatus.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = IntegrationTestData.AdminUserId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedByUserId = IntegrationTestData.AdminUserId
        });
        await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());
    }

    [TestMethod]
    public async Task RuntimeOpenApiRetainsPhase2DEndpointsAndNoMedicalFields()
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(raw);
        Assert.AreEqual(
            "0.5.0-phase3a",
            document.RootElement
                .GetProperty("info")
                .GetProperty("version")
                .GetString());
        var paths = document.RootElement.GetProperty("paths");
        Assert.IsTrue(paths.TryGetProperty(
            "/api/me/payroll-onboarding",
            out _));
        Assert.IsTrue(paths.TryGetProperty(
            "/api/me/tax-allowance-surveys",
            out _));
        Assert.IsTrue(paths.TryGetProperty(
            "/api/admin/employees/{employeeId}/payroll-onboarding/export",
            out _));
        Assert.IsFalse(paths.TryGetProperty(
            "/api/admin/monthly-payroll-export",
            out _));
        Assert.DoesNotContain(
            "diagnosis",
            raw,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "medicalDocument",
            raw,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "taxIdentificationNumberCiphertext",
            raw,
            StringComparison.Ordinal);
    }

    private async Task<EmployeePayrollProfileResponse> PutProfileAsync()
    {
        using var response = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/payroll-profile",
            ProfileRequest());
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<EmployeePayrollProfileResponse>(response);
    }

    private static UpdateEmployeePayrollProfileRequest ProfileRequest() =>
        new(
            "D-0001",
            TaxIdentifier,
            new DateOnly(2026, 1, 15),
            "PAY-001",
            EmployeePayrollProfileStatus.Draft,
            null);

    private async Task<TaxAllowanceSurveyResponse> PutAdminSurveyAsync(
        TaxAllowanceSurveyAnswers answers)
    {
        using var response = await SendWithCsrfAsync(
            HttpMethod.Put,
            $"/api/admin/employees/{IntegrationTestData.RegularEmployeeId}/tax-allowance-surveys/2026",
            new UpdateTaxAllowanceSurveyRequest(
                new DateOnly(2026, 1, 1),
                answers,
                null,
                null));
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<TaxAllowanceSurveyResponse>(response);
    }

    private async Task<TaxAllowanceSurveyResponse> PostSurveyWorkflowAsync(
        Guid surveyId,
        string operation,
        object body)
    {
        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/tax-allowance-surveys/{surveyId}/{operation}",
            body);
        return await ReadAsync<TaxAllowanceSurveyResponse>(response);
    }

    private static TaxAllowanceSurveyAnswers ValidAnswers() =>
        new(
            MonthlyAllowancePreference.ApplyMonthly,
            MaritalStatus.Single,
            MarriageDate: null,
            FirstMarriageStatus: SurveyAnswer.No,
            FamilyAllowanceEligibleChildrenCount: 0,
            DependentStudentCount: 0,
            HasFetusAfterDay91: false,
            FetusEligibilityMonth: null,
            HasDisabledDependent: false,
            HasSharedCustodyChild: false,
            FamilyAllowanceClaimMode.NotRequested,
            OtherEligiblePersonClaimsPart: SurveyAnswer.No,
            IsBiologicalOrAdoptiveMother: false,
            MotherAllowanceQualifyingChildrenCount.None,
            HasCurrentOwnChildOrFetusEligibleForFamilyAllowance: SurveyAnswer.No,
            PersonalAllowanceEligibility: SurveyAnswer.No,
            PersonalAllowanceStartMonth: null,
            HasOtherEmployerOrRegularPayer: SurveyAnswer.No,
            Under25AllowanceOptOut.No,
            ForeignTaxResidencyOrSimilarForeignBenefit.None);

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

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await IntegrationJson.ReadSuccessAsync<T>(response);
}
