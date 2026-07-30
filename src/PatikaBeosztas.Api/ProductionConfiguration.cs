using PatikaBeosztas.Infrastructure.Persistence;

namespace PatikaBeosztas.Api;

public static class ProductionConfiguration
{
    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsProduction())
        {
            return;
        }

        _ = PostgreSqlConnectionString.Resolve(configuration);

        if (configuration.GetValue<bool>("Seed:Enabled"))
        {
            throw new InvalidOperationException(
                "Production környezetben a Seed:Enabled nem lehet true.");
        }

        if (configuration.GetValue<bool>("OpenApi:Enabled"))
        {
            throw new InvalidOperationException(
                "Production környezetben az OpenApi:Enabled nem lehet true.");
        }

        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath) ||
            !Path.IsPathFullyQualified(keysPath))
        {
            throw new InvalidOperationException(
                "Production környezetben abszolút DataProtection:KeysPath kötelező.");
        }

        var applicationName = configuration["DataProtection:ApplicationName"];
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new InvalidOperationException(
                "Production környezetben a DataProtection:ApplicationName kötelező.");
        }

        ValidateHashKey(configuration["SensitiveData:TaxIdentifierHashKey"]);
        EnsureWritableDirectory(keysPath);
    }

    private static void ValidateHashKey(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Production környezetben a SensitiveData:TaxIdentifierHashKey kötelező.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configured);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "A SensitiveData:TaxIdentifierHashKey érvényes Base64 érték legyen.",
                exception);
        }

        if (key.Length < 32)
        {
            throw new InvalidOperationException(
                "A SensitiveData:TaxIdentifierHashKey legalább 32 bájtos kulcsot tartalmazzon.");
        }
    }

    private static void EnsureWritableDirectory(string keysPath)
    {
        try
        {
            Directory.CreateDirectory(keysPath);
            var probePath = Path.Combine(
                keysPath,
                $".patika-write-probe-{Guid.NewGuid():N}");
            using var probe = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            probe.WriteByte(0);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "A Data Protection kulcskönyvtár nem írható.",
                exception);
        }
    }
}
