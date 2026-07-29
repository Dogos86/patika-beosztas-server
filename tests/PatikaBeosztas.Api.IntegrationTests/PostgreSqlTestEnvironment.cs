using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class PostgreSqlTestEnvironment
{
    private const string ExternalConnectionVariable =
        "PATIKA_TEST_POSTGRES_CONNECTION";
    private static PostgreSqlContainer? container;
    private static string? externalConnectionString;

    public static string GetConnectionString() =>
        externalConnectionString ??
        (container ?? throw new InvalidOperationException(
            "A PostgreSQL Testcontainer nem indult el.")).GetConnectionString();

    [AssemblyInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        externalConnectionString = Environment.GetEnvironmentVariable(
            ExternalConnectionVariable);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            return;
        }

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
