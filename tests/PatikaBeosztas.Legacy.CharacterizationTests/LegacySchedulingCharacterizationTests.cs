using Microsoft.VisualStudio.TestTools.UnitTesting;
using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;
using PharmacyScheduler.Core.Services;

namespace PatikaBeosztas.Legacy.CharacterizationTests;

[TestClass]
public sealed class LegacySchedulingCharacterizationTests
{
    private static readonly DateOnly Monday = new(2025, 1, 6);

    [TestMethod]
    public void AutoFillReturnsCreatedSlotCountAndMergesAdjacentEntries()
    {
        var (data, schedule, _, _) = CreateCoverageScenario(
            locationIsActive: true,
            employeeRole: EmployeeRole.Pharmacist);

        var createdCount = new AutoSchedulerService().FillCoverageGaps(data, schedule);

        Assert.AreEqual(4, createdCount);
        Assert.HasCount(1, schedule.Entries);
        Assert.AreEqual(new TimeOnly(8, 0), schedule.Entries[0].Start);
        Assert.AreEqual(new TimeOnly(10, 0), schedule.Entries[0].End);
        Assert.AreEqual("Automatikus kitöltés", schedule.Entries[0].Note);
    }

    [TestMethod]
    public void InactiveLocationCoverageRuleIsIgnored()
    {
        var (data, schedule, _, _) = CreateCoverageScenario(
            locationIsActive: false,
            employeeRole: EmployeeRole.Pharmacist);

        schedule.Entries.Clear();
        var report = new ScheduleValidationService().Validate(data, schedule);

        Assert.IsFalse(report.Issues.Any(issue => issue.Code == "COVERAGE_SHORTAGE"));
    }

    [TestMethod]
    public void AutoScheduleRoleOverrideIsNotUsedByCoverageValidation()
    {
        var (data, schedule, employee, _) = CreateCoverageScenario(
            locationIsActive: true,
            employeeRole: EmployeeRole.PharmacyManager);
        employee.AutoScheduleRoleOverride = EmployeeRole.Pharmacist;

        var createdCount = new AutoSchedulerService().FillCoverageGaps(data, schedule);
        var report = new ScheduleValidationService().Validate(data, schedule);

        Assert.AreEqual(4, createdCount);
        Assert.IsTrue(report.Issues.Any(issue => issue.Code == "COVERAGE_SHORTAGE"));
    }

    private static (AppData Data, SchedulePlan Schedule, Employee Employee, Location Location)
        CreateCoverageScenario(bool locationIsActive, EmployeeRole employeeRole)
    {
        var location = new Location
        {
            Name = "Teszt telephely",
            IsActive = locationIsActive
        };
        var employee = new Employee
        {
            FullName = "Teszt Elek",
            DisplayName = "Teszt Elek",
            Role = employeeRole,
            IsActive = true,
            IncludeInAutoSchedule = true,
            MonthlyHoursLimit = 168,
            MaxDailyHours = 12,
            AllowedLocationIds = [location.Id],
            AllowedTimeTypes = [TimeType.Work]
        };
        var schedule = new SchedulePlan
        {
            Name = "Karakterizáció",
            PeriodStart = Monday,
            PeriodEnd = Monday,
            Entries = []
        };
        var rule = new CoverageRule
        {
            LocationId = location.Id,
            DayOfWeek = DayOfWeek.Monday,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(10, 0),
            Role = EmployeeRole.Pharmacist,
            RequiredCount = 1,
            Severity = Severity.Hard
        };
        var data = new AppData
        {
            Locations = [location],
            Employees = [employee],
            CoverageRules = [rule],
            Leaves = [],
            Schedules = [schedule],
            Settings = new AppSettings()
        };

        return (data, schedule, employee, location);
    }
}

