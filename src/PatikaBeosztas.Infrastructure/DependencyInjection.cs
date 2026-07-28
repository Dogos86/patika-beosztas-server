using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatikaBeosztas.Infrastructure.Identity;
using PatikaBeosztas.Infrastructure.Persistence;
using PatikaBeosztas.Infrastructure.Security;
using PatikaBeosztas.Application.Security;

namespace PatikaBeosztas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<PatikaDbContext>((serviceProvider, options) =>
        {
            var currentConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = currentConfiguration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "A ConnectionStrings:DefaultConnection konfiguráció kötelező.");
            }

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
        services.AddDataProtection()
            .SetApplicationName("PatikaBeosztas.Payroll");
        services.AddSingleton<ITaxIdentifierProtector, TaxIdentifierProtector>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
