using Microsoft.EntityFrameworkCore;
using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api;

public static class PilotRuntimeCommands
{
    public static async Task<int?> TryExecuteAsync(
        string[] args,
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (args.Length == 0)
        {
            return null;
        }

        return args[0] switch
        {
            "migrate" => await MigrateAsync(services, cancellationToken),
            "bootstrap-admin" => await BootstrapAdminAsync(
                args,
                services,
                configuration,
                cancellationToken),
            _ => null
        };
    }

    private static async Task<int> MigrateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        var pending = await dbContext.Database
            .GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            throw new InvalidOperationException(
                "A migráció után is maradt függő EF migráció.");
        }

        await Console.Out.WriteLineAsync(
            "Az EF Core migrációk sikeresen alkalmazva; az adatbázis séma naprakész.");
        return 0;
    }

    private static async Task<int> BootstrapAdminAsync(
        string[] args,
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var options = ParseOptions(args.Skip(1).ToArray());
        var organizationName = Required(options, "--organization-name");
        var email = Required(options, "--email");
        var displayName = Required(options, "--display-name");
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "A BootstrapAdmin__Password környezeti változó kötelező.");
        }

        await using var scope = services.CreateAsyncScope();
        var bootstrapper =
            scope.ServiceProvider.GetRequiredService<PilotAdminBootstrapper>();
        var result = await bootstrapper.BootstrapAsync(
            new PilotAdminBootstrapRequest(
                organizationName,
                email,
                displayName,
                password),
            cancellationToken);

        var outcome = result.Created
            ? "Az első szervezet és admin létrejött."
            : "Az első szervezet és admin már létezett; nem történt módosítás.";
        await Console.Out.WriteLineAsync(
            $"{outcome} OrganizationId={result.OrganizationId}; UserId={result.UserId}.");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        if (args.Length == 0 || args.Length % 2 != 0)
        {
            throw Usage();
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            var name = args[index];
            if (name is not "--organization-name" and
                not "--email" and
                not "--display-name")
            {
                throw Usage();
            }

            if (!options.TryAdd(name, args[index + 1]))
            {
                throw Usage();
            }
        }

        return options;
    }

    private static string Required(
        Dictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            throw Usage();
        }

        return value;
    }

    private static InvalidOperationException Usage() =>
        new(
            "Használat: bootstrap-admin --organization-name <név> " +
            "--email <email> --display-name <név>. " +
            "A jelszót a BootstrapAdmin__Password secret adja.");
}
