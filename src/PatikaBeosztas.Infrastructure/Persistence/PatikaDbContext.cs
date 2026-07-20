using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Domain;
using PatikaBeosztas.Infrastructure.Identity;

namespace PatikaBeosztas.Infrastructure.Persistence;

public sealed class PatikaDbContext(
    DbContextOptions<PatikaDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<EmployeeLocation> EmployeeLocations => Set<EmployeeLocation>();

    public DbSet<EmployeeTimeWindow> EmployeeTimeWindows => Set<EmployeeTimeWindow>();

    public DbSet<EmployeeAllowedTimeType> EmployeeAllowedTimeTypes =>
        Set<EmployeeAllowedTimeType>();

    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureOrganizations(builder);
        ConfigureEmployees(builder);
        ConfigureLocations(builder);
        ConfigureEmployeeSettings(builder);
        ConfigurePermissions(builder);
        ConfigureAudit(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectAuditLogs();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectAuditLogs();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.DisplayName).HasMaxLength(100);
            entity.HasIndex(user => new { user.OrganizationId, user.IsActive });
            entity.HasIndex(user => user.EmployeeId)
                .IsUnique()
                .HasFilter("\"EmployeeId\" IS NOT NULL");
            entity.HasOne(user => user.Organization)
                .WithMany()
                .HasForeignKey(user => user.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(user => user.Employee)
                .WithMany()
                .HasForeignKey(user => user.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
    }

    private static void ConfigureOrganizations(ModelBuilder builder)
    {
        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name).HasMaxLength(200);
            entity.Property(organization => organization.TimeZoneId).HasMaxLength(100);
            entity.HasIndex(organization => organization.Name);
            entity.HasIndex(organization => organization.IsActive);
        });
    }

    private static void ConfigureEmployees(ModelBuilder builder)
    {
        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(employee => employee.Id);
            entity.Property(employee => employee.FullName).HasMaxLength(200);
            entity.Property(employee => employee.DisplayName).HasMaxLength(100);
            entity.Property(employee => employee.ProfessionalRole)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.Property(employee => employee.ExternalPayrollId).HasMaxLength(100);
            entity.Property(employee => employee.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasIndex(employee => new { employee.OrganizationId, employee.IsActive });
            entity.HasIndex(employee => new { employee.OrganizationId, employee.DisplayName });
            entity.HasIndex(employee => new { employee.OrganizationId, employee.ExternalPayrollId });
            entity.HasOne(employee => employee.Organization)
                .WithMany()
                .HasForeignKey(employee => employee.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLocations(ModelBuilder builder)
    {
        builder.Entity<Location>(entity =>
        {
            entity.ToTable("Locations");
            entity.HasKey(location => location.Id);
            entity.Property(location => location.Name).HasMaxLength(200);
            entity.Property(location => location.Address).HasMaxLength(500);
            entity.Property(location => location.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(location => location.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasIndex(location => new { location.OrganizationId, location.IsActive });
            entity.HasIndex(location => new { location.OrganizationId, location.Name });
            entity.HasOne(location => location.Organization)
                .WithMany()
                .HasForeignKey(location => location.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEmployeeSettings(ModelBuilder builder)
    {
        builder.Entity<EmployeeLocation>(entity =>
        {
            entity.ToTable("EmployeeLocations");
            entity.HasKey(item => new { item.EmployeeId, item.LocationId });
            entity.HasIndex(item => new { item.OrganizationId, item.LocationId });
            entity.HasOne(item => item.Employee)
                .WithMany(employee => employee.Locations)
                .HasForeignKey(item => item.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Location)
                .WithMany(location => location.Employees)
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeTimeWindow>(entity =>
        {
            entity.ToTable("EmployeeTimeWindows");
            entity.HasKey(window => window.Id);
            entity.Property(window => window.Type).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(window => new { window.OrganizationId, window.EmployeeId });
            entity.HasOne(window => window.Employee)
                .WithMany(employee => employee.TimeWindows)
                .HasForeignKey(window => window.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeAllowedTimeType>(entity =>
        {
            entity.ToTable("EmployeeAllowedTimeTypes");
            entity.HasKey(item => new { item.EmployeeId, item.TimeType });
            entity.Property(item => item.TimeType).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(item => new { item.OrganizationId, item.EmployeeId });
            entity.HasOne(item => item.Employee)
                .WithMany(employee => employee.AllowedTimeTypes)
                .HasForeignKey(item => item.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePermissions(ModelBuilder builder)
    {
        builder.Entity<UserPermission>(entity =>
        {
            entity.ToTable("UserPermissions");
            entity.HasKey(item => new { item.UserId, item.Permission });
            entity.Property(item => item.Permission).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(item => new { item.OrganizationId, item.Permission });
            entity.HasOne<ApplicationUser>()
                .WithMany(user => user.Permissions)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAudit(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasMaxLength(100);
            entity.Property(log => log.EntityType).HasMaxLength(100);
            entity.Property(log => log.EntityId).HasMaxLength(100);
            entity.Property(log => log.CorrelationId).HasMaxLength(100);
            entity.Property(log => log.ChangeSummary).HasMaxLength(1000);
            entity.HasIndex(log => new { log.OrganizationId, log.TimestampUtc });
            entity.HasIndex(log => log.CorrelationId);
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(log => log.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ProtectAuditLogs()
    {
        var illegalEntries = ChangeTracker.Entries<AuditLog>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .ToArray();
        if (illegalEntries.Length > 0)
        {
            throw new InvalidOperationException("Az auditbejegyzések nem módosíthatók és nem törölhetők.");
        }
    }
}
