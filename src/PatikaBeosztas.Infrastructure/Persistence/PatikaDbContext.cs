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

    public DbSet<EmployeePayrollProfile> EmployeePayrollProfiles =>
        Set<EmployeePayrollProfile>();

    public DbSet<TaxAllowanceSurvey> TaxAllowanceSurveys =>
        Set<TaxAllowanceSurvey>();

    public DbSet<TaxDeclarationRequirement> TaxDeclarationRequirements =>
        Set<TaxDeclarationRequirement>();

    public DbSet<SchedulePlan> SchedulePlans => Set<SchedulePlan>();

    public DbSet<ScheduleGenerationRun> ScheduleGenerationRuns =>
        Set<ScheduleGenerationRun>();

    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();

    public DbSet<ShiftSegment> ShiftSegments => Set<ShiftSegment>();

    public DbSet<ScheduleIssue> ScheduleIssues => Set<ScheduleIssue>();

    public DbSet<ShiftExplanation> ShiftExplanations => Set<ShiftExplanation>();

    public DbSet<GeneratedSuggestionDecision> GeneratedSuggestionDecisions =>
        Set<GeneratedSuggestionDecision>();

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
        ConfigurePayrollOnboarding(builder);
        ConfigureScheduling(builder);
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
            entity.Property(template => template.TimeType)
                .HasConversion<string>()
                .HasMaxLength(30);
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
            entity.Property(requirement => requirement.TimeType)
                .HasConversion<string>()
                .HasMaxLength(30);
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

    private static void ConfigurePayrollOnboarding(ModelBuilder builder)
    {
        builder.Entity<EmployeePayrollProfile>(entity =>
        {
            entity.ToTable("EmployeePayrollProfiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.EmployeeNumber).HasMaxLength(50);
            entity.Property(profile => profile.TaxIdentificationNumberCiphertext)
                .HasMaxLength(2000);
            entity.Property(profile => profile.TaxIdentificationNumberHash)
                .HasMaxLength(64);
            entity.Property(profile => profile.PayrollExternalId).HasMaxLength(100);
            entity.Property(profile => profile.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(profile => profile.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(profile => new { profile.OrganizationId, profile.Id });
            entity.HasIndex(profile => new { profile.OrganizationId, profile.EmployeeId })
                .IsUnique();
            entity.HasIndex(profile => new
            {
                profile.OrganizationId,
                profile.EmployeeNumber
            }).IsUnique();
            entity.HasIndex(profile => new
            {
                profile.OrganizationId,
                profile.TaxIdentificationNumberHash
            }).IsUnique();
            entity.HasIndex(profile => new
            {
                profile.OrganizationId,
                profile.Status
            });
            entity.HasOne(profile => profile.Employee)
                .WithOne(employee => employee.PayrollProfile)
                .HasForeignKey<EmployeePayrollProfile>(profile => new
                {
                    profile.OrganizationId,
                    profile.EmployeeId
                })
                .HasPrincipalKey<Employee>(employee => new
                {
                    employee.OrganizationId,
                    employee.Id
                })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(profile => new
                {
                    profile.OrganizationId,
                    profile.CreatedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(profile => new
                {
                    profile.OrganizationId,
                    profile.UpdatedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaxAllowanceSurvey>(entity =>
        {
            entity.ToTable("TaxAllowanceSurveys");
            entity.HasKey(survey => survey.Id);
            entity.Property(survey => survey.FormVersion).HasMaxLength(50);
            entity.Property(survey => survey.RuleSetVersion).HasMaxLength(50);
            entity.Property(survey => survey.SourceMetadata).HasMaxLength(500);
            entity.Property(survey => survey.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(survey => survey.MonthlyAllowancePreference)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(survey => survey.MaritalStatus)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(survey => survey.FirstMarriageStatus)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(survey => survey.FamilyAllowanceClaimMode)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(survey => survey.OtherEligiblePersonClaimsPart)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(survey => survey.MotherAllowanceQualifyingChildrenCount)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(survey => survey.HasCurrentOwnChildOrFetusEligibleForFamilyAllowance)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(survey => survey.PersonalAllowanceEligibility)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(survey => survey.HasOtherEmployerOrRegularPayer)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(survey => survey.Under25AllowanceOptOut)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(survey => survey.ForeignTaxResidencyOrSimilarForeignBenefit)
                .HasConversion<string>()
                .HasMaxLength(40);
            entity.Property(survey => survey.FetusEligibilityMonth).HasMaxLength(7);
            entity.Property(survey => survey.PersonalAllowanceStartMonth).HasMaxLength(7);
            entity.Property(survey => survey.HrPayrollNote).HasMaxLength(1000);
            entity.Property(survey => survey.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(survey => new
            {
                survey.OrganizationId,
                survey.Id
            });
            entity.HasAlternateKey(survey => new
            {
                survey.OrganizationId,
                survey.EmployeeId,
                survey.Id
            });
            entity.HasIndex(survey => new
            {
                survey.OrganizationId,
                survey.EmployeeId,
                survey.TaxYear,
                survey.FormVersion
            }).IsUnique();
            entity.HasIndex(survey => new
            {
                survey.OrganizationId,
                survey.EmployeeId,
                survey.TaxYear,
                survey.Status
            });
            entity.HasIndex(survey => new
            {
                survey.OrganizationId,
                survey.Status
            });
            entity.HasOne(survey => survey.Employee)
                .WithMany(employee => employee.TaxAllowanceSurveys)
                .HasForeignKey(survey => new
                {
                    survey.OrganizationId,
                    survey.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(survey => new
                {
                    survey.OrganizationId,
                    survey.CreatedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(survey => new
                {
                    survey.OrganizationId,
                    survey.UpdatedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(survey => new
                {
                    survey.OrganizationId,
                    survey.DeclaredByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(survey => new
                {
                    survey.OrganizationId,
                    survey.ReviewedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaxDeclarationRequirement>(entity =>
        {
            entity.ToTable("TaxDeclarationRequirements");
            entity.HasKey(requirement => requirement.Id);
            entity.Property(requirement => requirement.Type)
                .HasConversion<string>()
                .HasMaxLength(40);
            entity.Property(requirement => requirement.Status)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(requirement => requirement.Notes).HasMaxLength(1000);
            entity.Property(requirement => requirement.GeneratedByRuleVersion)
                .HasMaxLength(50);
            entity.Property(requirement => requirement.ManualOverrideReason)
                .HasMaxLength(1000);
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
                requirement.EmployeeId,
                requirement.SurveyId,
                requirement.Type
            }).IsUnique();
            entity.HasIndex(requirement => new
            {
                requirement.OrganizationId,
                requirement.EmployeeId,
                requirement.Status
            });
            entity.HasIndex(requirement => new
            {
                requirement.OrganizationId,
                requirement.SurveyId,
                requirement.Status
            });
            entity.HasOne(requirement => requirement.Employee)
                .WithMany()
                .HasForeignKey(requirement => new
                {
                    requirement.OrganizationId,
                    requirement.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(requirement => requirement.Survey)
                .WithMany(survey => survey.DeclarationRequirements)
                .HasForeignKey(requirement => new
                {
                    requirement.OrganizationId,
                    requirement.EmployeeId,
                    requirement.SurveyId
                })
                .HasPrincipalKey(survey => new
                {
                    survey.OrganizationId,
                    survey.EmployeeId,
                    survey.Id
                })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(requirement => new
                {
                    requirement.OrganizationId,
                    requirement.CreatedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(requirement => new
                {
                    requirement.OrganizationId,
                    requirement.UpdatedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureScheduling(ModelBuilder builder)
    {
        builder.Entity<SchedulePlan>(entity =>
        {
            entity.ToTable("SchedulePlans");
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.TimeZoneId).HasMaxLength(100);
            entity.Property(plan => plan.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(plan => plan.AlgorithmVersion).HasMaxLength(100);
            entity.Property(plan => plan.GenerationOptionsSnapshot).HasColumnType("jsonb");
            entity.Property(plan => plan.InputSnapshotHash).HasMaxLength(64);
            entity.Property(plan => plan.CloneIdempotencyKeyHash).HasMaxLength(64);
            entity.Property(plan => plan.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(plan => new { plan.OrganizationId, plan.Id });
            entity.HasIndex(plan => new
            {
                plan.OrganizationId,
                plan.PeriodStart,
                plan.PeriodEnd,
                plan.Status
            });
            entity.HasIndex(plan => new
            {
                plan.OrganizationId,
                plan.PeriodStart,
                plan.PeriodEnd,
                plan.PublishedRevisionNumber
            }).IsUnique()
                .HasFilter("\"PublishedRevisionNumber\" > 0");
            entity.HasIndex(plan => new
            {
                plan.OrganizationId,
                plan.CloneIdempotencyKeyHash
            }).IsUnique()
                .HasFilter("\"CloneIdempotencyKeyHash\" IS NOT NULL");
            entity.HasOne(plan => plan.Organization)
                .WithMany()
                .HasForeignKey(plan => plan.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(plan => plan.BasedOnSchedule)
                .WithMany()
                .HasForeignKey(plan => new
                {
                    plan.OrganizationId,
                    plan.BasedOnScheduleId
                })
                .HasPrincipalKey(plan => new { plan.OrganizationId, plan.Id })
                .OnDelete(DeleteBehavior.Restrict);
            ConfigureScheduleActor(
                entity,
                plan => plan.CreatedByUserId);
            ConfigureScheduleActor(
                entity,
                plan => plan.UpdatedByUserId);
            ConfigureScheduleActor(
                entity,
                plan => plan.ReviewRequestedByUserId);
            ConfigureScheduleActor(
                entity,
                plan => plan.ApprovedByUserId);
            ConfigureScheduleActor(
                entity,
                plan => plan.PublishedByUserId);
            ConfigureScheduleActor(
                entity,
                plan => plan.ArchivedByUserId);
        });

        builder.Entity<ScheduleGenerationRun>(entity =>
        {
            entity.ToTable("ScheduleGenerationRuns");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(run => run.AlgorithmVersion).HasMaxLength(100);
            entity.Property(run => run.OptionsJson).HasColumnType("jsonb");
            entity.Property(run => run.InputSnapshotJson).HasColumnType("jsonb");
            entity.Property(run => run.InputSnapshotHash).HasMaxLength(64);
            entity.Property(run => run.SolverStatus)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(run => run.SolverStatisticsJson).HasColumnType("jsonb");
            entity.Property(run => run.ErrorCode).HasMaxLength(100);
            entity.Property(run => run.RedactedError).HasMaxLength(1000);
            entity.Property(run => run.IdempotencyKeyHash).HasMaxLength(64);
            entity.Property(run => run.ScopeConcurrencyKey).HasMaxLength(100);
            entity.Property(run => run.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(run => new { run.OrganizationId, run.Id });
            entity.HasIndex(run => new
            {
                run.OrganizationId,
                run.IdempotencyKeyHash
            }).IsUnique();
            entity.HasIndex(run => new
            {
                run.OrganizationId,
                run.Status,
                run.RequestedAtUtc
            });
            entity.HasIndex(run => new
            {
                run.OrganizationId,
                run.SchedulePlanId
            }).IsUnique()
                .HasFilter("\"Status\" IN ('Queued', 'Running')");
            entity.HasIndex(run => new
            {
                run.OrganizationId,
                run.ScopeConcurrencyKey
            }).IsUnique()
                .HasFilter("\"Status\" IN ('Queued', 'Running')");
            entity.HasOne(run => run.SchedulePlan)
                .WithMany(plan => plan.GenerationRuns)
                .HasForeignKey(run => new
                {
                    run.OrganizationId,
                    run.SchedulePlanId
                })
                .HasPrincipalKey(plan => new { plan.OrganizationId, plan.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(run => new
                {
                    run.OrganizationId,
                    run.RequestedByUserId
                })
                .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ShiftAssignment>(entity =>
        {
            entity.ToTable("ShiftAssignments");
            entity.HasKey(shift => shift.Id);
            entity.Property(shift => shift.Source)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(shift => shift.ChangeKind)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(shift => shift.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(shift => new { shift.OrganizationId, shift.Id });
            entity.HasIndex(shift => new
            {
                shift.OrganizationId,
                shift.SchedulePlanId,
                shift.Date,
                shift.EmployeeId
            });
            entity.HasIndex(shift => new
            {
                shift.OrganizationId,
                shift.SchedulePlanId,
                shift.LocationId,
                shift.Date,
                shift.StartTime
            });
            entity.HasOne(shift => shift.SchedulePlan)
                .WithMany(plan => plan.ShiftAssignments)
                .HasForeignKey(shift => new
                {
                    shift.OrganizationId,
                    shift.SchedulePlanId
                })
                .HasPrincipalKey(plan => new { plan.OrganizationId, plan.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(shift => shift.Employee)
                .WithMany()
                .HasForeignKey(shift => new
                {
                    shift.OrganizationId,
                    shift.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(shift => shift.Location)
                .WithMany()
                .HasForeignKey(shift => new
                {
                    shift.OrganizationId,
                    shift.LocationId
                })
                .HasPrincipalKey(location => new { location.OrganizationId, location.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(shift => shift.GeneratedByRun)
                .WithMany(run => run.GeneratedAssignments)
                .HasForeignKey(shift => new
                {
                    shift.OrganizationId,
                    shift.GeneratedByRunId
                })
                .HasPrincipalKey(run => new { run.OrganizationId, run.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(shift => shift.ReplacesShift)
                .WithMany()
                .HasForeignKey(shift => new
                {
                    shift.OrganizationId,
                    shift.ReplacesShiftId
                })
                .HasPrincipalKey(shift => new { shift.OrganizationId, shift.Id })
                .OnDelete(DeleteBehavior.Restrict);
            ConfigureScheduleActor(entity, shift => shift.CreatedByUserId);
            ConfigureScheduleActor(entity, shift => shift.UpdatedByUserId);
        });

        builder.Entity<ShiftSegment>(entity =>
        {
            entity.ToTable("ShiftSegments");
            entity.HasKey(segment => segment.Id);
            entity.Property(segment => segment.TimeType)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.HasIndex(segment => new
            {
                segment.OrganizationId,
                segment.ShiftAssignmentId,
                segment.StartTime
            });
            entity.HasOne(segment => segment.ShiftAssignment)
                .WithMany(shift => shift.Segments)
                .HasForeignKey(segment => new
                {
                    segment.OrganizationId,
                    segment.ShiftAssignmentId
                })
                .HasPrincipalKey(shift => new { shift.OrganizationId, shift.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ScheduleIssue>(entity =>
        {
            entity.ToTable("ScheduleIssues");
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.Code).HasMaxLength(100);
            entity.Property(issue => issue.Severity)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(issue => issue.ParametersJson).HasColumnType("jsonb");
            entity.Property(issue => issue.ResolutionNote).HasMaxLength(1000);
            entity.Property(issue => issue.Version)
                .IsRowVersion()
                .HasColumnName("xmin");
            entity.HasAlternateKey(issue => new { issue.OrganizationId, issue.Id });
            entity.HasIndex(issue => new
            {
                issue.OrganizationId,
                issue.SchedulePlanId,
                issue.Severity,
                issue.IsResolved
            });
            entity.HasIndex(issue => new
            {
                issue.OrganizationId,
                issue.Code,
                issue.Date
            });
            entity.HasOne(issue => issue.SchedulePlan)
                .WithMany(plan => plan.Issues)
                .HasForeignKey(issue => new
                {
                    issue.OrganizationId,
                    issue.SchedulePlanId
                })
                .HasPrincipalKey(plan => new { plan.OrganizationId, plan.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(issue => issue.GenerationRun)
                .WithMany(run => run.Issues)
                .HasForeignKey(issue => new
                {
                    issue.OrganizationId,
                    issue.GenerationRunId
                })
                .HasPrincipalKey(run => new { run.OrganizationId, run.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(issue => new
                {
                    issue.OrganizationId,
                    issue.EmployeeId
                })
                .HasPrincipalKey(employee => new { employee.OrganizationId, employee.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Location>()
                .WithMany()
                .HasForeignKey(issue => new
                {
                    issue.OrganizationId,
                    issue.LocationId
                })
                .HasPrincipalKey(location => new { location.OrganizationId, location.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ShiftAssignment>()
                .WithMany()
                .HasForeignKey(issue => new
                {
                    issue.OrganizationId,
                    issue.ShiftAssignmentId
                })
                .HasPrincipalKey(shift => new { shift.OrganizationId, shift.Id })
                .OnDelete(DeleteBehavior.Restrict);
            ConfigureScheduleActor(entity, issue => issue.ResolutionByUserId);
        });

        builder.Entity<ShiftExplanation>(entity =>
        {
            entity.ToTable("ShiftExplanations");
            entity.HasKey(explanation => explanation.Id);
            entity.Property(explanation => explanation.AlgorithmVersion)
                .HasMaxLength(100);
            entity.Property(explanation => explanation.ReasonCodesJson)
                .HasColumnType("jsonb");
            entity.Property(explanation => explanation.ScoreComponentsJson)
                .HasColumnType("jsonb");
            entity.Property(explanation => explanation.AlternativesJson)
                .HasColumnType("jsonb");
            entity.HasIndex(explanation => new
            {
                explanation.OrganizationId,
                explanation.ShiftAssignmentId
            }).IsUnique();
            entity.HasOne(explanation => explanation.ShiftAssignment)
                .WithOne(shift => shift.Explanation)
                .HasForeignKey<ShiftExplanation>(explanation => new
                {
                    explanation.OrganizationId,
                    explanation.ShiftAssignmentId
                })
                .HasPrincipalKey<ShiftAssignment>(shift => new
                {
                    shift.OrganizationId,
                    shift.Id
                })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SchedulePlan>()
                .WithMany()
                .HasForeignKey(explanation => new
                {
                    explanation.OrganizationId,
                    explanation.SchedulePlanId
                })
                .HasPrincipalKey(plan => new { plan.OrganizationId, plan.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ScheduleGenerationRun>()
                .WithMany()
                .HasForeignKey(explanation => new
                {
                    explanation.OrganizationId,
                    explanation.GenerationRunId
                })
                .HasPrincipalKey(run => new { run.OrganizationId, run.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GeneratedSuggestionDecision>(entity =>
        {
            entity.ToTable("GeneratedSuggestionDecisions");
            entity.HasKey(decision => decision.Id);
            entity.Property(decision => decision.DecisionType)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(decision => decision.ExclusionScope)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(decision => decision.Reason).HasMaxLength(1000);
            entity.HasIndex(decision => new
            {
                decision.OrganizationId,
                decision.SchedulePlanId,
                decision.ShiftAssignmentId,
                decision.OccurredAtUtc
            });
            entity.HasOne(decision => decision.ShiftAssignment)
                .WithMany()
                .HasForeignKey(decision => new
                {
                    decision.OrganizationId,
                    decision.ShiftAssignmentId
                })
                .HasPrincipalKey(shift => new { shift.OrganizationId, shift.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SchedulePlan>()
                .WithMany()
                .HasForeignKey(decision => new
                {
                    decision.OrganizationId,
                    decision.SchedulePlanId
                })
                .HasPrincipalKey(plan => new { plan.OrganizationId, plan.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ScheduleGenerationRun>()
                .WithMany()
                .HasForeignKey(decision => new
                {
                    decision.OrganizationId,
                    decision.GenerationRunId
                })
                .HasPrincipalKey(run => new { run.OrganizationId, run.Id })
                .OnDelete(DeleteBehavior.Restrict);
            ConfigureScheduleActor(entity, decision => decision.ActorUserId);
        });
    }

    private static void ConfigureScheduleActor<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        System.Linq.Expressions.Expression<Func<TEntity, Guid>> foreignKey)
        where TEntity : class =>
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey("OrganizationId", GetPropertyName(foreignKey))
            .HasPrincipalKey(
                nameof(ApplicationUser.OrganizationId),
                nameof(ApplicationUser.Id))
            .OnDelete(DeleteBehavior.Restrict);

    private static void ConfigureScheduleActor<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        System.Linq.Expressions.Expression<Func<TEntity, Guid?>> foreignKey)
        where TEntity : class =>
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey("OrganizationId", GetPropertyName(foreignKey))
            .HasPrincipalKey(
                nameof(ApplicationUser.OrganizationId),
                nameof(ApplicationUser.Id))
            .OnDelete(DeleteBehavior.Restrict);

    private static string GetPropertyName<TEntity, TProperty>(
        System.Linq.Expressions.Expression<Func<TEntity, TProperty>> expression) =>
        expression.Body is System.Linq.Expressions.MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("Egyszerű property expression szükséges.", nameof(expression));

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
