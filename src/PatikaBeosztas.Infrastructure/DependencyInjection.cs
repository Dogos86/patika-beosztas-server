using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;
using PatikaBeosztas.Infrastructure.Security;
using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Infrastructure.Scheduling;

namespace PatikaBeosztas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<PatikaDbContext>((serviceProvider, options) =>
        {
            var currentConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString =
                PostgreSqlConnectionString.Resolve(currentConfiguration);
            options.UseNpgsql(connectionString);
        });
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<PatikaDbContext>()
            .AddErrorDescriber<HungarianIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<AuditWriter>();
        services.AddSingleton<ITaxIdentifierProtector, TaxIdentifierProtector>();
        services.AddScoped<PilotAdminBootstrapper>();
        services.AddScoped<ScheduleInputSnapshotFactory>();
        services.AddSingleton<IScheduleOptimizer, OrToolsScheduleOptimizer>();
        services.AddHostedService<ScheduleGenerationBackgroundService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
