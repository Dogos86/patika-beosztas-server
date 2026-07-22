using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class EmployeePlanningRulesTests
{
    [TestMethod]
    public void ConsistentWorkProfileIsAccepted()
    {
        var issues = EmployeeWorkProfileRules.Validate(
            ValidProfile(),
            employeeIsActive: true,
            employeeIsSchedulable: true);

        Assert.IsEmpty(issues);
    }

    [TestMethod]
    public void WorkProfileBoundariesAndConditionalLimitsAreEnforced()
    {
        var profile = ValidProfile();
        profile.MinimumShiftMinutes = 600;
        profile.StandardShiftMinutes = 480;
        profile.MaximumRegularShiftMinutes = 420;
        profile.MaximumDailyMinutes = 400;
        profile.AllowsLongShift = true;
        profile.MaximumLongShiftMinutes = 300;
        profile.AllowsOvertime = false;
        profile.MaximumOvertimeMinutesPerMonth = 60;

        var codes = EmployeeWorkProfileRules.Validate(
                profile,
                employeeIsActive: false,
                employeeIsSchedulable: false)
            .Select(issue => issue.Code)
            .ToArray();

        CollectionAssert.Contains(codes, "WORK_PROFILE_SHIFT_LIMIT_ORDER");
        CollectionAssert.Contains(codes, "REGULAR_SHIFT_EXCEEDS_DAILY_MAXIMUM");
        CollectionAssert.Contains(codes, "LONG_SHIFT_MAXIMUM_TOO_SMALL");
        CollectionAssert.Contains(codes, "OVERTIME_LIMIT_MUST_BE_EMPTY");
        CollectionAssert.Contains(codes, "AUTOFILL_REQUIRES_ACTIVE_SCHEDULABLE_EMPLOYEE");
    }

    [TestMethod]
    public void QuotaRequiresNonNegativeOrderedBounds()
    {
        Assert.IsEmpty(EmployeeShiftQuotaRuleRules.Validate(1, 2, 3));
        var codes = EmployeeShiftQuotaRuleRules.Validate(-1, 4, 3)
            .Select(issue => issue.Code)
            .ToArray();
        CollectionAssert.Contains(codes, "SHIFT_QUOTA_NEGATIVE");
        CollectionAssert.Contains(codes, "SHIFT_QUOTA_ORDER");
    }

    private static EmployeeWorkProfile ValidProfile() =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            ContractedMonthlyMinutes = 9_600,
            ContractedWeeklyMinutes = 2_400,
            StandardShiftMinutes = 480,
            MinimumShiftMinutes = 240,
            MaximumRegularShiftMinutes = 600,
            MaximumDailyMinutes = 720,
            AllowsLongShift = true,
            MaximumLongShiftMinutes = 720,
            AllowsFullOpeningHoursShift = false,
            AllowsOvertime = true,
            MaximumOvertimeMinutesPerMonth = 600,
            AllowsOnCallDuty = true,
            MaximumOnCallAssignmentsPerMonth = 4,
            AllowsStandby = false,
            MaximumStandbyAssignmentsPerMonth = null,
            AllowsSaturday = true,
            MaximumSaturdaysPerMonth = 2,
            AllowsSunday = false,
            MaximumSundaysPerMonth = null,
            IncludeInAutoFill = true
        };
}
