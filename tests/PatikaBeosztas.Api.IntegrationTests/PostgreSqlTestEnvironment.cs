using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class PostgreSqlTestEnvironment
{
    private static PostgreSqlContainer? container;

    public static string GetConnectionString() =>
        (container ?? throw new InvalidOperationException(
            "A PostgreSQL Testcontainer nem indult el.")).GetConnectionString();

    [AssemblyInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("patika_tests")
            .WithUsername("patika_tests")
            .WithPassword("integration-test-only")
            .Build();
        await container.StartAsync();
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
