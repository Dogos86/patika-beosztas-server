namespace PatikaBeosztas.Application.Security;

public interface ITaxIdentifierProtector
{
    string Protect(string taxIdentificationNumber);

    string Unprotect(string protectedValue);

    string ComputeLookupHash(string taxIdentificationNumber);

    string Mask(string taxIdentificationNumber);
}
