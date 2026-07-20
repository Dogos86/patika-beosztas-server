using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class EmployeeTimeWindowRulesTests
{
    [TestMethod]
    public void StartAtOrAfterEndIsRejected()
    {
        var windows = new[]
        {
            CreateWindow(
                DayOfWeek.Monday,
                new TimeOnly(12, 0),
                new TimeOnly(8, 0),
                EmployeeTimeWindowType.Forbidden)
        };

        var issues = EmployeeTimeWindowRules.Validate(windows);

        Assert.IsTrue(issues.Any(issue => issue.Code == "TIME_WINDOW_ORDER"));
    }

    [TestMethod]
    public void AllDayAndSpecificDayOverlapIsRejected()
    {
        var windows = new[]
        {
            CreateWindow(
                null,
                new TimeOnly(8, 0),
                new TimeOnly(10, 0),
                EmployeeTimeWindowType.Preferred),
            CreateWindow(
                DayOfWeek.Tuesday,
                new TimeOnly(9, 0),
                new TimeOnly(11, 0),
                EmployeeTimeWindowType.Forbidden)
        };

        var issues = EmployeeTimeWindowRules.Validate(windows);

        Assert.IsTrue(issues.Any(issue => issue.Code == "OVERLAPPING_TIME_WINDOWS"));
    }

    [TestMethod]
    public void TouchingWindowsDoNotOverlap()
    {
        var windows = new[]
        {
            CreateWindow(
                DayOfWeek.Friday,
                new TimeOnly(8, 0),
                new TimeOnly(10, 0),
                EmployeeTimeWindowType.Preferred),
            CreateWindow(
                DayOfWeek.Friday,
                new TimeOnly(10, 0),
                new TimeOnly(12, 0),
                EmployeeTimeWindowType.Forbidden)
        };

        var issues = EmployeeTimeWindowRules.Validate(windows);

        Assert.IsEmpty(issues);
    }

    private static EmployeeTimeWindow CreateWindow(
        DayOfWeek? dayOfWeek,
        TimeOnly start,
        TimeOnly end,
        EmployeeTimeWindowType type) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            StartTime = start,
            EndTime = end,
            Type = type
        };
}
