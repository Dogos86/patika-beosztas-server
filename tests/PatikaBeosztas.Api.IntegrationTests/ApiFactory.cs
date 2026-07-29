using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;
using PatikaBeosztas.Infrastructure.Scheduling;

namespace PatikaBeosztas.Api.IntegrationTests;

internal sealed class ApiFactory(
    string connectionString,
    bool disableScheduleGenerationWorker = false)
    : WebApplicationFactory<Program>
{
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    public async Task ResetAndSeedDatabaseAsync()
    {
        _ = Server;
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        await IntegrationTestData.SeedAsync(scope.ServiceProvider);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            if (string.Equals(
                    Environment.GetEnvironmentVariable(
                        "PATIKA_TEST_CONSOLE_LOGGING"),
                    "1",
                    StringComparison.Ordinal))
            {
                logging.AddConsole();
            }
        });
        builder.ConfigureServices(services =>
        {
            if (disableScheduleGenerationWorker)
            {
                var worker = services.Single(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType ==
                    typeof(ScheduleGenerationBackgroundService));
                services.Remove(worker);
            }

            services.AddSingleton<IDataProtectionProvider>(
                new EphemeralDataProtectionProvider());
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Seed:Enabled"] = "false",
                    ["Cors:AllowedOrigins:0"] = "https://localhost:5173",
                    ["SensitiveData:TaxIdentifierHashKey"] =
                        "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="
                });
        });
    }
}

internal static class IntegrationTestData
{
    public const string Password = "Integration-Test123!";

    public static readonly Guid OrganizationId =
        Guid.Parse("81000000-0000-0000-0000-000000000001");
    public static readonly Guid OtherOrganizationId =
        Guid.Parse("81000000-0000-0000-0000-000000000002");
    public static readonly Guid InactiveOrganizationId =
        Guid.Parse("81000000-0000-0000-0000-000000000003");
    public static readonly Guid AdminEmployeeId =
        Guid.Parse("82000000-0000-0000-0000-000000000001");
    public static readonly Guid RegularEmployeeId =
        Guid.Parse("82000000-0000-0000-0000-000000000002");
    public static readonly Guid OtherEmployeeId =
        Guid.Parse("82000000-0000-0000-0000-000000000003");
    public static readonly Guid OfflineEmployeeId =
        Guid.Parse("82000000-0000-0000-0000-000000000004");
    public static readonly Guid AdminUserId =
        Guid.Parse("83000000-0000-0000-0000-000000000001");
    public static readonly Guid RegularUserId =
        Guid.Parse("83000000-0000-0000-0000-000000000002");
    public static readonly Guid InactiveUserId =
        Guid.Parse("83000000-0000-0000-0000-000000000003");
    public static readonly Guid InactiveOrganizationUserId =
        Guid.Parse("83000000-0000-0000-0000-000000000004");
    public static readonly Guid OtherLocationId =
        Guid.Parse("84000000-0000-0000-0000-000000000001");
    public static readonly Guid LocalLocationId =
        Guid.Parse("84000000-0000-0000-0000-000000000002");
    public static readonly Guid InactiveLocalLocationId =
        Guid.Parse("84000000-0000-0000-0000-000000000003");

