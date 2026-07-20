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
            10_080,
            720,
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
    public void PharmacistCoverageMismatchProducesDocumentedWarning()
    {
        var warnings = InputValidation.EmployeeWarnings(
            ProfessionalRole.Assistant,
            countsAsPharmacist: true);

        Assert.HasCount(1, warnings);
    }
}
