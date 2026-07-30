using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PatikaBeosztas.Infrastructure.Security;

public static class DataProtectionConfiguration
{
    public const string DefaultApplicationName = "PatikaBeosztas";

    public static IServiceCollection AddConfiguredDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var applicationName =
            configuration["DataProtection:ApplicationName"] ??
            DefaultApplicationName;
        var builder = services
            .AddDataProtection()
            .SetApplicationName(applicationName);

        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }
        return services;
    }
}