    public static async Task SeedAsync(IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<PatikaDbContext>();
        var now = DateTimeOffset.UtcNow;
        dbContext.Organizations.AddRange(
            new Organization
            {
                Id = OrganizationId,
                Name = "Első teszt szervezet",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Organization
            {
                Id = OtherOrganizationId,
                Name = "Másik teszt szervezet",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Organization
            {
                Id = InactiveOrganizationId,
                Name = "Inaktív teszt szervezet",
                IsActive = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        dbContext.Employees.AddRange(
            CreateEmployee(
                AdminEmployeeId,
                OrganizationId,
                "Admin Gyógyszerész",
                ProfessionalRole.PharmacyManager,
                true,
                true,
                now),
            CreateEmployee(
                RegularEmployeeId,
                OrganizationId,
                "Normál Dolgozó",
                ProfessionalRole.Assistant,
                true,
                false,
                now),
            CreateEmployee(
                OtherEmployeeId,
                OtherOrganizationId,
                "Másik Dolgozó",
                ProfessionalRole.Pharmacist,
                true,
                true,
                now),
            CreateEmployee(
                OfflineEmployeeId,
                OrganizationId,
                "Fiók Nélküli Dolgozó",
                ProfessionalRole.SpecialistAssistant,
                true,
                false,
                now));
        dbContext.Locations.AddRange(
            new Location
            {
                Id = OtherLocationId,
                OrganizationId = OtherOrganizationId,
                Name = "Másik szervezet telephelye",
                Type = LocationType.Central,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Location
            {
                Id = LocalLocationId,
                OrganizationId = OrganizationId,
                Name = "Helyi teszt telephely",
                Type = LocationType.Central,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Location
            {
                Id = InactiveLocalLocationId,
                OrganizationId = OrganizationId,
                Name = "Inaktív helyi telephely",
                Type = LocationType.Branch,
                IsActive = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        await dbContext.SaveChangesAsync();

        dbContext.EmployeeCapabilities.AddRange(
            new EmployeeCapability
            {
                OrganizationId = OrganizationId,
                EmployeeId = AdminEmployeeId,
                Capability = StaffingCapability.Pharmacist,
                AssignedAtUtc = now
            },
            new EmployeeCapability
            {
                OrganizationId = OtherOrganizationId,
                EmployeeId = OtherEmployeeId,
                Capability = StaffingCapability.Pharmacist,
                AssignedAtUtc = now
            });
        await dbContext.SaveChangesAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateUserAsync(
            userManager,
            dbContext,
            AdminUserId,
            OrganizationId,
            AdminEmployeeId,
            "admin@test.invalid",
            "Teszt Admin",
            true,
            [
                ApplicationPermission.ManageEmployees,
                ApplicationPermission.ManageLocations,
                ApplicationPermission.ManageUsers,
                ApplicationPermission.ManageWorkPreferences,
                ApplicationPermission.ManageCoverageRules,
                ApplicationPermission.ManageAllLeaveRequests,
                ApplicationPermission.ApproveLeaveRequests,
                ApplicationPermission.RecordLeaveForOthers,
                ApplicationPermission.ViewOwnSchedule,
                ApplicationPermission.ManagePayrollOnboarding,
                ApplicationPermission.ViewPayrollSensitiveData,
                ApplicationPermission.ReviewTaxAllowanceSurvey,
                ApplicationPermission.ExportPayrollData,
                ApplicationPermission.ManageSchedules,
                ApplicationPermission.RunAutoFill,
                ApplicationPermission.ApproveSchedules,
                ApplicationPermission.PublishSchedules
            ],
            now);
        await CreateUserAsync(
            userManager,
            dbContext,
            RegularUserId,
            OrganizationId,
            RegularEmployeeId,
            "dolgozo@test.invalid",
            "Teszt Dolgozó",
            true,
            [
                ApplicationPermission.ViewOwnSchedule,
                ApplicationPermission.ManageOwnLeaveRequests
            ],
            now);
        await CreateUserAsync(
            userManager,
            dbContext,
            InactiveUserId,
            OrganizationId,
            null,
            "inaktiv@test.invalid",
            "Inaktív Felhasználó",
            false,
            [],
            now);
        await CreateUserAsync(
            userManager,
            dbContext,
            InactiveOrganizationUserId,
            InactiveOrganizationId,
            null,
            "inaktiv-szervezet@test.invalid",
            "Inaktív Szervezet Felhasználó",
            true,
            [],
            now);
    }

    private static Employee CreateEmployee(
        Guid id,
        Guid organizationId,
        string name,
        ProfessionalRole role,
        bool schedulable,
        bool countsAsPharmacist,
        DateTimeOffset now) =>
        new()
        {
            Id = id,
            OrganizationId = organizationId,
            FullName = name,
            DisplayName = name,
            ProfessionalRole = role,
            IsActive = true,
            IsSchedulable = schedulable,
            IncludeInAutoFill = schedulable,
            CountsAsPharmacist = countsAsPharmacist,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        Guid id,
        Guid organizationId,
        Guid? employeeId,
        string email,
        string displayName,
        bool isActive,
        IReadOnlyCollection<ApplicationPermission> permissions,
        DateTimeOffset now)
    {
        var user = new ApplicationUser
        {
            Id = id,
            OrganizationId = organizationId,
            EmployeeId = employeeId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        dbContext.UserPermissions.AddRange(permissions.Select(permission =>
            new UserPermission
            {
                OrganizationId = organizationId,
                UserId = id,
                Permission = permission
            }));
        await dbContext.SaveChangesAsync();
    }
}
