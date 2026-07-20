using System.Reflection;

namespace PatikaBeosztas.Infrastructure;

public static class AssemblyMarker
{
    public static Assembly ApplicationAssembly => typeof(Application.AssemblyMarker).Assembly;

    public static Assembly DomainAssembly => typeof(Domain.AssemblyMarker).Assembly;
}

