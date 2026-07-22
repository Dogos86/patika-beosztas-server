using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class WorkPreferenceRulesTests
{
    [TestMethod]
    public void FullDayAndValidPartialDayAreAccepted()
    {
        var fullDay = WorkPreferenceRules.Validate(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            isFullDay: true,
            startTime: null,
            endTime: null,
            note: null);
        var partial = WorkPreferenceRules.Validate(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            isFullDay: false,
            new TimeOnly(8, 15),
            new TimeOnly(12, 45),
            "Délelőtti kérés");

        Assert.IsEmpty(fullDay);
        Assert.IsEmpty(partial);
    }

    [TestMethod]
    public void InvalidDateAndTimeShapesAreRejected()
    {
        var issues = WorkPreferenceRules.Validate(
            new DateOnly(2026, 8, 2),
            new DateOnly(2026, 8, 1),
            isFullDay: false,
            new TimeOnly(12, 0),
            new TimeOnly(8, 0),
            new string('x', WorkPreferenceRules.MaximumNoteLength + 1));

        Assert.IsTrue(issues.Any(issue => issue.Code == "WORK_PREFERENCE_DATE_ORDER"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "WORK_PREFERENCE_TIME_ORDER"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "WORK_PREFERENCE_NOTE_TOO_LONG"));
    }
}
