using System.Reflection;

namespace PatikaBeosztas.Application;

public static class AssemblyMarker
{
    public static Assembly DomainAssembly => typeof(Domain.AssemblyMarker).Assembly;
}

