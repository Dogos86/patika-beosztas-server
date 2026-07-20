using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class EmployeeRulesTests
{
    [TestMethod]
    public void NullableLimitsAndBoundaryValuesAreAccepted()
    {
        var currentDate = new DateOnly(2026, 7, 20);

        var nullableIssues = EmployeeRules.ValidateConfiguration(
            true,
            true,
            false,
            null,
            null,
            null,
            currentDate);
        var boundaryIssues = EmployeeRules.ValidateConfiguration(
            true,
            true,
            true,
            44_640,
            1_440,
            EmployeeRules.MinimumBirthDate,
            currentDate);

        Assert.IsEmpty(nullableIssues);
        Assert.IsEmpty(boundaryIssues);
    }

    [TestMethod]
    public void InvalidLimitsDatesAndAutofillCombinationAreRejected()
    {
        var issues = EmployeeRules.ValidateConfiguration(
            false,
            false,
            true,
            0,
            1_441,
            new DateOnly(1899, 12, 31),
            new DateOnly(2026, 7, 20));

        Assert.IsTrue(issues.Any(issue => issue.Code == "MONTHLY_MINUTES_OUT_OF_RANGE"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "MAX_DAILY_MINUTES_OUT_OF_RANGE"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "BIRTH_DATE_TOO_EARLY"));
        Assert.IsTrue(issues.Any(issue =>
            issue.Code == "AUTOFILL_REQUIRES_ACTIVE_SCHEDULABLE_EMPLOYEE"));
    }
}
