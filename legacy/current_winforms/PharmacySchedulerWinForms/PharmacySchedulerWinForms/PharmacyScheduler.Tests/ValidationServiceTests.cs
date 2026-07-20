using Microsoft.VisualStudio.TestTools.UnitTesting;
using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;
using PharmacyScheduler.Core.Services;

namespace PharmacyScheduler.Tests;

[TestClass]
public class ValidationServiceTests
{
    private readonly ScheduleValidationService _service = new();

    [TestMethod]
    public void Validate_ShouldFlagOverlapAsHardIssue()
    {
        var data = SampleDataFactory.Create();
        var schedule = data.Schedules.First();

        var employee = data.Employees.First(x => x.Role == EmployeeRole.Pharmacist);
        var location = data.Locations.First();

        schedule.Entries.Add(new ShiftEntry
        {
            ScheduleId = schedule.Id,
            EmployeeId = employee.Id,
            LocationId = location.Id,
            Date = schedule.PeriodStart,
            Start = new TimeOnly(9, 0),
            End = new TimeOnly(12, 0),
            TimeType = TimeType.Work
        });

        var report = _service.Validate(data, schedule);

        Assert.IsTrue(report.Issues.Any(x => x.Code == "EMPLOYEE_OVERLAP" && x.Severity == Severity.Hard));
    }

    [TestMethod]
    public void Validate_ShouldFlagCoverageShortage()
    {
        var data = SampleDataFactory.Create();
        var schedule = data.Schedules.First();

        schedule.Entries.Clear();

        var report = _service.Validate(data, schedule);

        Assert.IsTrue(report.Issues.Any(x => x.Code == "COVERAGE_SHORTAGE"));
    }

    [TestMethod]
    public void Validate_ShouldFlagDailyHoursExceeded()
    {
        var data = SampleDataFactory.Create();
        var schedule = data.Schedules.First();
        var employee = data.Employees.First(x => x.Role == EmployeeRole.Pharmacist);
        var location = data.Locations.First();

        schedule.Entries.Add(new ShiftEntry
        {
            ScheduleId = schedule.Id,
            EmployeeId = employee.Id,
            LocationId = location.Id,
            Date = schedule.PeriodStart.AddDays(1),
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(18, 30),
            TimeType = TimeType.Work
        });

        var report = _service.Validate(data, schedule);

        Assert.IsTrue(report.Issues.Any(x => x.Code == "DAILY_LIMIT_EXCEEDED"));
    }
}
