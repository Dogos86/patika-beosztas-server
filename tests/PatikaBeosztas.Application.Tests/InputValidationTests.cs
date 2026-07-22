using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Application.Validation;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Application.Tests;

[TestClass]
public sealed class InputValidationTests
{
    [TestMethod]
    public void DuplicateLocationsAndTimeTypesAreRejected()
    {
        var locationId = Guid.NewGuid();

        var errors = InputValidation.ValidateEmployee(
            "Teszt Elek",
            "Teszt",
            true,
            true,
            true,
            10_080,
            720,
            null,
            new DateOnly(2026, 7, 20),
            null,
            [
                new EmployeeLocationRequest(locationId),
                new EmployeeLocationRequest(locationId)
            ],
            [],
            [TimeType.Work, TimeType.Work]);

        Assert.IsTrue(errors.Any(error => error.Code == "DUPLICATE_LOCATION"));
        Assert.IsTrue(errors.Any(error => error.Code == "DUPLICATE_TIME_TYPE"));
    }

    [TestMethod]
    public void EmployeeLimitsBirthDateAndAutofillFlagsAreValidated()
    {
        var errors = InputValidation.ValidateEmployee(
            "Teszt Elek",
            "Teszt",
            false,
            false,
            true,
            0,
            1_441,
            new DateOnly(2026, 7, 21),
            new DateOnly(2026, 7, 20),
            "  bér-azonosító  ",
            [],
            [],
            []);

        Assert.IsTrue(errors.Any(error => error.Code == "MONTHLY_MINUTES_OUT_OF_RANGE"));
        Assert.IsTrue(errors.Any(error => error.Code == "MAX_DAILY_MINUTES_OUT_OF_RANGE"));
        Assert.IsTrue(errors.Any(error => error.Code == "BIRTH_DATE_IN_FUTURE"));
        Assert.IsTrue(errors.Any(error =>
            error.Code == "AUTOFILL_REQUIRES_ACTIVE_SCHEDULABLE_EMPLOYEE"));
    }

    [TestMethod]
    public void PharmacistCoverageMismatchProducesDocumentedWarning()
    {
        var warnings = InputValidation.EmployeeWarnings(
            ProfessionalRole.Assistant,
            countsAsPharmacist: true);

        Assert.HasCount(1, warnings);
    }

    [TestMethod]
    public void WorkPreferenceErrorsAreMappedToPublicFields()
    {
        var errors = InputValidation.ValidateWorkPreference(
            new DateOnly(2026, 8, 2),
            new DateOnly(2026, 8, 1),
            isFullDay: false,
            startTime: null,
            endTime: null,
            note: null);

        Assert.IsTrue(errors.Any(error =>
            error.Code == "WORK_PREFERENCE_DATE_ORDER" && error.Field == "dateTo"));
        Assert.IsTrue(errors.Any(error =>
            error.Code == "PARTIAL_WORK_PREFERENCE_REQUIRES_TIME" &&
            error.Field == "startTime"));
    }

    [TestMethod]
    public void LeaveErrorsAndTransitionsAreMappedToPublicFields()
    {
        var periodErrors = InputValidation.ValidateLeaveRequest(
            LeaveType.AnnualLeave,
            new DateOnly(2026, 8, 10),
            dateTo: null,
            isFullDay: true,
            startTime: null,
            endTime: null,
            employeeNote: null);
        var transitionErrors = InputValidation.ValidateLeaveTransition(
            LeaveRequestStatus.Pending,
            LeaveRequestStatus.Rejected,
            new DateOnly(2026, 8, 10),
            reason: null);

        Assert.IsTrue(periodErrors.Any(error =>
            error.Code == "LEAVE_END_DATE_REQUIRED" && error.Field == "dateTo"));
        Assert.IsTrue(transitionErrors.Any(error =>
            error.Code == "LEAVE_DECISION_REASON_REQUIRED" && error.Field == "reason"));
    }

    [TestMethod]
    public void Phase2BOpeningCoverageAndShiftTemplateErrorsUsePublicFields()
    {
        var openingErrors = InputValidation.ValidateOpeningWeek(
            [new OpeningDayRequest(
                DayOfWeek.Monday,
                OpeningDayMode.CustomIntervals,
                [])]);
        var coverageErrors = InputValidation.ValidateCoverageRequirement(
            new TimeOnly(18, 0),
            new TimeOnly(8, 0),
            0);
        var templateErrors = InputValidation.ValidateShiftTemplate(
            " ",
            [],
            new TimeOnly(18, 0),
            new TimeOnly(8, 0));

        Assert.IsTrue(openingErrors.All(error => error.Field == "days"));
        Assert.IsTrue(coverageErrors.Any(error => error.Field == "requiredCount"));
        Assert.IsTrue(templateErrors.Any(error => error.Field == "name"));
        Assert.IsTrue(templateErrors.Any(error => error.Field == "weekdays"));
    }

    [TestMethod]
    public void Phase2BWorkProfileQuotaAndCapabilitiesAreMapped()
    {
        var request = new UpdateEmployeeWorkProfileRequest(
            ContractedMonthlyMinutes: 0,
            ContractedWeeklyMinutes: null,
            StandardShiftMinutes: 480,
            MinimumShiftMinutes: 600,
            MaximumRegularShiftMinutes: 400,
            MaximumDailyMinutes: 360,
            AllowsLongShift: false,
            MaximumLongShiftMinutes: 720,
            AllowsFullOpeningHoursShift: false,
            AllowsOvertime: false,
            MaximumOvertimeMinutesPerMonth: null,
            AllowsOnCallDuty: false,
            MaximumOnCallAssignmentsPerMonth: null,
            AllowsStandby: false,
            MaximumStandbyAssignmentsPerMonth: null,
            AllowsSaturday: false,
            MaximumSaturdaysPerMonth: null,
            AllowsSunday: false,
            MaximumSundaysPerMonth: null,
            IncludeInAutoFill: true,
            ExpectedVersion: null);

        var profileErrors = InputValidation.ValidateWorkProfile(
            request,
            employeeIsActive: false,
            employeeIsSchedulable: false);
        var quotaErrors = InputValidation.ValidateShiftQuota(3, 2, 1);
        var capabilityErrors = InputValidation.ValidateCapabilities(
            [StaffingCapability.Pharmacist, StaffingCapability.Pharmacist]);

        Assert.IsTrue(profileErrors.Any(error => error.Field == "contractedMonthlyMinutes"));
        Assert.IsTrue(profileErrors.Any(error => error.Field == "includeInAutoFill"));
        Assert.IsTrue(quotaErrors.Any(error => error.Code == "SHIFT_QUOTA_ORDER"));
        Assert.HasCount(1, capabilityErrors);
    }

    [TestMethod]
    public void Phase2BRequiredCollectionsAreValidatedWithoutThrowing()
    {
        var openingErrors = InputValidation.ValidateOpeningWeek(null);
        var templateErrors = InputValidation.ValidateShiftTemplate(
            null,
            null,
            new TimeOnly(8, 0),
            new TimeOnly(16, 0));
        var capabilityErrors = InputValidation.ValidateCapabilities(null);

        Assert.HasCount(1, openingErrors);
        Assert.IsTrue(templateErrors.Any(error => error.Field == "name"));
        Assert.IsTrue(templateErrors.Any(error => error.Field == "weekdays"));
        Assert.HasCount(1, capabilityErrors);
    }
}
