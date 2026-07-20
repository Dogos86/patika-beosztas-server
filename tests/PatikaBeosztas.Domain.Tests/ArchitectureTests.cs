using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class ArchitectureTests
{
    [TestMethod]
    public void DomainDoesNotReferenceOuterLayers()
    {
        var forbiddenReferences = typeof(AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Where(reference =>
                reference.Name is not null &&
                (reference.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                 reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                 reference.Name.StartsWith("PatikaBeosztas.Application", StringComparison.Ordinal) ||
                 reference.Name.StartsWith("PatikaBeosztas.Infrastructure", StringComparison.Ordinal) ||
                 reference.Name.StartsWith("PatikaBeosztas.Api", StringComparison.Ordinal)))
            .Select(reference => reference.Name)
            .ToArray();

        Assert.HasCount(0, forbiddenReferences);
    }
}
