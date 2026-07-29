using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;
using PatikaBeosztas.Infrastructure.Scheduling;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
[DoNotParallelize]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "MSTest invokes the asynchronous TestCleanup method after every test.")]
public sealed class Phase3ARuntimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationJson.Options;
    private static readonly DateOnly PlanningDate = new(2026, 8, 3);

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
        client?.Dispose();
        if (application is not null)
        {
            await application.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task GenerationLifecyclePersistsSnapshotsProjectionsAuditAndRuntimeOpenApi()
    {
        await SeedPlanningInputAsync();
        await LoginAsync("admin@test.invalid");
        var request = new CreateScheduleGenerationRequest(
            PlanningDate,
            PlanningDate,
            DeterministicSeed: 42,
            MaxSolveSeconds: 10,
            WorkerCount: 1);

        using var missingCsrf = await client.PostAsJsonAsync(
            "/api/admin/schedule-generations",
            request,
            JsonOptions);
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        using var createResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/schedule-generations",
            request,
            "phase3-generation-lifecycle-0001");
        Assert.AreEqual(HttpStatusCode.Accepted, createResponse.StatusCode);
        var created = await ReadAsync<ScheduleGenerationRunResponse>(createResponse);

        using var idempotentResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/schedule-generations",
            request,
            "phase3-generation-lifecycle-0001");
        var idempotent = await ReadAsync<ScheduleGenerationRunResponse>(
            idempotentResponse);
        Assert.AreEqual(created.Id, idempotent.Id);

        var completed = await WaitForTerminalRunAsync(created.Id);
        Assert.AreEqual(ScheduleGenerationStatus.Succeeded, completed.Status);
        Assert.IsTrue(completed.SolverStatus is
            ScheduleSolverStatus.Optimal or ScheduleSolverStatus.Feasible);
        Assert.AreEqual(64, completed.InputSnapshotHash.Length);
        Assert.IsNotNull(completed.Statistics);
        Assert.IsTrue(completed.Statistics.CandidateOptionCount >= 2);

        using var scheduleResponse = await client.GetAsync(
            $"/api/admin/schedules/{completed.SchedulePlanId}");
        var schedule = await ReadAsync<SchedulePlanResponse>(scheduleResponse);
        Assert.AreEqual(ScheduleStatus.Draft, schedule.Status);
        Assert.HasCount(1, schedule.Shifts);
        Assert.HasCount(1, schedule.Shifts[0].Segments);

        using var matrixResponse = await client.GetAsync(
            $"/api/admin/schedules/{schedule.Id}/employee-matrix");
        var matrix = await ReadAsync<EmployeeScheduleMatrixResponse>(matrixResponse);
        Assert.IsTrue(matrix.Employees.Any(row =>
            row.Days.Any(day => day.Shifts.Count > 0)));

        using var coverageResponse = await client.GetAsync(
            $"/api/admin/schedules/{schedule.Id}/location-coverage");
        var coverage = await ReadAsync<LocationCoverageResponse>(coverageResponse);
        Assert.IsTrue(coverage.Slots.All(slot => slot.Shortage == 0));

        using var issuesResponse = await client.GetAsync(
            $"/api/admin/schedules/{schedule.Id}/issues");
        var issues = await ReadAsync<ScheduleIssueResponse[]>(issuesResponse);
        Assert.IsEmpty(issues);

        using var changesResponse = await client.GetAsync(
            $"/api/admin/schedules/{schedule.Id}/changes");
        var changes = await ReadAsync<ScheduleChangeResponse[]>(changesResponse);
        Assert.IsTrue(changes.All(item => item.ChangeKind == ShiftChangeKind.New));

        var shift = schedule.Shifts.Single();
        using var explanationResponse = await client.GetAsync(
            $"/api/admin/schedules/{schedule.Id}/shifts/{shift.Id}/explanation");
        var explanation = await ReadAsync<ShiftExplanationResponse>(
            explanationResponse);
        Assert.IsTrue(explanation.ReasonCodes.Contains("CapabilityMatch"));
        Assert.HasCount(1, explanation.Alternatives);

        using var alternativesResponse = await client.GetAsync(
            $"/api/admin/schedules/{schedule.Id}/shifts/{shift.Id}/alternatives");
        var alternatives = await ReadAsync<ScheduleAlternativeResponse[]>(
            alternativesResponse);
        Assert.HasCount(1, alternatives);

        using var openApiResponse = await client.GetAsync("/openapi/v1.json");
        var openApi = await openApiResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, openApiResponse.StatusCode);
        Assert.Contains("\"version\": \"0.5.0-phase3a\"", openApi);
        Assert.Contains("/api/admin/schedule-generations", openApi);
        Assert.Contains("/api/admin/schedules/{scheduleId}/regenerate", openApi);
        Assert.Contains("/api/me/schedule", openApi);
        Assert.Contains("\"name\": \"Idempotency-Key\"", openApi);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var persisted = await dbContext.ScheduleGenerationRuns
            .AsNoTracking()
            .SingleAsync(item => item.Id == completed.Id);
        Assert.AreNotEqual("{}", persisted.InputSnapshotJson);
        Assert.AreEqual(
            completed.InputSnapshotHash,
            persisted.InputSnapshotHash);
        Assert.IsTrue(await dbContext.ShiftExplanations.AnyAsync(item =>
            item.SchedulePlanId == schedule.Id));
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "ScheduleGeneration.Succeeded" &&
            item.EntityId == completed.Id.ToString()));
    }

    [TestMethod]
    public async Task CancelActiveScopeControlAndRestartRecoveryArePersistent()
    {
        await RecreateApplicationAsync(disableWorker: true);
        await SeedPlanningInputAsync();
        await LoginAsync("admin@test.invalid");
        var queued = await SeedRunAsync(
            ScheduleGenerationStatus.Queued,
            PlanningDate,
            "phase3-queued-cancel");

        using var cancelResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedule-generations/{queued.RunId}/cancel",
            new CancelScheduleGenerationRequest(queued.RunVersion));
        var cancelled = await ReadAsync<ScheduleGenerationRunResponse>(
            cancelResponse);
        Assert.AreEqual(ScheduleGenerationStatus.Cancelled, cancelled.Status);
        Assert.AreEqual(ScheduleSolverStatus.Cancelled, cancelled.SolverStatus);

        using var cancelledPlanResponse = await client.GetAsync(
            $"/api/admin/schedules/{queued.PlanId}");
        var cancelledPlan = await ReadAsync<SchedulePlanResponse>(
            cancelledPlanResponse);
        Assert.AreEqual(ScheduleStatus.Draft, cancelledPlan.Status);

        var active = await SeedRunAsync(
            ScheduleGenerationStatus.Queued,
            PlanningDate,
            "phase3-active-scope");
        using var conflictResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/schedule-generations",
            new CreateScheduleGenerationRequest(
                PlanningDate,
                PlanningDate,
                1,
                10,
                1),
            "phase3-conflicting-scope-0001");
        Assert.AreEqual(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Contains(
            "SCHEDULE_GENERATION_ALREADY_ACTIVE",
            await conflictResponse.Content.ReadAsStringAsync());

        await MarkRunAsync(active.RunId, ScheduleGenerationStatus.Cancelled);
        var running = await SeedRunAsync(
            ScheduleGenerationStatus.Running,
            PlanningDate.AddDays(1),
            "phase3-running-restart");

        client.Dispose();
        await application.DisposeAsync();
        application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString());
        client = application.CreateHttpsClient();
        _ = application.Server;

        var recovered = await WaitForDatabaseRunAsync(running.RunId);
        Assert.AreEqual(ScheduleGenerationStatus.Failed, recovered.Status);
        Assert.AreEqual("RECOVERED_AFTER_RESTART", recovered.ErrorCode);
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var planStatus = await dbContext.SchedulePlans
            .Where(item => item.Id == running.PlanId)
            .Select(item => item.Status)
            .SingleAsync();
        Assert.AreEqual(ScheduleStatus.Draft, planStatus);
    }

    [TestMethod]
    public async Task S020AndS021PublishIsImmutableCloneArchivesPreviousAndOwnViewUsesLatest()
    {
        var seeded = await SeedDraftPlanAsync(
            IntegrationTestData.RegularEmployeeId,
            includeExplanation: false);
        await LoginAsync("admin@test.invalid");
        var draft = await GetScheduleAsync(seeded.PlanId);

        var underReview = await ChangeStatusAsync(
            seeded.PlanId,
            "submit-review",
            draft.Version);
        Assert.AreEqual(ScheduleStatus.UnderReview, underReview.Status);
        var approved = await ChangeStatusAsync(
            seeded.PlanId,
            "approve",
            underReview.Version);
        Assert.AreEqual(ScheduleStatus.Approved, approved.Status);
        var published = await ChangeStatusAsync(
            seeded.PlanId,
            "publish",
            approved.Version);
        Assert.AreEqual(ScheduleStatus.Published, published.Status);
        Assert.AreEqual(1, published.PublishedRevisionNumber);

        var publishedShift = published.Shifts.Single();
        using var immutableResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{published.Id}/shifts/{publishedShift.Id}/lock",
            new ShiftVersionRequest(
                publishedShift.Version,
                published.Version,
                "nem módosítható"));
        Assert.AreEqual(
            HttpStatusCode.UnprocessableEntity,
            immutableResponse.StatusCode);
        Assert.Contains(
            "PUBLISHED_SCHEDULE_IMMUTABLE",
            await immutableResponse.Content.ReadAsStringAsync());

        using var cloneResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{published.Id}/clone-draft",
            new CloneScheduleDraftRequest(published.Version),
            "phase3-published-clone-0001");
        var clone = await ReadAsync<SchedulePlanResponse>(cloneResponse);
        Assert.AreEqual(ScheduleStatus.Draft, clone.Status);
        Assert.AreEqual(published.Id, clone.BasedOnScheduleId);

        using var repeatedCloneResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{published.Id}/clone-draft",
            new CloneScheduleDraftRequest(published.Version),
            "phase3-published-clone-0001");
        var repeatedClone = await ReadAsync<SchedulePlanResponse>(
            repeatedCloneResponse);
        Assert.AreEqual(clone.Id, repeatedClone.Id);

        var cloneReview = await ChangeStatusAsync(
            clone.Id,
            "submit-review",
            clone.Version);
        var cloneApproved = await ChangeStatusAsync(
            clone.Id,
            "approve",
            cloneReview.Version);
        var clonePublished = await ChangeStatusAsync(
            clone.Id,
            "publish",
            cloneApproved.Version);
        Assert.AreEqual(2, clonePublished.PublishedRevisionNumber);

        var old = await GetScheduleAsync(published.Id);
        Assert.AreEqual(ScheduleStatus.Archived, old.Status);

        await LoginAsync("dolgozo@test.invalid");
        using var ownResponse = await client.GetAsync(
            $"/api/me/schedule?periodStart={PlanningDate:yyyy-MM-dd}" +
            $"&periodEnd={PlanningDate:yyyy-MM-dd}");
        var own = await ReadAsync<OwnScheduleResponse>(ownResponse);
        Assert.AreEqual(clone.Id, own.ScheduleId);
        Assert.AreEqual(2, own.PublishedRevisionNumber);
        Assert.HasCount(1, own.Shifts);

        await LoginAsync("admin@test.invalid");
        using var changesResponse = await client.GetAsync(
            $"/api/admin/schedules/{clone.Id}/changes");
        var changes = await ReadAsync<ScheduleChangeResponse[]>(changesResponse);
        Assert.IsTrue(changes.All(item =>
            item.ChangeKind == ShiftChangeKind.Unchanged));

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "SchedulePlan.ArchivedByNewPublication" &&
            item.EntityId == published.Id.ToString()));
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "SchedulePlan.Published" &&
            item.EntityId == clone.Id.ToString()));
    }

    [TestMethod]
    public async Task S022EverySchedulePermissionIsIndependent()
    {
        await LoginAsync("admin@test.invalid");

        await SetAdminPermissionsAsync(ApplicationPermission.ManageSchedules);
        using var generationForbidden = await SendWithCsrfAsync(
            HttpMethod.Post,
            "/api/admin/schedule-generations",
            new CreateScheduleGenerationRequest(
                PlanningDate,
                PlanningDate,
                1,
                10,
                1),
            "phase3-permission-generation-0001");
        Assert.AreEqual(HttpStatusCode.Forbidden, generationForbidden.StatusCode);

        await SetAdminPermissionsAsync(ApplicationPermission.RunAutoFill);
        using var approveForbidden = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{Guid.NewGuid()}/approve",
            new ScheduleVersionRequest(1));
        Assert.AreEqual(HttpStatusCode.Forbidden, approveForbidden.StatusCode);

        await SetAdminPermissionsAsync(ApplicationPermission.ApproveSchedules);
        using var publishForbidden = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{Guid.NewGuid()}/publish",
            new ScheduleVersionRequest(1));
        Assert.AreEqual(HttpStatusCode.Forbidden, publishForbidden.StatusCode);
    }

    [TestMethod]
    public async Task S023AndS024TenantBoundaryAndOptimisticConcurrencyProtectMutations()
    {
        var local = await SeedDraftPlanAsync(
            IntegrationTestData.AdminEmployeeId,
            includeExplanation: true);
        var foreign = await SeedForeignScheduleAsync();
        await LoginAsync("admin@test.invalid");

        using var foreignPlan = await client.GetAsync(
            $"/api/admin/schedules/{foreign.PlanId}");
        Assert.AreEqual(HttpStatusCode.NotFound, foreignPlan.StatusCode);
        using var foreignRun = await client.GetAsync(
            $"/api/admin/schedule-generations/{foreign.RunId}");
        Assert.AreEqual(HttpStatusCode.NotFound, foreignRun.StatusCode);
        using var foreignShift = await client.GetAsync(
            $"/api/admin/schedules/{foreign.PlanId}/shifts/" +
            $"{foreign.ShiftId}/explanation");
        Assert.AreEqual(HttpStatusCode.NotFound, foreignShift.StatusCode);

        var schedule = await GetScheduleAsync(local.PlanId);
        var shift = schedule.Shifts.Single();
        var lockRequest = new ShiftVersionRequest(
            shift.Version,
            schedule.Version,
            "konkurencia teszt");
        using var firstLockResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{schedule.Id}/shifts/{shift.Id}/lock",
            lockRequest);
        var locked = await ReadAsync<ShiftAssignmentResponse>(firstLockResponse);
        Assert.IsTrue(locked.IsLocked);

        using var staleLockResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{schedule.Id}/shifts/{shift.Id}/lock",
            lockRequest);
        Assert.AreEqual(HttpStatusCode.Conflict, staleLockResponse.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            1,
            await dbContext.GeneratedSuggestionDecisions.CountAsync(item =>
                item.SchedulePlanId == schedule.Id &&
                item.DecisionType ==
                GeneratedSuggestionDecisionType.Lock));
    }

    [TestMethod]
    public async Task LockRejectReplaceAndRegeneratePreserveDecisionsAndAudit()
    {
        await SeedPlanningInputAsync();
        var replacePlan = await SeedDraftPlanAsync(
            IntegrationTestData.AdminEmployeeId,
            includeExplanation: true);
        await LoginAsync("admin@test.invalid");
        var replaceSchedule = await GetScheduleAsync(replacePlan.PlanId);
        var replaceShift = replaceSchedule.Shifts.Single();

        using var replaceResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{replaceSchedule.Id}/shifts/" +
            $"{replaceShift.Id}/replace",
            new ReplaceShiftRequest(
                IntegrationTestData.RegularEmployeeId,
                replaceShift.Version,
                replaceSchedule.Version,
                "helyettesítés"));
        var replacement = await ReadAsync<ShiftAssignmentResponse>(
            replaceResponse);
        Assert.AreEqual(
            IntegrationTestData.RegularEmployeeId,
            replacement.EmployeeId);
        Assert.AreEqual(ShiftAssignmentSource.Replacement, replacement.Source);

        var rejectPlan = await SeedDraftPlanAsync(
            IntegrationTestData.AdminEmployeeId,
            includeExplanation: true);
        var rejectSchedule = await GetScheduleAsync(rejectPlan.PlanId);
        var rejectShift = rejectSchedule.Shifts.Single();
        using var rejectResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{rejectSchedule.Id}/shifts/" +
            $"{rejectShift.Id}/reject",
            new RejectGeneratedSuggestionRequest(
                rejectShift.Version,
                rejectSchedule.Version,
                "nem megfelelő",
                SuggestionExclusionScope.Schedule));
        Assert.AreEqual(HttpStatusCode.OK, rejectResponse.StatusCode);

        var afterReject = await GetScheduleAsync(rejectSchedule.Id);
        Assert.IsEmpty(afterReject.Shifts);
        using var regenerateResponse = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{rejectSchedule.Id}/regenerate",
            new RegenerateScheduleRequest(
                new RegenerationScopeRequest(
                    RegenerationScopeType.FullPeriod,
                    null,
                    null,
                    null,
                    null,
                    null,
                    []),
                afterReject.Version,
                11,
                10,
                1),
            "phase3-reject-regenerate-0001");
        var regeneration = await ReadAsync<ScheduleGenerationRunResponse>(
            regenerateResponse);
        var completed = await WaitForTerminalRunAsync(regeneration.Id);
        Assert.AreEqual(ScheduleGenerationStatus.Succeeded, completed.Status);

        var regenerated = await GetScheduleAsync(rejectSchedule.Id);
        Assert.HasCount(1, regenerated.Shifts);
        Assert.AreEqual(
            IntegrationTestData.RegularEmployeeId,
            regenerated.Shifts[0].EmployeeId);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.IsTrue(await dbContext.GeneratedSuggestionDecisions.AnyAsync(item =>
            item.SchedulePlanId == rejectSchedule.Id &&
            item.DecisionType == GeneratedSuggestionDecisionType.Reject));
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "ShiftAssignment.Replaced"));
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "ShiftAssignment.Rejected"));
        Assert.IsTrue(await dbContext.AuditLogs.AnyAsync(item =>
            item.Action == "ScheduleGeneration.Succeeded" &&
            item.EntityId == regeneration.Id.ToString()));
    }

    public TestContext TestContext { get; set; } = null!;

    private async Task RecreateApplicationAsync(bool disableWorker)
    {
        client.Dispose();
        await application.DisposeAsync();
        application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString(),
            disableWorker);
        client = application.CreateHttpsClient();
        await application.ResetAndSeedDatabaseAsync();
    }

    private async Task SeedPlanningInputAsync()
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var employees = await dbContext.Employees
            .Where(item =>
                item.Id == IntegrationTestData.AdminEmployeeId ||
                item.Id == IntegrationTestData.RegularEmployeeId)
            .ToArrayAsync();
        foreach (var employee in employees)
        {
            employee.ProfessionalRole = ProfessionalRole.Pharmacist;
            employee.IsSchedulable = true;
            employee.IncludeInAutoFill = true;
            employee.CountsAsPharmacist = true;
            employee.UpdatedAtUtc = now;
            dbContext.EmployeeLocations.Add(new EmployeeLocation
            {
                OrganizationId = IntegrationTestData.OrganizationId,
                EmployeeId = employee.Id,
                LocationId = IntegrationTestData.LocalLocationId,
                Enabled = true
            });
            if (!await dbContext.EmployeeCapabilities.AnyAsync(item =>
                    item.EmployeeId == employee.Id &&
                    item.Capability == StaffingCapability.Pharmacist))
            {
                dbContext.EmployeeCapabilities.Add(new EmployeeCapability
                {
                    OrganizationId = IntegrationTestData.OrganizationId,
                    EmployeeId = employee.Id,
                    Capability = StaffingCapability.Pharmacist,
                    AssignedAtUtc = now
                });
            }

            dbContext.EmployeeWorkProfiles.Add(WorkProfile(employee.Id, now));
        }

        var opening = new LocationWeeklyOpening
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationId = IntegrationTestData.LocalLocationId,
            MondayMode = OpeningDayMode.CustomIntervals,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        opening.Intervals.Add(new OpeningInterval
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationWeeklyOpeningId = opening.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0)
        });
        dbContext.LocationWeeklyOpenings.Add(opening);
        dbContext.LocationShiftTemplates.Add(new LocationShiftTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationId = IntegrationTestData.LocalLocationId,
            Name = "Nappali",
            Category = ShiftTemplateCategory.Custom,
            WeekdayMask = 1 << (int)DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            IsActive = true,
            RequiredCapability = StaffingCapability.Pharmacist,
            TimeType = TimeType.Work,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.CoverageRequirements.Add(new CoverageRequirement
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationId = IntegrationTestData.LocalLocationId,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            RequiredCapability = StaffingCapability.Pharmacist,
            RequiredCount = 1,
            Severity = CoverageSeverity.Blocking,
            TimeType = TimeType.Work,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
    }

    private static EmployeeWorkProfile WorkProfile(
        Guid employeeId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = employeeId,
            ContractedMonthlyMinutes = 0,
            StandardShiftMinutes = 480,
            MinimumShiftMinutes = 240,
            MaximumRegularShiftMinutes = 480,
            MaximumDailyMinutes = 720,
            AllowsLongShift = true,
            MaximumLongShiftMinutes = 720,
            AllowsFullOpeningHoursShift = true,
            AllowsOvertime = true,
            MaximumOvertimeMinutesPerMonth = 1_200,
            AllowsOnCallDuty = false,
            AllowsStandby = false,
            AllowsSaturday = true,
            AllowsSunday = true,
            IncludeInAutoFill = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private async Task<(Guid PlanId, Guid RunId, uint RunVersion)> SeedRunAsync(
        ScheduleGenerationStatus status,
        DateOnly date,
        string idempotencyHash)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var plan = Plan(
            IntegrationTestData.OrganizationId,
            IntegrationTestData.AdminUserId,
            date,
            ScheduleStatus.Generating,
            now);
        var options = ScheduleGenerationOptions.CreateDefault(date, date);
        var run = new ScheduleGenerationRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            SchedulePlanId = plan.Id,
            Status = status,
            RequestedByUserId = IntegrationTestData.AdminUserId,
            RequestedAtUtc = now,
            StartedAtUtc = status == ScheduleGenerationStatus.Running
                ? now
                : null,
            AlgorithmVersion = OrToolsScheduleOptimizer.AlgorithmVersion,
            DeterministicSeed = 1,
            OptionsJson = JsonSerializer.Serialize(options, JsonOptions),
            InputSnapshotJson = "{}",
            InputSnapshotHash = string.Empty,
            SolverStatus = ScheduleSolverStatus.NotStarted,
            SolverStatisticsJson = "{}",
            IdempotencyKeyHash = idempotencyHash,
            ScopeConcurrencyKey = $"{date:yyyyMMdd}-{date:yyyyMMdd}"
        };
        dbContext.SchedulePlans.Add(plan);
        dbContext.ScheduleGenerationRuns.Add(run);
        await dbContext.SaveChangesAsync();
        return (plan.Id, run.Id, run.Version);
    }

    private async Task MarkRunAsync(
        Guid runId,
        ScheduleGenerationStatus status)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        await dbContext.ScheduleGenerationRuns
            .Where(item => item.Id == runId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.CompletedAtUtc, DateTimeOffset.UtcNow));
    }

    private async Task<(Guid PlanId, Guid RunId, Guid ShiftId)> SeedDraftPlanAsync(
        Guid employeeId,
        bool includeExplanation)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var plan = Plan(
            IntegrationTestData.OrganizationId,
            IntegrationTestData.AdminUserId,
            PlanningDate,
            ScheduleStatus.Draft,
            now);
        var run = CompletedRun(
            plan,
            IntegrationTestData.AdminUserId,
            $"direct-{Guid.NewGuid():N}",
            now);
        var shift = Shift(
            plan,
            run,
            employeeId,
            IntegrationTestData.LocalLocationId,
            IntegrationTestData.AdminUserId,
            now);
        dbContext.AddRange(plan, run, shift);
        if (includeExplanation)
        {
            var alternativeEmployeeId =
                employeeId == IntegrationTestData.RegularEmployeeId
                    ? IntegrationTestData.AdminEmployeeId
                    : IntegrationTestData.RegularEmployeeId;
            dbContext.ShiftExplanations.Add(new ShiftExplanation
            {
                Id = Guid.NewGuid(),
                OrganizationId = IntegrationTestData.OrganizationId,
                SchedulePlanId = plan.Id,
                ShiftAssignmentId = shift.Id,
                GenerationRunId = run.Id,
                AlgorithmVersion = OrToolsScheduleOptimizer.AlgorithmVersion,
                ReasonCodesJson = JsonSerializer.Serialize<IReadOnlyList<string>>(
                    ["CapabilityMatch", "CoverageContribution"],
                    JsonOptions),
                ScoreComponentsJson = "{}",
                AlternativesJson =
                    JsonSerializer.Serialize<IReadOnlyList<ScheduleAlternativeScore>>(
                    [
                        new ScheduleAlternativeScore(
                            alternativeEmployeeId,
                            "Alternatív dolgozó",
                            10,
                            new Dictionary<string, long>
                            {
                                ["HoursBalance"] = 10
                            },
                            ["PreviousScheduleChanged"])
                    ],
                    JsonOptions)
            });
        }

        await dbContext.SaveChangesAsync();
        return (plan.Id, run.Id, shift.Id);
    }

    private async Task<(Guid PlanId, Guid RunId, Guid ShiftId)>
        SeedForeignScheduleAsync()
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var foreignUserId = Guid.NewGuid();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = foreignUserId,
            OrganizationId = IntegrationTestData.OtherOrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            UserName = "foreign@test.invalid",
            NormalizedUserName = "FOREIGN@TEST.INVALID",
            Email = "foreign@test.invalid",
            NormalizedEmail = "FOREIGN@TEST.INVALID",
            EmailConfirmed = true,
            DisplayName = "Másik admin",
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        var plan = Plan(
            IntegrationTestData.OtherOrganizationId,
            foreignUserId,
            PlanningDate,
            ScheduleStatus.Draft,
            now);
        var run = CompletedRun(
            plan,
            foreignUserId,
            $"foreign-{Guid.NewGuid():N}",
            now);
        var shift = Shift(
            plan,
            run,
            IntegrationTestData.OtherEmployeeId,
            IntegrationTestData.OtherLocationId,
            foreignUserId,
            now);
        dbContext.AddRange(plan, run, shift);
        dbContext.ShiftExplanations.Add(new ShiftExplanation
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OtherOrganizationId,
            SchedulePlanId = plan.Id,
            ShiftAssignmentId = shift.Id,
            GenerationRunId = run.Id,
            AlgorithmVersion = OrToolsScheduleOptimizer.AlgorithmVersion,
            ReasonCodesJson = "[]",
            ScoreComponentsJson = "{}",
            AlternativesJson = "[]"
        });
        await dbContext.SaveChangesAsync();
        return (plan.Id, run.Id, shift.Id);
    }

    private static SchedulePlan Plan(
        Guid organizationId,
        Guid actorId,
        DateOnly date,
        ScheduleStatus status,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PeriodStart = date,
            PeriodEnd = date,
            TimeZoneId = "Europe/Budapest",
            Status = status,
            AlgorithmVersion = OrToolsScheduleOptimizer.AlgorithmVersion,
            GenerationOptionsSnapshot = "{}",
            InputSnapshotHash = new string('0', 64),
            CreatedByUserId = actorId,
            CreatedAtUtc = now,
            UpdatedByUserId = actorId,
            UpdatedAtUtc = now
        };

    private static ScheduleGenerationRun CompletedRun(
        SchedulePlan plan,
        Guid actorId,
        string idempotencyHash,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = plan.OrganizationId,
            SchedulePlanId = plan.Id,
            Status = ScheduleGenerationStatus.Succeeded,
            RequestedByUserId = actorId,
            RequestedAtUtc = now,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            AlgorithmVersion = OrToolsScheduleOptimizer.AlgorithmVersion,
            DeterministicSeed = 1,
            OptionsJson = "{}",
            InputSnapshotJson = "{}",
            InputSnapshotHash = new string('0', 64),
            SolverStatus = ScheduleSolverStatus.Optimal,
            SolverStatisticsJson = "{}",
            ObjectiveValue = 0,
            IdempotencyKeyHash = idempotencyHash,
            ScopeConcurrencyKey =
                $"{plan.PeriodStart:yyyyMMdd}-{plan.PeriodEnd:yyyyMMdd}"
        };

    private static ShiftAssignment Shift(
        SchedulePlan plan,
        ScheduleGenerationRun run,
        Guid employeeId,
        Guid locationId,
        Guid actorId,
        DateTimeOffset now)
    {
        var shift = new ShiftAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = plan.OrganizationId,
            SchedulePlanId = plan.Id,
            EmployeeId = employeeId,
            LocationId = locationId,
            Date = plan.PeriodStart,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            Source = ShiftAssignmentSource.Generated,
            GeneratedByRunId = run.Id,
            ChangeKind = ShiftChangeKind.New,
            CreatedByUserId = actorId,
            CreatedAtUtc = now,
            UpdatedByUserId = actorId,
            UpdatedAtUtc = now
        };
        shift.Segments.Add(new ShiftSegment
        {
            Id = Guid.NewGuid(),
            OrganizationId = plan.OrganizationId,
            ShiftAssignmentId = shift.Id,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            TimeType = TimeType.Work
        });
        return shift;
    }

    private async Task SetAdminPermissionsAsync(
        params ApplicationPermission[] permissions)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var existing = await dbContext.UserPermissions
            .Where(item => item.UserId == IntegrationTestData.AdminUserId)
            .ToArrayAsync();
        dbContext.UserPermissions.RemoveRange(existing);
        dbContext.UserPermissions.AddRange(permissions.Select(permission =>
            new UserPermission
            {
                OrganizationId = IntegrationTestData.OrganizationId,
                UserId = IntegrationTestData.AdminUserId,
                Permission = permission
            }));
        await dbContext.SaveChangesAsync();
    }

    private async Task<SchedulePlanResponse> ChangeStatusAsync(
        Guid planId,
        string action,
        uint version)
    {
        using var response = await SendWithCsrfAsync(
            HttpMethod.Post,
            $"/api/admin/schedules/{planId}/{action}",
            new ScheduleVersionRequest(version));
        return await ReadAsync<SchedulePlanResponse>(response);
    }

    private async Task<SchedulePlanResponse> GetScheduleAsync(Guid planId)
    {
        using var response = await client.GetAsync(
            $"/api/admin/schedules/{planId}");
        return await ReadAsync<SchedulePlanResponse>(response);
    }

    private async Task<ScheduleGenerationRunResponse> WaitForTerminalRunAsync(
        Guid runId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(
                $"/api/admin/schedule-generations/{runId}");
            var run = await ReadAsync<ScheduleGenerationRunResponse>(response);
            if (run.Status is
                ScheduleGenerationStatus.Succeeded or
                ScheduleGenerationStatus.Failed or
                ScheduleGenerationStatus.Cancelled)
            {
                return run;
            }

            await Task.Delay(100, TestContext.CancellationToken);
        }

        Assert.Fail("A generálási futás 20 másodpercen belül nem fejeződött be.");
        throw new InvalidOperationException();
    }

    private async Task<ScheduleGenerationRun> WaitForDatabaseRunAsync(Guid runId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = application.Services.CreateAsyncScope();
            var dbContext =
                scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            var run = await dbContext.ScheduleGenerationRuns
                .AsNoTracking()
                .SingleAsync(item => item.Id == runId);
            if (run.Status == ScheduleGenerationStatus.Failed)
            {
                return run;
            }

            await Task.Delay(100, TestContext.CancellationToken);
        }

        Assert.Fail("A Running futás restart recovery-je nem fejeződött be.");
        throw new InvalidOperationException();
    }

    private async Task LoginAsync(string email)
    {
        var token = await GetCsrfTokenAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(email, IntegrationTestData.Password),
                options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpMethod method,
        string path,
        object body,
        string? idempotencyKey = null)
    {
        var token = await GetCsrfTokenAsync();
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Add(token.HeaderName, token.RequestToken);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

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

    private static Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        IntegrationJson.ReadSuccessAsync<T>(response);
}
