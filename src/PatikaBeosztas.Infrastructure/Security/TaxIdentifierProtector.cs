using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using PatikaBeosztas.Application.Security;

namespace PatikaBeosztas.Infrastructure.Security;

public sealed class TaxIdentifierProtector : ITaxIdentifierProtector
{
    private const string HashKeyConfigurationName =
        "SensitiveData:TaxIdentifierHashKey";
    private readonly IDataProtector dataProtector;
    private readonly byte[] hashKey;

    public TaxIdentifierProtector(
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        dataProtector = dataProtectionProvider.CreateProtector(
            "PatikaBeosztas.Payroll.TaxIdentifier.v1");

        var configuredHashKey = configuration[HashKeyConfigurationName];
        if (string.IsNullOrWhiteSpace(configuredHashKey))
        {
            throw new InvalidOperationException(
                $"A {HashKeyConfigurationName} konfiguráció kötelező.");
        }

        try
        {
            hashKey = Convert.FromBase64String(configuredHashKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"A {HashKeyConfigurationName} érvényes Base64 érték legyen.",
                exception);
        }

        if (hashKey.Length < 32)
        {
            throw new InvalidOperationException(
                $"A {HashKeyConfigurationName} legalább 32 bájtos kulcsot tartalmazzon.");
        }
    }

    public string Protect(string taxIdentificationNumber) =>
        dataProtector.Protect(Normalize(taxIdentificationNumber));

    public string Unprotect(string protectedValue) =>
        dataProtector.Unprotect(protectedValue);

    public string ComputeLookupHash(string taxIdentificationNumber)
    {
        var normalized = Normalize(taxIdentificationNumber);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return Convert.ToHexString(
                HMACSHA256.HashData(hashKey, bytes))
            .ToLower(CultureInfo.InvariantCulture);
    }

    public string Mask(string taxIdentificationNumber)
    {
        var normalized = Normalize(taxIdentificationNumber);
        return normalized.Length <= 4
            ? new string('*', normalized.Length)
            : string.Concat(
                new string('*', normalized.Length - 4),
                normalized.AsSpan(normalized.Length - 4));
    }

    private static string Normalize(string value) => value.Trim();
}
