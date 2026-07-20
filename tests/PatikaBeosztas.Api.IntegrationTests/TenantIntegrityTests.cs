using Microsoft.EntityFrameworkCore;
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
}
