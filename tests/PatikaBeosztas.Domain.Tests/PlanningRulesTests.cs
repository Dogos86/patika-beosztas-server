using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class PlanningRulesTests
{
    private static readonly string[] InvalidShiftTemplateCodes =
    [
        "SHIFT_TEMPLATE_NAME_REQUIRED",
        "SHIFT_TEMPLATE_WEEKDAY_REQUIRED",
        "SHIFT_TEMPLATE_TIME_ORDER"
    ];

    [TestMethod]
    public void ShiftTemplateRequiresNameWeekdayAndOrderedTime()
    {
        var valid = LocationShiftTemplateRules.Validate(
            "Reggel",
            [DayOfWeek.Monday, DayOfWeek.Tuesday],
            new TimeOnly(8, 0),
            new TimeOnly(14, 0));
        var invalid = LocationShiftTemplateRules.Validate(
            " ",
            [],
            new TimeOnly(14, 0),
            new TimeOnly(8, 0));

        Assert.IsEmpty(valid);
        CollectionAssert.AreEquivalent(
            InvalidShiftTemplateCodes,
            invalid.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void CapabilityImplicationsAndManagerCompatibilityAreApplied()
    {
        var specialist = StaffingCapabilityRules.Expand(
            [StaffingCapability.SpecialistPharmacist, StaffingCapability.SpecialistAssistant]);
        var manager = StaffingCapabilityRules.ResolveEffective(
            [],
            ProfessionalRole.PharmacyManager,
            countsAsPharmacist: false);

        Assert.Contains(StaffingCapability.Pharmacist, specialist);
        Assert.Contains(StaffingCapability.Assistant, specialist);
        Assert.Contains(StaffingCapability.Pharmacist, manager);
    }

    [TestMethod]
    public void OverlappingCoverageUsesMaximumInsteadOfSum()
    {
        var requirements = new[]
        {
            Requirement(new TimeOnly(8, 0), new TimeOnly(16, 0), 1),
            Requirement(new TimeOnly(12, 0), new TimeOnly(18, 0), 2),
            Requirement(new TimeOnly(12, 0), new TimeOnly(18, 0), 8, isActive: false)
        };

        var effective = CoverageRequirementRules.GetEffectiveRequiredCount(
            requirements,
            DayOfWeek.Monday,
            StaffingCapability.Pharmacist,
            new TimeOnly(13, 0));

        Assert.AreEqual(2, effective);
    }

    [TestMethod]
    public void InactiveLocationIsExcludedFromActivePlanning()
    {
        Assert.IsTrue(PlanningEligibilityRules.IncludeLocationInActivePlanning(true));
        Assert.IsFalse(PlanningEligibilityRules.IncludeLocationInActivePlanning(false));
    }

    private static CoverageRequirement Requirement(
        TimeOnly start,
        TimeOnly end,
        int count,
        bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            DayOfWeek = DayOfWeek.Monday,
            StartTime = start,
            EndTime = end,
            RequiredCapability = StaffingCapability.Pharmacist,
            RequiredCount = count,
            Severity = CoverageSeverity.Blocking,
            IsActive = isActive
        };
}
