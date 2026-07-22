using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class TenantIntegrityTests
{
    [TestMethod]
    public async Task Phase2BMigrationBackfillsPharmacistCapabilitiesWithoutDataLoss()
    {
        var options = new DbContextOptionsBuilder<PatikaDbContext>()
            .UseNpgsql(PostgreSqlTestEnvironment.GetConnectionString())
            .Options;
        await using var dbContext = new PatikaDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(
            "20260722110418_Phase2AWorkPreferencesAndLeaveRequests");

        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var compatibilityId = Guid.NewGuid();
        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Migrációs teszt szervezet",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Employees.AddRange(
            MigrationEmployee(
                managerId,
                organizationId,
                "Migrációs Vezető",
                ProfessionalRole.PharmacyManager,
                countsAsPharmacist: false,
                now),
            MigrationEmployee(
                compatibilityId,
                organizationId,
                "Kompatibilitási Gyógyszerész",
                ProfessionalRole.Assistant,
                countsAsPharmacist: true,
                now));
        await dbContext.SaveChangesAsync();

        await migrator.MigrateAsync();
        dbContext.ChangeTracker.Clear();

        var pharmacistEmployeeIds = await dbContext.EmployeeCapabilities
            .Where(capability => capability.Capability == StaffingCapability.Pharmacist)
            .Select(capability => capability.EmployeeId)
            .Order()
            .ToArrayAsync();
        CollectionAssert.AreEquivalent(
            new[] { managerId, compatibilityId },
            pharmacistEmployeeIds);
        Assert.AreEqual(2, await dbContext.Employees.CountAsync());
    }

    [TestMethod]
    public async Task CompositeForeignKeysRejectEveryConfiguredCrossOrganizationLink()
    {
        await using var application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString());
        await application.ResetAndSeedDatabaseAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();

        dbContext.EmployeeLocations.Add(new EmployeeLocation
        {
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            LocationId = IntegrationTestData.LocalLocationId
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.EmployeeLocations.Add(new EmployeeLocation
        {
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.AdminEmployeeId,
            LocationId = IntegrationTestData.OtherLocationId
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.EmployeeTimeWindows.Add(new EmployeeTimeWindow
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 0),
            Type = EmployeeTimeWindowType.Preferred
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.EmployeeAllowedTimeTypes.Add(new EmployeeAllowedTimeType
        {
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            TimeType = TimeType.Work
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.UserPermissions.Add(new UserPermission
        {
            OrganizationId = IntegrationTestData.OrganizationId,
            UserId = IntegrationTestData.InactiveOrganizationUserId,
            Permission = ApplicationPermission.ManageUsers
        });
        await AssertSaveRejectedAsync(dbContext);

        var user = await dbContext.Users.SingleAsync(
            item => item.Id == IntegrationTestData.InactiveUserId);
        user.EmployeeId = IntegrationTestData.OtherEmployeeId;
        await AssertSaveRejectedAsync(dbContext);

        dbContext.WorkPreferences.Add(new WorkPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            Type = WorkPreferenceType.Available,
            DateFrom = new DateOnly(2026, 8, 1),
            DateTo = new DateOnly(2026, 8, 1),
            IsFullDay = true,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.WorkPreferences.Add(new WorkPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.AdminEmployeeId,
            LocationId = IntegrationTestData.OtherLocationId,
            Type = WorkPreferenceType.Available,
            DateFrom = new DateOnly(2026, 8, 1),
            DateTo = new DateOnly(2026, 8, 1),
            IsFullDay = true,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            CreatedByUserId = IntegrationTestData.AdminUserId,
            Type = LeaveType.AnnualLeave,
            DateFrom = new DateOnly(2026, 8, 1),
            DateTo = new DateOnly(2026, 8, 1),
            IsFullDay = true,
            Status = LeaveRequestStatus.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.AdminEmployeeId,
            CreatedByUserId = IntegrationTestData.InactiveOrganizationUserId,
            Type = LeaveType.AnnualLeave,
            DateFrom = new DateOnly(2026, 8, 1),
            DateTo = new DateOnly(2026, 8, 1),
            IsFullDay = true,
            Status = LeaveRequestStatus.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.AdminEmployeeId,
            CreatedByUserId = IntegrationTestData.AdminUserId,
            Type = LeaveType.AnnualLeave,
            DateFrom = new DateOnly(2026, 8, 1),
            DateTo = new DateOnly(2026, 8, 1),
            IsFullDay = true,
            Status = LeaveRequestStatus.Approved,
            DecidedByUserId = IntegrationTestData.InactiveOrganizationUserId,
            DecidedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await AssertSaveRejectedAsync(dbContext);

        var validLeave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.AdminEmployeeId,
            CreatedByUserId = IntegrationTestData.AdminUserId,
            Type = LeaveType.AnnualLeave,
            DateFrom = new DateOnly(2026, 8, 1),
            DateTo = new DateOnly(2026, 8, 1),
            IsFullDay = true,
            Status = LeaveRequestStatus.Approved,
            DecidedByUserId = IntegrationTestData.AdminUserId,
            DecidedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.LeaveRequests.Add(validLeave);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        dbContext.LeaveStatusHistories.Add(new LeaveStatusHistory
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LeaveRequestId = validLeave.Id,
            FromStatus = LeaveRequestStatus.Pending,
            ToStatus = LeaveRequestStatus.Approved,
            ActorUserId = IntegrationTestData.InactiveOrganizationUserId,
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await AssertSaveRejectedAsync(dbContext);
    }

    [TestMethod]
    public async Task Phase2BCompositeForeignKeysRejectCrossOrganizationPlanningLinks()
    {
        await using var application = new ApiFactory(
            PostgreSqlTestEnvironment.GetConnectionString());
        await application.ResetAndSeedDatabaseAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        var now = DateTimeOffset.UtcNow;

        dbContext.LocationWeeklyOpenings.Add(new LocationWeeklyOpening
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationId = IntegrationTestData.OtherLocationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.LocationShiftTemplates.Add(new LocationShiftTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationId = IntegrationTestData.OtherLocationId,
            Name = "Szervezetidegen",
            Category = ShiftTemplateCategory.Custom,
            WeekdayMask = 1,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 0),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.CoverageRequirements.Add(new CoverageRequirement
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationId = IntegrationTestData.OtherLocationId,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 0),
            RequiredCapability = StaffingCapability.Pharmacist,
            RequiredCount = 1,
            Severity = CoverageSeverity.Blocking,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.EmployeeCapabilities.Add(new EmployeeCapability
        {
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            Capability = StaffingCapability.Assistant,
            AssignedAtUtc = now
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.EmployeeWorkProfiles.Add(new EmployeeWorkProfile
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            ContractedMonthlyMinutes = 9_600,
            StandardShiftMinutes = 480,
            MinimumShiftMinutes = 240,
            MaximumRegularShiftMinutes = 600,
            MaximumDailyMinutes = 720,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await AssertSaveRejectedAsync(dbContext);

        dbContext.EmployeeShiftQuotaRules.Add(new EmployeeShiftQuotaRule
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            EmployeeId = IntegrationTestData.OtherEmployeeId,
            Dimension = ShiftQuotaDimension.MorningShift,
            Period = QuotaPeriod.Week,
            Minimum = 0,
            Target = 1,
            Maximum = 2,
            Severity = QuotaSeverity.Preferred,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await AssertSaveRejectedAsync(dbContext);

        var otherOpening = new LocationWeeklyOpening
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OtherOrganizationId,
            LocationId = IntegrationTestData.OtherLocationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.LocationWeeklyOpenings.Add(otherOpening);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        dbContext.OpeningIntervals.Add(new OpeningInterval
        {
            Id = Guid.NewGuid(),
            OrganizationId = IntegrationTestData.OrganizationId,
            LocationWeeklyOpeningId = otherOpening.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 0)
        });
        await AssertSaveRejectedAsync(dbContext);
    }

    private static async Task AssertSaveRejectedAsync(PatikaDbContext dbContext)
    {
        try
        {
            await dbContext.SaveChangesAsync();
            Assert.Fail("A szervezetek közötti kapcsolatot az adatbázisnak el kell utasítania.");
        }
        catch (DbUpdateException)
        {
            // Expected: the composite tenant foreign key is the enforcement boundary.
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private static Employee MigrationEmployee(
        Guid id,
        Guid organizationId,
        string name,
        ProfessionalRole professionalRole,
        bool countsAsPharmacist,
        DateTimeOffset now) =>
        new()
        {
            Id = id,
            OrganizationId = organizationId,
            FullName = name,
            DisplayName = name,
            ProfessionalRole = professionalRole,
            IsActive = true,
            IsSchedulable = true,
            IncludeInAutoFill = true,
            CountsAsPharmacist = countsAsPharmacist,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
}
