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
}
