using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;

namespace PatikaBeosztas.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    private static readonly Action<ILogger, Exception?> LogDevelopmentSeedWarning =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1001, "DevelopmentSeedEnabled"),
            "A Development demo seed aktív. A fiókok kizárólag helyi fejlesztésre használhatók.");

    private static readonly Guid OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CentralLocationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid BranchLocationId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid AdminEmployeeId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid RegularEmployeeId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid OfflineEmployeeId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid AdminUserId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid RegularUserId = Guid.Parse("40000000-0000-0000-0000-000000000002");

    public static async Task InitializeDevelopmentDatabaseAsync(
        this IServiceProvider services,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (!configuration.GetValue("Seed:Enabled", true))
        {
            return;
        }

        var demoPassword = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            throw new InvalidOperationException(
                "Development seed esetén a Seed__DemoPassword környezeti változó kötelező.");
        }

        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();
        await SeedOrganizationAndEmployeesAsync(dbContext, now, cancellationToken);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(
            userManager,
            dbContext,
            new ApplicationUser
            {
                Id = AdminUserId,
                OrganizationId = OrganizationId,
                UserName = "admin@example.invalid",
                Email = "admin@example.invalid",
                EmailConfirmed = true,
                DisplayName = "Demo Admin",
                EmployeeId = AdminEmployeeId,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            demoPassword,
            Enum.GetValues<ApplicationPermission>(),
            cancellationToken);
        await EnsureUserAsync(
            userManager,
            dbContext,
            new ApplicationUser
            {
                Id = RegularUserId,
                OrganizationId = OrganizationId,
                UserName = "dolgozo@example.invalid",
                Email = "dolgozo@example.invalid",
                EmailConfirmed = true,
                DisplayName = "Demo Dolgozó",
                EmployeeId = RegularEmployeeId,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            demoPassword,
            [
                ApplicationPermission.ViewOwnSchedule,
                ApplicationPermission.ManageOwnLeaveRequests
            ],
            cancellationToken);

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DevelopmentDataSeeder");
        LogDevelopmentSeedWarning(logger, null);
    }

    private static async Task SeedOrganizationAndEmployeesAsync(
        PatikaDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Organizations.AnyAsync(
                organization => organization.Id == OrganizationId,
                cancellationToken))
        {
            dbContext.Organizations.Add(new Organization
            {
                Id = OrganizationId,
                Name = "Demo Gyógyszertár",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        if (!await dbContext.Locations.AnyAsync(
                location => location.Id == CentralLocationId,
                cancellationToken))
        {
            dbContext.Locations.Add(new Location
            {
                Id = CentralLocationId,
                OrganizationId = OrganizationId,
                Name = "Központi gyógyszertár",
                Type = LocationType.Central,
                Address = "Anonimizált fejlesztői cím",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        if (!await dbContext.Locations.AnyAsync(
                location => location.Id == BranchLocationId,
                cancellationToken))
        {
            dbContext.Locations.Add(new Location
            {
                Id = BranchLocationId,
                OrganizationId = OrganizationId,
                Name = "Régi fióktelep",
                Type = LocationType.Branch,
                Address = null,
                IsActive = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        if (!await dbContext.Employees.AnyAsync(
                employee => employee.Id == AdminEmployeeId,
                cancellationToken))
        {
            dbContext.Employees.Add(CreateEmployee(
                AdminEmployeeId,
                "Demo Admin",
                ProfessionalRole.PharmacyManager,
                isSchedulable: true,
                countsAsPharmacist: true,
                now));
        }

        if (!await dbContext.Employees.AnyAsync(
                employee => employee.Id == RegularEmployeeId,
                cancellationToken))
        {
            dbContext.Employees.Add(CreateEmployee(
                RegularEmployeeId,
                "Demo Dolgozó",
                ProfessionalRole.Assistant,
                isSchedulable: true,
                countsAsPharmacist: false,
                now));
        }

        if (!await dbContext.Employees.AnyAsync(
                employee => employee.Id == OfflineEmployeeId,
                cancellationToken))
        {
            dbContext.Employees.Add(CreateEmployee(
                OfflineEmployeeId,
                "Fiók nélküli Dolgozó",
                ProfessionalRole.Pharmacist,
                isSchedulable: true,
                countsAsPharmacist: true,
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var fixedDemoEmployeeIds = new[]
        {
            AdminEmployeeId,
            RegularEmployeeId,
            OfflineEmployeeId
        };
        var pharmacistEmployeeIds = await dbContext.Employees
            .Where(employee =>
                employee.OrganizationId == OrganizationId &&
                fixedDemoEmployeeIds.Contains(employee.Id) &&
                (employee.CountsAsPharmacist ||
                 employee.ProfessionalRole == ProfessionalRole.PharmacyManager))
            .Select(employee => employee.Id)
            .ToArrayAsync(cancellationToken);
        var existingPharmacistCapabilityIds = await dbContext.EmployeeCapabilities
            .Where(capability =>
                capability.OrganizationId == OrganizationId &&
                capability.Capability == StaffingCapability.Pharmacist &&
                pharmacistEmployeeIds.Contains(capability.EmployeeId))
            .Select(capability => capability.EmployeeId)
            .ToArrayAsync(cancellationToken);
        dbContext.EmployeeCapabilities.AddRange(
            pharmacistEmployeeIds.Except(existingPharmacistCapabilityIds).Select(employeeId =>
                new EmployeeCapability
                {
                    OrganizationId = OrganizationId,
                    EmployeeId = employeeId,
                    Capability = StaffingCapability.Pharmacist,
                    AssignedAtUtc = now
                }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Employee CreateEmployee(
        Guid id,
        string name,
        ProfessionalRole role,
        bool isSchedulable,
        bool countsAsPharmacist,
        DateTimeOffset now)
    {
        var employee = new Employee
        {
            Id = id,
            OrganizationId = OrganizationId,
            FullName = name,
            DisplayName = name,
            ProfessionalRole = role,
            IsActive = true,
            IsSchedulable = isSchedulable,
            IncludeInAutoFill = isSchedulable,
            CountsAsPharmacist = countsAsPharmacist,
            MonthlyMinutesLimit = 10_080,
            MaxDailyMinutes = 720,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        employee.Locations.Add(new EmployeeLocation
        {
            OrganizationId = OrganizationId,
            EmployeeId = id,
            LocationId = CentralLocationId,
            Enabled = true
        });
        employee.AllowedTimeTypes.Add(new EmployeeAllowedTimeType
        {
            OrganizationId = OrganizationId,
            EmployeeId = id,
            TimeType = TimeType.Work
        });
        return employee;
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        PatikaDbContext dbContext,
        ApplicationUser user,
        string password,
        IEnumerable<ApplicationPermission> permissions,
        CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByIdAsync(user.Id.ToString());
        if (existing is null)
        {
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var description = string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"A demo felhasználó nem hozható létre: {description}");
            }
        }

        var permissionValues = permissions.Distinct().ToArray();
        var existingPermissions = await dbContext.UserPermissions
            .Where(permission => permission.UserId == user.Id)
            .Select(permission => permission.Permission)
            .ToListAsync(cancellationToken);
        var missingPermissions = permissionValues.Except(existingPermissions);
        dbContext.UserPermissions.AddRange(missingPermissions.Select(permission =>
            new UserPermission
            {
                OrganizationId = OrganizationId,
                UserId = user.Id,
                Permission = permission
            }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
