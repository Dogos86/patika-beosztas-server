using Microsoft.VisualStudio.TestTools.UnitTesting;
using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;
using PharmacyScheduler.Core.Services;

namespace PharmacyScheduler.Tests;

[TestClass]
public class AutoSchedulerServiceTests
{
    private readonly AutoSchedulerService _service = new();

    [TestMethod]
    public void FillCoverageGaps_ShouldCreateEntriesWhenEligibleEmployeeExists()
    {
        var data = SampleDataFactory.Create();
        var schedule = new SchedulePlan
        {
            Name = "Auto test",
            PeriodStart = new DateOnly(2025, 1, 6),
            PeriodEnd = new DateOnly(2025, 1, 6)
        };

        data.Schedules.Clear();
        data.Schedules.Add(schedule);
        data.CoverageRules.Clear();

        var location = data.Locations.First();
        var pharmacist = data.Employees.First(x => x.Role == EmployeeRole.Pharmacist);

        data.CoverageRules.Add(new CoverageRule
        {
            LocationId = location.Id,
            DayOfWeek = DayOfWeek.Monday,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(10, 0),
            Role = EmployeeRole.Pharmacist,
            RequiredCount = 1,
            Severity = Severity.Hard
        });

        pharmacist.AllowedLocationIds = new List<Guid> { location.Id };
        pharmacist.AllowedTimeTypes = new List<TimeType> { TimeType.Work };

        var created = _service.FillCoverageGaps(data, schedule);

        Assert.IsTrue(created > 0);
        Assert.IsTrue(schedule.Entries.Any());
        Assert.IsTrue(schedule.Entries.All(x => x.EmployeeId == pharmacist.Id));
    }
}
