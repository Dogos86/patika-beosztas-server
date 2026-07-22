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

    public DbSet<WorkPreference> WorkPreferences => Set<WorkPreference>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<LeaveStatusHistory> LeaveStatusHistories => Set<LeaveStatusHistory>();

    public DbSet<LocationWeeklyOpening> LocationWeeklyOpenings =>
        Set<LocationWeeklyOpening>();

    public DbSet<OpeningInterval> OpeningIntervals => Set<OpeningInterval>();

    public DbSet<LocationShiftTemplate> LocationShiftTemplates =>
        Set<LocationShiftTemplate>();

    public DbSet<EmployeeCapability> EmployeeCapabilities => Set<EmployeeCapability>();

    public DbSet<CoverageRequirement> CoverageRequirements => Set<CoverageRequirement>();

    public DbSet<EmployeeWorkProfile> EmployeeWorkProfiles => Set<EmployeeWorkProfile>();

    public DbSet<EmployeeShiftQuotaRule> EmployeeShiftQuotaRules =>
        Set<EmployeeShiftQuotaRule>();

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
        ConfigureWorkPreferences(builder);
        ConfigureLeaveRequests(builder);
        ConfigureLocationPlanning(builder);
        ConfigureEmployeePlanning(builder);
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
            entity.Property(user => user.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(user => new { user.OrganizationId, user.Id });
            entity.HasIndex(user => new { user.OrganizationId, user.IsActive });
            entity.HasIndex(user => new { user.OrganizationId, user.EmployeeId })
                .IsUnique()
                .HasFilter("\"EmployeeId\" IS NOT NULL");
            entity.HasOne(user => user.Organization)
                .WithMany()
                .HasForeignKey(user => user.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(user => user.Employee)
                .WithMany()
                .HasForeignKey(user => new { user.OrganizationId, user.EmployeeId })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
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
            entity.HasAlternateKey(employee => new { employee.OrganizationId, employee.Id });
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
            entity.HasAlternateKey(location => new { location.OrganizationId, location.Id });
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
                .HasForeignKey(item => new { item.OrganizationId, item.EmployeeId })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Location)
                .WithMany(location => location.Employees)
                .HasForeignKey(item => new { item.OrganizationId, item.LocationId })
                .HasPrincipalKey(location => new { location.OrganizationId, location.Id })
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
                .HasForeignKey(window => new { window.OrganizationId, window.EmployeeId })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
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
                .HasForeignKey(item => new { item.OrganizationId, item.EmployeeId })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
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
                .HasForeignKey(item => new { item.OrganizationId, item.UserId })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWorkPreferences(ModelBuilder builder)
    {
        builder.Entity<WorkPreference>(entity =>
        {
            entity.ToTable("WorkPreferences");
            entity.HasKey(preference => preference.Id);
            entity.Property(preference => preference.Type)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(preference => preference.Note).HasMaxLength(1000);
            entity.Property(preference => preference.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(preference => new
            {
                preference.OrganizationId,
                preference.Id
            });
            entity.HasIndex(preference => new
            {
                preference.OrganizationId,
                preference.EmployeeId,
                preference.IsActive,
                preference.DateFrom,
                preference.DateTo
            });
            entity.HasOne(preference => preference.Employee)
                .WithMany()
                .HasForeignKey(preference => new
                {
                    preference.OrganizationId,
                    preference.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(preference => preference.Location)
                .WithMany()
                .HasForeignKey(preference => new
                {
                    preference.OrganizationId,
                    preference.LocationId
                })
                .HasPrincipalKey(location => new { location.OrganizationId, location.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLeaveRequests(ModelBuilder builder)
    {
        builder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Type)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(request => request.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(request => request.EmployeeNote).HasMaxLength(1000);
            entity.Property(request => request.DecisionReason).HasMaxLength(1000);
            entity.Property(request => request.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(request => new
            {
                request.OrganizationId,
                request.Id
            });
            entity.HasIndex(request => new
            {
                request.OrganizationId,
                request.EmployeeId,
                request.Status,
                request.DateFrom
            });
            entity.HasOne(request => request.Employee)
                .WithMany()
                .HasForeignKey(request => new
                {
                    request.OrganizationId,
                    request.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(request => new
                {
                    request.OrganizationId,
                    request.CreatedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(request => new
                {
                    request.OrganizationId,
                    request.DecidedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LeaveStatusHistory>(entity =>
        {
            entity.ToTable("LeaveStatusHistories");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.FromStatus)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(history => history.ToStatus)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(history => history.Reason).HasMaxLength(1000);
            entity.HasIndex(history => new
            {
                history.OrganizationId,
                history.LeaveRequestId,
                history.OccurredAtUtc
            });
            entity.HasOne(history => history.LeaveRequest)
                .WithMany(request => request.StatusHistory)
                .HasForeignKey(history => new
                {
                    history.OrganizationId,
                    history.LeaveRequestId
                })
                .HasPrincipalKey(request => new { request.OrganizationId, request.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(history => new
                {
                    history.OrganizationId,
                    history.ActorUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLocationPlanning(ModelBuilder builder)
    {
        builder.Entity<LocationWeeklyOpening>(entity =>
        {
            entity.ToTable("LocationWeeklyOpenings");
            entity.HasKey(opening => opening.Id);
            entity.Property(opening => opening.SundayMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(opening => opening.MondayMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(opening => opening.TuesdayMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(opening => opening.WednesdayMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(opening => opening.ThursdayMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(opening => opening.FridayMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(opening => opening.SaturdayMode).HasConversion<string>().HasMaxLength(30);
            entity.Property(opening => opening.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(opening => new { opening.OrganizationId, opening.Id });
            entity.HasIndex(opening => new { opening.OrganizationId, opening.LocationId })
                .IsUnique();
            entity.HasOne(opening => opening.Location)
                .WithOne(location => location.WeeklyOpening)
                .HasForeignKey<LocationWeeklyOpening>(opening => new
                {
                    opening.OrganizationId,
                    opening.LocationId
                })
                .HasPrincipalKey<Location>(location => new
                {
                    location.OrganizationId,
                    location.Id
                })
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OpeningInterval>(entity =>
        {
            entity.ToTable("OpeningIntervals");
            entity.HasKey(interval => interval.Id);
            entity.HasIndex(interval => new
            {
                interval.OrganizationId,
                interval.LocationWeeklyOpeningId,
                interval.DayOfWeek,
                interval.StartTime
            });
            entity.HasOne(interval => interval.WeeklyOpening)
                .WithMany(opening => opening.Intervals)
                .HasForeignKey(interval => new
                {
                    interval.OrganizationId,
                    interval.LocationWeeklyOpeningId
                })
                .HasPrincipalKey(opening => new { opening.OrganizationId, opening.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LocationShiftTemplate>(entity =>
        {
            entity.ToTable("LocationShiftTemplates");
            entity.HasKey(template => template.Id);
            entity.Property(template => template.Name).HasMaxLength(100);
            entity.Property(template => template.Category)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(template => template.RequiredCapability)
                .HasConversion<string>()
                .HasMaxLength(40);
            entity.Property(template => template.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(template => new { template.OrganizationId, template.Id });
            entity.HasIndex(template => new
            {
                template.OrganizationId,
                template.LocationId,
                template.IsActive
            });
            entity.HasOne(template => template.Location)
                .WithMany(location => location.ShiftTemplates)
                .HasForeignKey(template => new
                {
                    template.OrganizationId,
                    template.LocationId
                })
                .HasPrincipalKey(location => new { location.OrganizationId, location.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CoverageRequirement>(entity =>
        {
            entity.ToTable("CoverageRequirements");
            entity.HasKey(requirement => requirement.Id);
            entity.Property(requirement => requirement.RequiredCapability)
                .HasConversion<string>()
                .HasMaxLength(40);
            entity.Property(requirement => requirement.Severity)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(requirement => requirement.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(requirement => new
            {
                requirement.OrganizationId,
                requirement.Id
            });
            entity.HasIndex(requirement => new
            {
                requirement.OrganizationId,
                requirement.LocationId,
                requirement.DayOfWeek,
                requirement.IsActive
            });
            entity.HasIndex(requirement => new
            {
                requirement.OrganizationId,
                requirement.RequiredCapability,
                requirement.DayOfWeek,
                requirement.StartTime,
                requirement.EndTime
            });
            entity.HasOne(requirement => requirement.Location)
                .WithMany(location => location.CoverageRequirements)
                .HasForeignKey(requirement => new
                {
                    requirement.OrganizationId,
                    requirement.LocationId
                })
                .HasPrincipalKey(location => new { location.OrganizationId, location.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureEmployeePlanning(ModelBuilder builder)
    {
        builder.Entity<EmployeeCapability>(entity =>
        {
            entity.ToTable("EmployeeCapabilities");
            entity.HasKey(capability => new
            {
                capability.EmployeeId,
                capability.Capability
            });
            entity.Property(capability => capability.Capability)
                .HasConversion<string>()
                .HasMaxLength(40);
            entity.HasIndex(capability => new
            {
                capability.OrganizationId,
                capability.Capability,
                capability.EmployeeId
            });
            entity.HasOne(capability => capability.Employee)
                .WithMany(employee => employee.Capabilities)
                .HasForeignKey(capability => new
                {
                    capability.OrganizationId,
                    capability.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeWorkProfile>(entity =>
        {
            entity.ToTable("EmployeeWorkProfiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(profile => new { profile.OrganizationId, profile.Id });
            entity.HasIndex(profile => new { profile.OrganizationId, profile.EmployeeId })
                .IsUnique();
            entity.HasOne(profile => profile.Employee)
                .WithOne(employee => employee.WorkProfile)
                .HasForeignKey<EmployeeWorkProfile>(profile => new
                {
                    profile.OrganizationId,
                    profile.EmployeeId
                })
                .HasPrincipalKey<Employee>(employee => new
                {
                    employee.OrganizationId,
                    employee.Id
                })
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeShiftQuotaRule>(entity =>
        {
            entity.ToTable("EmployeeShiftQuotaRules");
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.Dimension)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(rule => rule.Period)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(rule => rule.Severity)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(rule => rule.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(rule => new { rule.OrganizationId, rule.Id });
            entity.HasIndex(rule => new
            {
                rule.OrganizationId,
                rule.EmployeeId,
                rule.Dimension,
                rule.Period
            }).IsUnique();
            entity.HasIndex(rule => new
            {
                rule.OrganizationId,
                rule.EmployeeId,
                rule.IsActive
            });
            entity.HasOne(rule => rule.Employee)
                .WithMany(employee => employee.ShiftQuotaRules)
                .HasForeignKey(rule => new
                {
                    rule.OrganizationId,
                    rule.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Cascade);
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

        var illegalHistoryEntries = ChangeTracker.Entries<LeaveStatusHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .ToArray();
        if (illegalHistoryEntries.Length > 0)
        {
            throw new InvalidOperationException(
                "A távolléti státusztörténet nem módosítható és nem törölhető.");
        }
    }
}
