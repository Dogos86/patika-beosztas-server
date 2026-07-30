using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PatikaBeosztas.Infrastructure.Persistence;

public static class PostgreSqlConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "A ConnectionStrings:DefaultConnection konfiguráció kötelező.");
        }

        return Normalize(configured);
    }

    public static string Normalize(string configured)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configured);

        if (!configured.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !configured.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return configured;
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                "A Railway PostgreSQL URL formátuma érvénytelen.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2 ||
            string.IsNullOrWhiteSpace(userInfo[0]) ||
            string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            throw new InvalidOperationException(
                "A Railway PostgreSQL URL nem tartalmaz teljes hitelesítési adatot.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            IncludeErrorDetail = false
        };

        foreach (var item in uri.Query.TrimStart('?').Split(
                     '&',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            if (parts.Length == 2 &&
                string.Equals(
                    Uri.UnescapeDataString(parts[0]),
                    "sslmode",
                    StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = Enum.Parse<SslMode>(
                    Uri.UnescapeDataString(parts[1]),
                    ignoreCase: true);
            }
        }

        return builder.ConnectionString;
    }
}
