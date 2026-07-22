using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class DevelopmentDataSeederTests
{
    private static readonly Guid DemoOrganizationId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CentralLocationId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid BranchLocationId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid AdminEmployeeId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid RegularEmployeeId =
        Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid OfflineEmployeeId =
        Guid.Parse("30000000-0000-0000-0000-000000000003");

    [TestMethod]
    public async Task PartialDevelopmentSeedIsRepairedByFixedIdsWithoutOverwritingEmployees()
    {
        await using var application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString());
        _ = application.Server;
        var customEmployeeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.MigrateAsync();
            dbContext.Organizations.Add(new Organization
            {
                Id = DemoOrganizationId,
                Name = "Régi fejlesztői szervezet",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.Locations.Add(new Location
            {
                Id = CentralLocationId,
                OrganizationId = DemoOrganizationId,
                Name = "Meglévő központ",
                Type = LocationType.Central,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.Employees.AddRange(
                CreateEmployee(AdminEmployeeId, "Meglévő demo admin", now),
                CreateEmployee(customEmployeeId, "Felhasználó saját dolgozója", now));
            await dbContext.SaveChangesAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:DemoPassword"] = "Development-Seed123!"
            })
            .Build();
        var development = new TestWebHostEnvironment("Development");

        await application.Services.InitializeDevelopmentDatabaseAsync(
            development,
            configuration);
        await application.Services.InitializeDevelopmentDatabaseAsync(
            development,
            configuration);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            var employeeIds = await dbContext.Employees
                .Where(employee => employee.OrganizationId == DemoOrganizationId)
                .Select(employee => employee.Id)
                .ToListAsync();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    AdminEmployeeId,
                    RegularEmployeeId,
                    OfflineEmployeeId,
                    customEmployeeId
                },
                employeeIds);
            Assert.AreEqual(
                "Meglévő demo admin",
                await dbContext.Employees
                    .Where(employee => employee.Id == AdminEmployeeId)
                    .Select(employee => employee.DisplayName)
                    .SingleAsync());
            Assert.AreEqual(
                "Felhasználó saját dolgozója",
                await dbContext.Employees
                    .Where(employee => employee.Id == customEmployeeId)
                    .Select(employee => employee.DisplayName)
                    .SingleAsync());
            CollectionAssert.AreEquivalent(
                new[] { CentralLocationId, BranchLocationId },
                await dbContext.Locations
                    .Where(location => location.OrganizationId == DemoOrganizationId)
                    .Select(location => location.Id)
                    .ToListAsync());
            Assert.AreEqual(
                2,
                await dbContext.Users.CountAsync(
                    user => user.OrganizationId == DemoOrganizationId));
        }

        await application.Services.InitializeDevelopmentDatabaseAsync(
            new TestWebHostEnvironment("Production"),
            configuration);

        await using var verificationScope = application.Services.CreateAsyncScope();
        var verificationContext =
            verificationScope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        Assert.AreEqual(
            4,
            await verificationContext.Employees.CountAsync(
                employee => employee.OrganizationId == DemoOrganizationId));
    }

    private static Employee CreateEmployee(Guid id, string name, DateTimeOffset now) =>
        new()
        {
            Id = id,
            OrganizationId = DemoOrganizationId,
            FullName = name,
            DisplayName = name,
            ProfessionalRole = ProfessionalRole.Assistant,
            IsActive = true,
            IsSchedulable = true,
            IncludeInAutoFill = false,
            CountsAsPharmacist = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PatikaBeosztas.Api.IntegrationTests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = environmentName;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    }
}
