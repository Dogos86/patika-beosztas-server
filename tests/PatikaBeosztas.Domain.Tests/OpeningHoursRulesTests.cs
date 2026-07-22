using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class OpeningHoursRulesTests
{
    [TestMethod]
    public void ClosedOpen24AndMultipleCustomIntervalsAreAccepted()
    {
        var days = ClosedWeek();
        days[(int)DayOfWeek.Monday] = new(
            DayOfWeek.Monday,
            OpeningDayMode.Open24Hours,
            []);
        days[(int)DayOfWeek.Tuesday] = new(
            DayOfWeek.Tuesday,
            OpeningDayMode.CustomIntervals,
            [
                new(new TimeOnly(0, 0), new TimeOnly(12, 0)),
                new(new TimeOnly(13, 0), null)
            ]);
        days[(int)DayOfWeek.Wednesday] = new(
            DayOfWeek.Wednesday,
            OpeningDayMode.CustomIntervals,
            [new(new TimeOnly(0, 0), null)]);

        var issues = OpeningHoursRules.ValidateWeek(days);

        Assert.IsEmpty(issues);
        Assert.IsTrue(OpeningHoursRules.Contains(
            days[(int)DayOfWeek.Monday],
            new TimeOnly(0, 0),
            new TimeOnly(23, 59)));
        Assert.IsTrue(OpeningHoursRules.Contains(
            days[(int)DayOfWeek.Tuesday],
            new TimeOnly(13, 0),
            new TimeOnly(23, 59)));
        Assert.IsTrue(OpeningHoursRules.Contains(
            days[(int)DayOfWeek.Wednesday],
            new TimeOnly(0, 0),
            new TimeOnly(23, 59)));
        Assert.IsFalse(OpeningHoursRules.Contains(
            days[(int)DayOfWeek.Sunday],
            new TimeOnly(8, 0),
            new TimeOnly(9, 0)));
    }

    [TestMethod]
    public void MissingDuplicateUnsortedAndOverlappingIntervalsAreRejected()
    {
        var days = ClosedWeek().ToList();
        days.RemoveAt((int)DayOfWeek.Saturday);
        days.Add(new(
            DayOfWeek.Monday,
            OpeningDayMode.CustomIntervals,
            [
                new(new TimeOnly(12, 0), new TimeOnly(16, 0)),
                new(new TimeOnly(8, 0), new TimeOnly(13, 0))
            ]));

        var codes = OpeningHoursRules.ValidateWeek(days)
            .Select(issue => issue.Code)
            .ToArray();

        CollectionAssert.Contains(codes, "DUPLICATE_OPENING_DAY");
        CollectionAssert.Contains(codes, "OPENING_WEEK_REQUIRES_SEVEN_DAYS");
        CollectionAssert.Contains(codes, "OPENING_INTERVALS_NOT_SORTED");
        CollectionAssert.Contains(codes, "OPENING_INTERVAL_OVERLAP");
    }

    [TestMethod]
    public void UndefinedDayAndModeAreRejected()
    {
        var days = ClosedWeek();
        days[0] = new(
            (DayOfWeek)99,
            (OpeningDayMode)99,
            []);

        var codes = OpeningHoursRules.ValidateWeek(days)
            .Select(issue => issue.Code)
            .ToArray();

        CollectionAssert.Contains(codes, "OPENING_DAY_INVALID");
        CollectionAssert.Contains(codes, "OPENING_MODE_INVALID");
        CollectionAssert.Contains(codes, "OPENING_WEEK_REQUIRES_SEVEN_DAYS");
    }

    private static OpeningDayDefinition[] ClosedWeek() =>
        Enum.GetValues<DayOfWeek>()
            .Select(day => new OpeningDayDefinition(day, OpeningDayMode.Closed, []))
            .ToArray();
}
