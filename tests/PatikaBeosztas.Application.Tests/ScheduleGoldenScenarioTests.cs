using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Scheduling;

namespace PatikaBeosztas.Application.Tests;

[TestClass]
public sealed class ScheduleGoldenScenarioTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);
    private readonly OrToolsScheduleOptimizer _optimizer = new();

    [TestMethod]
    public async Task S001SimpleSingleLocationWeekHasFullBlockingCoverage()
    {
        var scenario = new Scenario(Monday, Monday.AddDays(4));
        var locationId = scenario.AddLocation("Központ");
        for (var index = 0; index < 2; index++)
        {
            scenario.AddEmployee(
                locationId,
                StaffingCapability.Pharmacist,
                ProfessionalRole.Pharmacist,
                maximumRegularMinutes: 360,
                maximumDailyMinutes: 360,
                contractedWeeklyMinutes: 1_800);
            scenario.AddEmployee(
                locationId,
                StaffingCapability.Assistant,
                ProfessionalRole.Assistant,
                maximumRegularMinutes: 360,
                maximumDailyMinutes: 360,
                contractedWeeklyMinutes: 1_800);
        }

        scenario.AddTemplate(locationId, new(8, 0), new(14, 0));
        scenario.AddTemplate(locationId, new(14, 0), new(20, 0));
        foreach (var day in Enum.GetValues<DayOfWeek>()
                     .Where(day => day is >= DayOfWeek.Monday and <= DayOfWeek.Friday))
        {
            scenario.AddCoverage(
                locationId,
                day,
                StaffingCapability.Pharmacist);
            scenario.AddCoverage(
                locationId,
                day,
                StaffingCapability.Assistant);
        }

        var result = await OptimizeAsync(scenario);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Issues.Any(issue =>
            issue.Code == "COVERAGE_SHORTAGE"));
        Assert.IsTrue(result.Assignments
            .GroupBy(item => new
            {
                item.Candidate.EmployeeId,
                item.Candidate.Date
            })
            .All(group => group.Count() <= 1));
        Assert.AreEqual(20, result.Assignments.Count);
    }

    [TestMethod]
    public void S002AdjacentWorkTemplatesBecomeOneContinuousAssignmentOption()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            maximumRegularMinutes: 600,
            maximumDailyMinutes: 600);
        scenario.AddTemplate(locationId, new(8, 0), new(14, 0));
        scenario.AddTemplate(locationId, new(14, 0), new(18, 0));

        var candidate = Build(scenario).OptimizerInput.Candidates.Single(item =>
            item.StartTime == new TimeOnly(8, 0) &&
            item.EndTime == new TimeOnly(18, 0));

        Assert.HasCount(1, candidate.Segments);
        Assert.AreEqual(TimeType.Work, candidate.Segments[0].TimeType);
    }

    [TestMethod]
    public void S003LongPresenceIsSplitIntoWorkAndOvertimeSegments()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            maximumRegularMinutes: 480,
            maximumDailyMinutes: 600,
            allowsOvertime: true);
        scenario.AddTemplate(locationId, new(8, 0), new(18, 0));

        var candidate = Build(scenario).OptimizerInput.Candidates.Single();

        Assert.HasCount(2, candidate.Segments);
        Assert.AreEqual(
            new SnapshotShiftSegment(
                new TimeOnly(8, 0),
                new TimeOnly(16, 0),
                TimeType.Work),
            candidate.Segments[0]);
        Assert.AreEqual(TimeType.Overtime, candidate.Segments[1].TimeType);
        Assert.AreEqual(120, candidate.OvertimeMinutes);
    }

    [TestMethod]
    public async Task S004SplitShiftIsNotGeneratedAndManualInputIsRejected()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            maximumRegularMinutes: 600,
            maximumDailyMinutes: 600);
        scenario.AddTemplate(locationId, new(8, 0), new(14, 0));
        scenario.AddTemplate(locationId, new(15, 0), new(18, 0));
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(14, 0));
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(15, 0),
            new(18, 0));

        var build = Build(scenario);
        Assert.IsFalse(build.OptimizerInput.Candidates.Any(item =>
            item.StartTime == new TimeOnly(8, 0) &&
            item.EndTime == new TimeOnly(18, 0)));
        var result = await _optimizer.OptimizeAsync(
            build.OptimizerInput,
            TestContext.CancellationToken);
        Assert.IsTrue(result.Assignments.Count <= 1);
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "COVERAGE_SHORTAGE"));

        var issues = SchedulePlanRules.ValidateDailyAssignments(
        [
            (locationId, new TimeOnly(8, 0), new TimeOnly(14, 0)),
            (locationId, new TimeOnly(15, 0), new TimeOnly(18, 0))
        ]);
        Assert.IsTrue(issues.Any(issue =>
            issue.Code == "SPLIT_SHIFT_NOT_ALLOWED"));
    }

    [TestMethod]
    public async Task S005OneEmployeeCannotCoverTwoLocationsOnTheSameDay()
    {
        var scenario = new Scenario(Monday, Monday);
        var firstLocationId = scenario.AddLocation("A");
        var secondLocationId = scenario.AddLocation("B");
        var employeeId = scenario.AddEmployee(
            firstLocationId,
            StaffingCapability.Pharmacist);
        scenario.EmployeeLocations.Add(new(
            employeeId,
            secondLocationId,
            Enabled: true));
        scenario.AddTemplate(firstLocationId, new(8, 0), new(14, 0));
        scenario.AddTemplate(secondLocationId, new(14, 0), new(18, 0));
        scenario.AddCoverage(
            firstLocationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(14, 0));
        scenario.AddCoverage(
            secondLocationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(14, 0),
            new(18, 0));

        var result = await OptimizeAsync(scenario);

        Assert.IsTrue(result.Assignments.Count <= 1);
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "COVERAGE_SHORTAGE"));
        var issues = SchedulePlanRules.ValidateDailyAssignments(
        [
            (firstLocationId, new TimeOnly(8, 0), new TimeOnly(14, 0)),
            (secondLocationId, new TimeOnly(14, 0), new TimeOnly(18, 0))
        ]);
        Assert.IsTrue(issues.Any(issue =>
            issue.Code == "MULTI_LOCATION_SAME_DAY_NOT_ALLOWED"));
    }

    [TestMethod]
    public void S006AndS007ActiveLeaveStatusesRemoveEveryWorkCandidate()
    {
        foreach (var status in new[]
                 {
                     LeaveRequestStatus.Approved,
                     LeaveRequestStatus.Reported,
                     LeaveRequestStatus.Recorded,
                     LeaveRequestStatus.Closed
                 })
        {
            var scenario = OneEmployeeOneShift();
            scenario.Leaves.Add(new(
                Guid.NewGuid(),
                scenario.Employees[0].Id,
                status == LeaveRequestStatus.Approved
                    ? LeaveType.AnnualLeave
                    : LeaveType.SickLeave,
                Monday,
                Monday,
                IsFullDay: true,
                null,
                null,
                status));

            Assert.IsEmpty(Build(scenario).OptimizerInput.Candidates);
        }
    }

    [TestMethod]
    public async Task S008PendingLeaveSupportsBothRequiredModes()
    {
        var scenario = OneEmployeeOneShift();
        scenario.Leaves.Add(new(
            Guid.NewGuid(),
            scenario.Employees[0].Id,
            LeaveType.AnnualLeave,
            Monday,
            Monday,
            IsFullDay: true,
            null,
            null,
            LeaveRequestStatus.Pending));

        var ignoreBuild = Build(scenario);
        Assert.IsTrue(ignoreBuild.OptimizerInput.Candidates.Single()
            .HasPendingLeaveOverlap);
        var ignoreResult = await _optimizer.OptimizeAsync(
            ignoreBuild.OptimizerInput,
            TestContext.CancellationToken);
        Assert.IsTrue(ignoreResult.Issues.Any(issue =>
            issue.Code == "PENDING_LEAVE_OVERLAP"));

        scenario.Options = scenario.Options with
        {
            PendingLeaveHandling =
                PendingLeaveHandlingMode.TreatAsTemporaryAbsence
        };
        Assert.IsEmpty(Build(scenario).OptimizerInput.Candidates);
    }

    [TestMethod]
    public void S009UnavailableIsHardAndPreferredIsSoft()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        var employeeId = scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist);
        scenario.AddTemplate(locationId, new(8, 0), new(14, 0));
        scenario.AddTemplate(locationId, new(14, 0), new(20, 0));
        scenario.Preferences.Add(new(
            Guid.NewGuid(),
            employeeId,
            WorkPreferenceType.Unavailable,
            Monday,
            Monday,
            null,
            IsFullDay: false,
            new TimeOnly(14, 0),
            new TimeOnly(20, 0),
            locationId,
            IsActive: true));
        scenario.Preferences.Add(new(
            Guid.NewGuid(),
            employeeId,
            WorkPreferenceType.Preferred,
            Monday,
            Monday,
            null,
            IsFullDay: false,
            new TimeOnly(8, 0),
            new TimeOnly(14, 0),
            locationId,
            IsActive: true));

        var candidates = Build(scenario).OptimizerInput.Candidates;

        Assert.IsFalse(candidates.Any(item =>
            item.StartTime >= new TimeOnly(14, 0)));
        Assert.IsTrue(candidates.Single().HasPreferredMatch);
    }

    [TestMethod]
    public async Task S010FixedPreferenceIsForcedOrReportedAsConflict()
    {
        var scenario = OneEmployeeOneShift();
        var employeeId = scenario.Employees[0].Id;
        var locationId = scenario.Locations[0].Id;
        scenario.Preferences.Add(new(
            Guid.NewGuid(),
            employeeId,
            WorkPreferenceType.Fixed,
            Monday,
            Monday,
            null,
            IsFullDay: false,
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            locationId,
            IsActive: true));

        var build = Build(scenario);
        var fixedCandidate = build.OptimizerInput.Candidates.Single();
        Assert.IsTrue(fixedCandidate.IsFixed);
        var result = await _optimizer.OptimizeAsync(
            build.OptimizerInput,
            TestContext.CancellationToken);
        Assert.IsTrue(result.Assignments.Single().ReasonCodes.Contains("FixedRule"));

        scenario.Openings.Clear();
        scenario.AddOpening(locationId, Monday.DayOfWeek, new(9, 0), new(20, 0));
        Assert.IsTrue(Build(scenario).InputIssues.Any(issue =>
            issue.Code == "FIXED_RULE_CONFLICT"));
    }

    [TestMethod]
    public async Task S011SpecialistCapabilitiesSatisfyBaseCoverage()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(
            locationId,
            StaffingCapability.SpecialistPharmacist,
            ProfessionalRole.Pharmacist);
        scenario.AddTemplate(
            locationId,
            new(8, 0),
            new(16, 0),
            StaffingCapability.Pharmacist);
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(16, 0));

        var result = await OptimizeAsync(scenario);

        Assert.IsFalse(result.Issues.Any(issue =>
            issue.Code == "COVERAGE_SHORTAGE"));
        Assert.IsTrue(result.Assignments.Single().ReasonCodes
            .Contains("CapabilityMatch"));
    }

    [TestMethod]
    public void S012InactiveLocationRetainsInputButProducesNoCandidatesOrCoverage()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Bezárt", isActive: false);
        scenario.AddEmployee(locationId, StaffingCapability.Pharmacist);
        scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist);

        var build = Build(scenario);

        Assert.IsEmpty(build.OptimizerInput.Candidates);
        Assert.IsEmpty(build.OptimizerInput.CoverageSlots);
        var issue = build.InputIssues.Single(item =>
            item.Code == "NO_CANDIDATE_OPTIONS");
        Assert.AreEqual(0, issue.Parameters["candidateOptionCount"]);
        Assert.AreEqual(0, issue.Parameters["activeLocationCount"]);
        Assert.IsFalse(scenario.Locations.Single().IsActive);
    }

    [TestMethod]
    public void PreflightReportsNoCoverageWithoutClaimingUsefulCoverage()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(locationId, StaffingCapability.Pharmacist);
        scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
        var snapshot = scenario.Build();
        var build = ScheduleCandidateBuilder.Build(snapshot, "hash");

        var preflight = ScheduleGenerationDiagnostics.Analyze(
            snapshot,
            build.OptimizerInput.Candidates.Count);

        Assert.IsFalse(preflight.CanStart);
        Assert.IsTrue(preflight.Issues.Any(issue =>
            issue.Code == "NO_COVERAGE_REQUIREMENTS"));
        Assert.AreEqual(0, preflight.Counts.CoverageRequirementCount);
    }

    [TestMethod]
    public void PreflightReportsMissingWorkProfileAndLocationAssignment()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(locationId, StaffingCapability.Pharmacist);
        scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(16, 0));
        scenario.Profiles.Clear();
        scenario.EmployeeLocations.Clear();
        var snapshot = scenario.Build();
        var build = ScheduleCandidateBuilder.Build(snapshot, "hash");

        var preflight = ScheduleGenerationDiagnostics.Analyze(
            snapshot,
            build.OptimizerInput.Candidates.Count);

        Assert.IsTrue(preflight.Issues.Any(issue =>
            issue.Code == "MISSING_WORK_PROFILE"));
        Assert.IsTrue(preflight.Issues.Any(issue =>
            issue.Code == "MISSING_LOCATION_ASSIGNMENT"));
        Assert.IsTrue(preflight.Issues.Any(issue =>
            issue.Code == "NO_CANDIDATE_OPTIONS"));
        Assert.AreEqual(0, preflight.Counts.WorkProfileEmployeeCount);
        Assert.AreEqual(0, preflight.Counts.LocationAssignedEmployeeCount);
    }

    [TestMethod]
    public async Task S013ImpossibleBlockingCoverageReturnsDraftableShortage()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(
            locationId,
            StaffingCapability.Assistant,
            ProfessionalRole.Assistant);
        scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(16, 0));

        var result = await OptimizeAsync(scenario);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "COVERAGE_SHORTAGE" &&
            issue.Severity == ScheduleIssueSeverity.Blocking));
        Assert.IsTrue(SchedulePlanRules.ValidateTransition(
            ScheduleStatus.UnderReview,
            ScheduleStatus.Approved,
            hasBlockingIssues: true).Any(issue =>
                issue.Code == "BLOCKING_SCHEDULE_ISSUES"));
    }

    [TestMethod]
    public async Task S014HoursBalancePrefersTheLessLoadedEmployee()
    {
        var scenario = new Scenario(Monday, Monday.AddDays(2));
        var locationId = scenario.AddLocation("Központ");
        var loadedEmployeeId = scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            contractedMonthlyMinutes: 9_920);
        var lessLoadedEmployeeId = scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            contractedMonthlyMinutes: 9_920);
        scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
        foreach (var day in new[]
                 {
                     Monday.DayOfWeek,
                     Monday.AddDays(1).DayOfWeek,
                     Monday.AddDays(2).DayOfWeek
                 })
        {
            scenario.AddCoverage(
                locationId,
                day,
                StaffingCapability.Pharmacist,
                new(8, 0),
                new(16, 0));
        }

        foreach (var date in new[] { Monday, Monday.AddDays(1) })
        {
            scenario.ExistingShifts.Add(ExistingWork(
                loadedEmployeeId,
                locationId,
                date,
                isLocked: true));
        }

        var result = await OptimizeAsync(scenario);

        Assert.IsTrue(result.Assignments.Any(item =>
            item.Candidate.EmployeeId == lessLoadedEmployeeId &&
            item.Candidate.Date == Monday.AddDays(2)));
        Assert.IsTrue(result.Assignments.All(item =>
            item.ReasonCodes.Contains("HoursBalance")));
    }

    [TestMethod]
    public async Task S015SaturdayEligibilityAndMonthlyMaximumAreHardLimits()
    {
        var firstSaturday = new DateOnly(2026, 8, 1);
        var scenario = new Scenario(firstSaturday, firstSaturday.AddDays(14));
        var locationId = scenario.AddLocation("Központ");
        var excludedEmployeeId = scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            allowsSaturday: false);
        var eligibleEmployeeId = scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            maximumSaturdays: 2);
        scenario.AddTemplate(
            locationId,
            new(8, 0),
            new(16, 0),
            weekdayMask: 1 << (int)DayOfWeek.Saturday);
        scenario.AddCoverage(
            locationId,
            DayOfWeek.Saturday,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(16, 0));

        var result = await OptimizeAsync(scenario);

        Assert.IsFalse(result.Assignments.Any(item =>
            item.Candidate.EmployeeId == excludedEmployeeId));
        Assert.AreEqual(2, result.Assignments.Count(item =>
            item.Candidate.EmployeeId == eligibleEmployeeId));
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "COVERAGE_SHORTAGE" &&
            issue.Severity == ScheduleIssueSeverity.Blocking));
    }

    [TestMethod]
    public async Task S016OnCallAndStandbyUseExplicitSegmentsAndLimits()
    {
        var scenario = new Scenario(Monday, Monday.AddDays(1));
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            allowsOnCall: true,
            maximumOnCall: 1,
            allowsStandby: true,
            maximumStandby: 1);
        scenario.AddTemplate(
            locationId,
            new(8, 0),
            new(12, 0),
            timeType: TimeType.OnCallDuty,
            weekdayMask: 1 << (int)Monday.DayOfWeek);
        scenario.AddTemplate(
            locationId,
            new(12, 0),
            new(16, 0),
            timeType: TimeType.Standby,
            weekdayMask: 1 << (int)Monday.AddDays(1).DayOfWeek);
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(12, 0),
            TimeType.OnCallDuty);
        scenario.AddCoverage(
            locationId,
            Monday.AddDays(1).DayOfWeek,
            StaffingCapability.Pharmacist,
            new(12, 0),
            new(16, 0),
            TimeType.Standby);

        var result = await OptimizeAsync(scenario);

        Assert.HasCount(2, result.Assignments);
        CollectionAssert.AreEquivalent(
            new[] { TimeType.OnCallDuty, TimeType.Standby },
            result.Assignments
                .Select(item => item.Candidate.Segments.Single().TimeType)
                .ToArray());
    }

    [TestMethod]
    public async Task S017IdenticalSnapshotSeedAndVersionAreDeterministic()
    {
        var scenario = OneEmployeeOneShift();
        var snapshot = scenario.Build();
        var canonical = ScheduleSnapshotCanonicalizer.Serialize(snapshot);
        var hash = ScheduleSnapshotCanonicalizer.ComputeHash(canonical);
        var firstInput = ScheduleCandidateBuilder.Build(snapshot, hash).OptimizerInput;
        var secondSnapshot = scenario.Build();
        var secondCanonical = ScheduleSnapshotCanonicalizer.Serialize(secondSnapshot);
        var secondHash = ScheduleSnapshotCanonicalizer.ComputeHash(secondCanonical);
        var secondInput = ScheduleCandidateBuilder.Build(
            secondSnapshot,
            secondHash).OptimizerInput;

        var first = await _optimizer.OptimizeAsync(
            firstInput,
            TestContext.CancellationToken);
        var second = await _optimizer.OptimizeAsync(
            secondInput,
            TestContext.CancellationToken);

        Assert.AreEqual(canonical, secondCanonical);
        Assert.AreEqual(hash, secondHash);
        CollectionAssert.AreEqual(
            first.Assignments.Select(item => item.Candidate.Key).ToArray(),
            second.Assignments.Select(item => item.Candidate.Key).ToArray());
        CollectionAssert.AreEqual(
            first.Assignments.SelectMany(item => item.ReasonCodes).ToArray(),
            second.Assignments.SelectMany(item => item.ReasonCodes).ToArray());
    }

    [TestMethod]
    public async Task S018LockedAssignmentSurvivesRegeneration()
    {
        var scenario = OneEmployeeOneShift();
        var employeeId = scenario.Employees[0].Id;
        var locationId = scenario.Locations[0].Id;
        var locked = ExistingWork(
            employeeId,
            locationId,
            Monday,
            isLocked: true);
        scenario.ExistingShifts.Add(locked);

        var result = await OptimizeAsync(scenario);

        Assert.IsTrue(result.Assignments.Any(item =>
            item.Candidate.ExistingShiftId == locked.Id &&
            item.Candidate.IsLocked));
    }

    [TestMethod]
    public async Task S019RejectedSuggestionStaysExcludedAndAlternativesAreHardValid()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        var firstEmployeeId = scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            contractedMonthlyMinutes: 0);
        var secondEmployeeId = scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist,
            contractedMonthlyMinutes: 0);
        scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(16, 0));

        var firstResult = await OptimizeAsync(scenario);
        var selected = firstResult.Assignments.Single();
        Assert.HasCount(1, selected.Alternatives);
        Assert.AreNotEqual(
            selected.Candidate.EmployeeId,
            selected.Alternatives[0].EmployeeId);

        scenario.RejectedSuggestions.Add(new(
            Guid.NewGuid(),
            selected.Candidate.EmployeeId,
            locationId,
            Monday,
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            SuggestionExclusionScope.Schedule));
        var rebuilt = Build(scenario);

        Assert.IsFalse(rebuilt.OptimizerInput.Candidates.Any(item =>
            item.EmployeeId == selected.Candidate.EmployeeId));
        Assert.IsTrue(rebuilt.OptimizerInput.Candidates.Any(item =>
            item.EmployeeId == (selected.Candidate.EmployeeId == firstEmployeeId
                ? secondEmployeeId
                : firstEmployeeId)));
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task S025EightLocationsFortyEmployeesThirtyOneDaysNativeSmoke()
    {
        var start = new DateOnly(2026, 8, 1);
        var scenario = new Scenario(start, start.AddDays(30))
        {
            Options = ScheduleGenerationOptions.CreateDefault(
                start,
                start.AddDays(30)) with
            {
                MaxSolveSeconds = 20
            }
        };
        for (var locationIndex = 0; locationIndex < 8; locationIndex++)
        {
            var locationId = scenario.AddLocation($"Patika {locationIndex + 1}");
            for (var employeeIndex = 0; employeeIndex < 5; employeeIndex++)
            {
                scenario.AddEmployee(
                    locationId,
                    StaffingCapability.Pharmacist,
                    contractedMonthlyMinutes: 2_976);
            }

            scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                scenario.AddCoverage(
                    locationId,
                    day,
                    StaffingCapability.Pharmacist,
                    new(8, 0),
                    new(16, 0));
            }
        }

        var result = await OptimizeAsync(scenario);

        TestContext.WriteLine(
            "S-025 candidate={0}, variable={1}, constraint={2}, wallSeconds={3:F3}, status={4}",
            result.Statistics.CandidateOptionCount,
            result.Statistics.VariableCount,
            result.Statistics.ConstraintCount,
            result.Statistics.WallTimeSeconds,
            result.Status);
        Assert.IsTrue(result.Status is
            ScheduleSolverStatus.Optimal or
            ScheduleSolverStatus.Feasible or
            ScheduleSolverStatus.Unknown);
        Assert.IsTrue(result.Statistics.CandidateOptionCount >= 1_240);
        Assert.IsTrue(result.Statistics.VariableCount > 0);
        Assert.IsTrue(result.Statistics.ConstraintCount > 0);
        Assert.IsTrue(result.Statistics.WallTimeSeconds <= 60);
        Assert.AreNotEqual(
            "SOLVER_RUNTIME_FAILURE",
            result.ErrorCode);
    }

    public TestContext TestContext { get; set; } = null!;

    private async Task<ScheduleOptimizationResult> OptimizeAsync(
        Scenario scenario)
    {
        var build = Build(scenario);
        Assert.IsFalse(build.InputIssues.Any(issue =>
            issue.Severity == ScheduleIssueSeverity.Blocking));
        return await _optimizer.OptimizeAsync(
            build.OptimizerInput,
            TestContext.CancellationToken);
    }

    private static ScheduleCandidateBuildResult Build(Scenario scenario)
    {
        var snapshot = scenario.Build();
        var canonical = ScheduleSnapshotCanonicalizer.Serialize(snapshot);
        return ScheduleCandidateBuilder.Build(
            snapshot,
            ScheduleSnapshotCanonicalizer.ComputeHash(canonical));
    }

    private static Scenario OneEmployeeOneShift()
    {
        var scenario = new Scenario(Monday, Monday);
        var locationId = scenario.AddLocation("Központ");
        scenario.AddEmployee(
            locationId,
            StaffingCapability.Pharmacist);
        scenario.AddTemplate(locationId, new(8, 0), new(16, 0));
        scenario.AddCoverage(
            locationId,
            Monday.DayOfWeek,
            StaffingCapability.Pharmacist,
            new(8, 0),
            new(16, 0));
        return scenario;
    }

    private static SnapshotExistingShift ExistingWork(
        Guid employeeId,
        Guid locationId,
        DateOnly date,
        bool isLocked) =>
        new(
            Guid.NewGuid(),
            employeeId,
            locationId,
            date,
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            isLocked,
            ShiftAssignmentSource.Generated,
            [
                new(
                    new TimeOnly(8, 0),
                    new TimeOnly(16, 0),
                    TimeType.Work)
            ]);

    private sealed class Scenario
    {
        public Scenario(DateOnly periodStart, DateOnly periodEnd)
        {
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
            Options = ScheduleGenerationOptions.CreateDefault(
                periodStart,
                periodEnd);
        }

        public DateOnly PeriodStart { get; }
        public DateOnly PeriodEnd { get; }
        public ScheduleGenerationOptions Options { get; set; }
        public List<SnapshotLocation> Locations { get; } = [];
        public List<SnapshotOpeningInterval> Openings { get; } = [];
        public List<SnapshotShiftTemplate> Templates { get; } = [];
        public List<SnapshotCoverageRequirement> Coverage { get; } = [];
        public List<SnapshotEmployee> Employees { get; } = [];
        public List<SnapshotEmployeeLocation> EmployeeLocations { get; } = [];
        public List<SnapshotEmployeeCapability> Capabilities { get; } = [];
        public List<SnapshotEmployeeWorkProfile> Profiles { get; } = [];
        public List<SnapshotShiftQuota> Quotas { get; } = [];
        public List<SnapshotWorkPreference> Preferences { get; } = [];
        public List<SnapshotLeave> Leaves { get; } = [];
        public List<SnapshotExistingShift> ExistingShifts { get; } = [];
        public List<SnapshotRejectedSuggestion> RejectedSuggestions { get; } = [];

        public Guid AddLocation(string name, bool isActive = true)
        {
            var id = Guid.NewGuid();
            Locations.Add(new(id, name, isActive));
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                AddOpening(id, day, new TimeOnly(8, 0), new TimeOnly(20, 0));
            }

            return id;
        }

        public void AddOpening(
            Guid locationId,
            DayOfWeek day,
            TimeOnly start,
            TimeOnly end) =>
            Openings.Add(new(
                locationId,
                day,
                OpeningDayMode.CustomIntervals,
                start,
                end));

        public Guid AddEmployee(
            Guid locationId,
            StaffingCapability capability,
            ProfessionalRole role = ProfessionalRole.Pharmacist,
            int contractedMonthlyMinutes = 9_920,
            int? contractedWeeklyMinutes = null,
            int maximumRegularMinutes = 480,
            int maximumDailyMinutes = 720,
            bool allowsOvertime = false,
            bool allowsSaturday = true,
            int? maximumSaturdays = null,
            bool allowsOnCall = false,
            int? maximumOnCall = null,
            bool allowsStandby = false,
            int? maximumStandby = null)
        {
            var id = Guid.NewGuid();
            Employees.Add(new(
                id,
                $"Dolgozó {Employees.Count + 1}",
                role,
                IsActive: true,
                IsSchedulable: true,
                IncludeInAutoFill: true,
                CountsAsPharmacist:
                    role == ProfessionalRole.Pharmacist));
            EmployeeLocations.Add(new(id, locationId, Enabled: true));
            Capabilities.Add(new(id, capability));
            Profiles.Add(new(
                id,
                contractedMonthlyMinutes,
                contractedWeeklyMinutes,
                StandardShiftMinutes: 480,
                MinimumShiftMinutes: 30,
                maximumRegularMinutes,
                maximumDailyMinutes,
                AllowsLongShift: true,
                MaximumLongShiftMinutes: 720,
                AllowsFullOpeningHoursShift: true,
                allowsOvertime,
                MaximumOvertimeMinutesPerMonth:
                    allowsOvertime ? 2_400 : 0,
                allowsOnCall,
                maximumOnCall,
                allowsStandby,
                maximumStandby,
                allowsSaturday,
                maximumSaturdays,
                AllowsSunday: true,
                MaximumSundaysPerMonth: null,
                IncludeInAutoFill: true));
            return id;
        }

        public void AddTemplate(
            Guid locationId,
            TimeOnly start,
            TimeOnly end,
            StaffingCapability? capability = null,
            TimeType timeType = TimeType.Work,
            int weekdayMask = 0x7F) =>
            Templates.Add(new(
                Guid.NewGuid(),
                locationId,
                $"Sablon {Templates.Count + 1}",
                ShiftTemplateCategory.Custom,
                weekdayMask,
                start,
                end,
                IsActive: true,
                capability,
                timeType));

        public void AddCoverage(
            Guid locationId,
            DayOfWeek day,
            StaffingCapability capability,
            TimeOnly? start = null,
            TimeOnly? end = null,
            TimeType timeType = TimeType.Work) =>
            Coverage.Add(new(
                Guid.NewGuid(),
                locationId,
                day,
                start ?? new TimeOnly(8, 0),
                end ?? new TimeOnly(20, 0),
                capability,
                RequiredCount: 1,
                CoverageSeverity.Blocking,
                IsActive: true,
                timeType));

        public ScheduleInputSnapshot Build() =>
            new(
                Guid.Parse("91C0722B-84D0-49AA-86E4-B9B82E8CFE7B"),
                "Teszt szervezet",
                "Europe/Budapest",
                PeriodStart,
                PeriodEnd,
                OrToolsScheduleOptimizer.AlgorithmVersion,
                Options,
                Locations.ToArray(),
                Openings.ToArray(),
                Templates.ToArray(),
                Coverage.ToArray(),
                Employees.ToArray(),
                EmployeeLocations.ToArray(),
                Capabilities.ToArray(),
                Profiles.ToArray(),
                Quotas.ToArray(),
                Preferences.ToArray(),
                Leaves.ToArray(),
                ExistingShifts.ToArray(),
                RejectedSuggestions.ToArray());
    }
}
