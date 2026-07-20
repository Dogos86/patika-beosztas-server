using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PatikaBeosztas.Application.Tests;

[TestClass]
public sealed class ArchitectureTests
{
    [TestMethod]
    public void ApplicationDependsOnDomainButNotOnOuterLayers()
    {
        Assert.AreEqual("PatikaBeosztas.Domain", AssemblyMarker.DomainAssembly.GetName().Name);

        var referencedNames = typeof(AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        CollectionAssert.DoesNotContain(referencedNames, "PatikaBeosztas.Infrastructure");
        CollectionAssert.DoesNotContain(referencedNames, "PatikaBeosztas.Api");
    }
}

