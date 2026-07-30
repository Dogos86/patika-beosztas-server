using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using PatikaBeosztas.Infrastructure.Persistence;
using PatikaBeosztas.Infrastructure.Security;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
public sealed class PilotConfigurationTests
{
    private const string ValidHashKey =
        "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    [TestMethod]
    public void RailwayPostgreSqlUrlIsNormalizedForNpgsql()
    {
        var normalized = PostgreSqlConnectionString.Normalize(
            "postgresql://pilot%40user:p%40ssword@postgres.railway.internal:5432/" +
            "patika_pilot?sslmode=require");
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.AreEqual("postgres.railway.internal", builder.Host);
        Assert.AreEqual(5432, builder.Port);
        Assert.AreEqual("patika_pilot", builder.Database);
        Assert.AreEqual("pilot@user", builder.Username);
        Assert.AreEqual("p@ssword", builder.Password);
        Assert.AreEqual(SslMode.Require, builder.SslMode);
        Assert.IsFalse(builder.IncludeErrorDetail);
    }

    [TestMethod]
    public void ProductionConfigurationRejectsSeedOpenApiAndWeakHashKey()
    {
        var keysPath = NewKeysPath();
        try
        {
            var environment = new TestHostEnvironment("Production");
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProductionConfiguration.Validate(
                    Configuration(keysPath, seedEnabled: true),
                    environment));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProductionConfiguration.Validate(
                    Configuration(keysPath, openApiEnabled: true),
                    environment));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProductionConfiguration.Validate(
                    Configuration(keysPath, hashKey: "dG9vLXNob3J0"),
                    environment));

            ProductionConfiguration.Validate(
                Configuration(keysPath),
                environment);
        }
        finally
        {
            DeleteDirectory(keysPath);
        }
    }

    [TestMethod]
    public void DataProtectionKeysRemainUsableAcrossServiceProviderRestart()
    {
        var keysPath = NewKeysPath();
        try
        {
            var configuration = Configuration(keysPath);
            var environment = new TestHostEnvironment("Production");
            string protectedValue;
            using (var firstProvider = Services(configuration, environment))
            {
                var protector = firstProvider
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("Pilot.Restart.Test.v1");
                protectedValue = protector.Protect("tartós-próba");
            }

            using (var secondProvider = Services(configuration, environment))
            {
                var protector = secondProvider
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("Pilot.Restart.Test.v1");
                Assert.AreEqual(
                    "tartós-próba",
                    protector.Unprotect(protectedValue));
            }

            Assert.IsTrue(
                Directory.EnumerateFiles(keysPath, "*.xml").Any(),
                "A Data Protection kulcs nem került a tartós könyvtárba.");
        }
        finally
        {
            DeleteDirectory(keysPath);
        }
    }

    private static ServiceProvider Services(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfiguredDataProtection(configuration, environment);
        return services.BuildServiceProvider();
    }

    private static IConfiguration Configuration(
        string keysPath,
        bool seedEnabled = false,
        bool openApiEnabled = false,
        string hashKey = ValidHashKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=pilot;Username=pilot;Password=pilot",
                ["Seed:Enabled"] = seedEnabled.ToString(),
                ["OpenApi:Enabled"] = openApiEnabled.ToString(),
                ["DataProtection:KeysPath"] = keysPath,
                ["DataProtection:ApplicationName"] = "PatikaBeosztas",
                ["SensitiveData:TaxIdentifierHashKey"] = hashKey
            })
            .Build();

    private static string NewKeysPath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"patika-dp-test-{Guid.NewGuid():N}");

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string ApplicationName { get; set; } =
            "PatikaBeosztas.Api.IntegrationTests";

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = environmentName;
    }
}
