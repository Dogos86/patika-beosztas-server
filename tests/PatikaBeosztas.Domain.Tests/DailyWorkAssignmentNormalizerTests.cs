using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class DailyWorkAssignmentNormalizerTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 7, 23);

    [TestMethod]
    public void TouchingWorkIntervalsMergeIntoOneSegment()
    {
        var result = Normalize(
            Interval(8, 14, TimeType.Work),
            Interval(14, 18, TimeType.Work));

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Assignment);
        Assert.AreEqual(new TimeOnly(8, 0), result.Assignment.StartTime);
        Assert.AreEqual(new TimeOnly(18, 0), result.Assignment.EndTime);
        Assert.AreEqual(600, result.Assignment.TotalMinutes);
        Assert.HasCount(1, result.Assignment.Segments);
    }

    [TestMethod]
    public void OverlappingWorkIntervalsMergeWithoutDoubleCounting()
    {
        var result = Normalize(
            Interval(8, 14, TimeType.Work),
            Interval(12, 18, TimeType.Work));

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Assignment);
        Assert.AreEqual(600, result.Assignment.TotalMinutes);
        Assert.HasCount(1, result.Assignment.Segments);
    }

    [TestMethod]
    public void GapIsRejectedAsSplitShift()
    {
        var result = Normalize(
            Interval(8, 12, TimeType.Work),
            Interval(13, 18, TimeType.Work));

        Assert.IsFalse(result.IsValid);
        Assert.Contains("SPLIT_SHIFT_NOT_ALLOWED", result.Issues.Select(issue => issue.Code));
    }

    [TestMethod]
    public void DifferentLocationIsRejected()
    {
        var result = DailyWorkAssignmentNormalizer.Normalize(
            [
                Interval(8, 12, TimeType.Work),
                Interval(12, 18, TimeType.Work) with { LocationId = Guid.NewGuid() }
            ],
            720);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            "MULTI_LOCATION_SAME_DAY_NOT_ALLOWED",
            result.Issues.Select(issue => issue.Code));
    }

    [TestMethod]
    public void TouchingWorkAndOvertimeBecomeOneAssignmentWithTwoSegments()
    {
        var result = Normalize(
            Interval(8, 16, TimeType.Work),
            Interval(16, 18, TimeType.Overtime));

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Assignment);
        Assert.AreEqual(600, result.Assignment.TotalMinutes);
        Assert.HasCount(2, result.Assignment.Segments);
        Assert.AreEqual(TimeType.Work, result.Assignment.Segments[0].TimeType);
        Assert.AreEqual(TimeType.Overtime, result.Assignment.Segments[1].TimeType);
    }

    [TestMethod]
    public void DifferentTimeTypeCannotHideInsideAnEarlierCoveringInterval()
    {
        var result = Normalize(
            Interval(8, 18, TimeType.Work),
            Interval(9, 10, TimeType.Work),
            Interval(11, 12, TimeType.Overtime));

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            "OVERLAPPING_TIME_TYPES_NOT_ALLOWED",
            result.Issues.Select(issue => issue.Code));
    }

    [TestMethod]
    public void DailyMaximumIsEnforced()
    {
        var result = DailyWorkAssignmentNormalizer.Normalize(
            [Interval(8, 18, TimeType.Work)],
            maximumDailyMinutes: 480);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            "MAXIMUM_DAILY_MINUTES_EXCEEDED",
            result.Issues.Select(issue => issue.Code));
    }

    [TestMethod]
    public void BudapestDaylightSavingUsesElapsedInstantDuration()
    {
        var result = DailyWorkAssignmentNormalizer.Normalize(
            [Interval(1, 4, TimeType.Work) with { Date = new DateOnly(2026, 3, 29) }],
            maximumDailyMinutes: 130,
            timeZoneId: "Europe/Budapest");

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Assignment);
        Assert.AreEqual(120, result.Assignment.TotalMinutes);
    }

    private static DailyWorkAssignmentNormalizationResult Normalize(
        params WorkInterval[] intervals) =>
        DailyWorkAssignmentNormalizer.Normalize(intervals, 720);

    private static WorkInterval Interval(int startHour, int endHour, TimeType type) =>
        new(
            EmployeeId,
            Date,
            LocationId,
            new TimeOnly(startHour, 0),
            new TimeOnly(endHour, 0),
            type);
}
