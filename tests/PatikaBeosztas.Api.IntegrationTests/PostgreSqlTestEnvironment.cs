using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class PostgreSqlTestEnvironment
{
    private static PostgreSqlContainer? container;
    private static string? startupFailure;

    public static string GetConnectionString()
    {
        if (container is null)
        {
            Assert.Inconclusive(
                $"A PostgreSQL Testcontainers tesztek kihagyva: {startupFailure ?? "Docker nem érhető el."}");
            return string.Empty;
        }

        return container.GetConnectionString();
    }

    [AssemblyInitialize]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A Docker hiánya dokumentált tesztkihagyás, nem assembly-szintű összeomlás.")]
    public static async Task InitializeAsync(TestContext _)
    {
        try
        {
            container = new PostgreSqlBuilder()
                .WithImage("postgres:17-alpine")
                .WithDatabase("patika_tests")
                .WithUsername("patika_tests")
                .WithPassword("integration-test-only")
                .Build();
            await container.StartAsync();
        }
        catch (Exception exception)
        {
            startupFailure = exception.Message;
            container = null;
        }
    }

    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }
}
