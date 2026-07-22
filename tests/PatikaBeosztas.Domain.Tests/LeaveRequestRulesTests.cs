using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class LeaveRequestRulesTests
{
    [TestMethod]
    public void SickLeaveMayBeOpenEndedAndStartsReported()
    {
        var issues = LeaveRequestRules.ValidatePeriod(
            LeaveType.SickLeave,
            new DateOnly(2026, 8, 10),
            dateTo: null,
            isFullDay: true,
            startTime: null,
            endTime: null,
            employeeNote: null);

        Assert.IsEmpty(issues);
        Assert.AreEqual(
            LeaveRequestStatus.Reported,
            LeaveRequestRules.InitialStatus(LeaveType.SickLeave));
    }

    [TestMethod]
    public void NormalLeaveNeedsEndDateAndPartialLeaveNeedsSingleOrderedDay()
    {
        var missingEnd = LeaveRequestRules.ValidatePeriod(
            LeaveType.AnnualLeave,
            new DateOnly(2026, 8, 10),
            dateTo: null,
            isFullDay: true,
            startTime: null,
            endTime: null,
            employeeNote: null);
        var invalidPartial = LeaveRequestRules.ValidatePeriod(
            LeaveType.UnpaidLeave,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 11),
            isFullDay: false,
            new TimeOnly(12, 0),
            new TimeOnly(8, 0),
            employeeNote: null);

        Assert.IsTrue(missingEnd.Any(issue => issue.Code == "LEAVE_END_DATE_REQUIRED"));
        Assert.IsTrue(invalidPartial.Any(issue => issue.Code == "LEAVE_TIME_ORDER"));
        Assert.IsTrue(invalidPartial.Any(issue => issue.Code == "PARTIAL_LEAVE_SINGLE_DAY"));
    }

    [TestMethod]
    public void SickLeaveRejectsFreeTextHealthData()
    {
        var issues = LeaveRequestRules.ValidatePeriod(
            LeaveType.SickLeave,
            new DateOnly(2026, 8, 10),
            dateTo: null,
            isFullDay: true,
            startTime: null,
            endTime: null,
            employeeNote: "Nem tárolható egészségügyi részlet");

        Assert.IsTrue(issues.Any(issue => issue.Code == "SICK_LEAVE_NOTE_NOT_ALLOWED"));
    }

    [TestMethod]
    public void NormalAndSickStateMachinesAllowOnlyDocumentedTransitions()
    {
        Assert.IsTrue(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Draft,
            LeaveRequestStatus.Pending));
        Assert.IsTrue(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Pending,
            LeaveRequestStatus.Approved));
        Assert.IsTrue(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Pending,
            LeaveRequestStatus.Rejected));
        Assert.IsTrue(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Pending,
            LeaveRequestStatus.Withdrawn));
        Assert.IsTrue(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Approved,
            LeaveRequestStatus.Cancelled));
        Assert.IsTrue(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Reported,
            LeaveRequestStatus.Recorded));
        Assert.IsTrue(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Recorded,
            LeaveRequestStatus.Closed));

        Assert.IsFalse(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Draft,
            LeaveRequestStatus.Approved));
        Assert.IsFalse(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Reported,
            LeaveRequestStatus.Approved));
        Assert.IsFalse(LeaveRequestRules.CanTransition(
            LeaveRequestStatus.Closed,
            LeaveRequestStatus.Recorded));
    }

    [TestMethod]
    public void RejectAndCancelNeedReasonAndClosingNeedsEndDate()
    {
        var rejected = LeaveRequestRules.ValidateTransition(
            LeaveRequestStatus.Pending,
            LeaveRequestStatus.Rejected,
            new DateOnly(2026, 8, 11),
            reason: null);
        var closed = LeaveRequestRules.ValidateTransition(
            LeaveRequestStatus.Recorded,
            LeaveRequestStatus.Closed,
            dateTo: null,
            reason: null);

        Assert.IsTrue(rejected.Any(issue =>
            issue.Code == "LEAVE_DECISION_REASON_REQUIRED"));
        Assert.IsTrue(closed.Any(issue =>
            issue.Code == "SICK_LEAVE_END_DATE_REQUIRED_TO_CLOSE"));
    }
}
