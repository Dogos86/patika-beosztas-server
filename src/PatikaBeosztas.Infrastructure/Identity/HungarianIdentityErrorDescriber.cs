using Microsoft.AspNetCore.Identity;

namespace PatikaBeosztas.Infrastructure.Identity;

public sealed class HungarianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Ismeretlen fiókkezelési hiba történt.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "A fiók adatai időközben megváltoztak. Töltse újra az adatokat.");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "A biztonsági token érvénytelen vagy lejárt.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "A felhasználónév érvénytelen.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "Az email-cím érvénytelen.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Ezzel az email-címmel már létezik felhasználónév.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Ezzel az email-címmel már létezik felhasználó.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"A jelszónak legalább {length} karakter hosszúnak kell lennie.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(
            nameof(PasswordRequiresUniqueChars),
            $"A jelszónak legalább {uniqueChars} különböző karaktert kell tartalmaznia.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "A jelszónak speciális karaktert is kell tartalmaznia.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "A jelszónak számjegyet is kell tartalmaznia.");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "A jelszónak kisbetűt is kell tartalmaznia.");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "A jelszónak nagybetűt is kell tartalmaznia.");

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "A felhasználóhoz már tartozik jelszó.");

    public override IdentityError LoginAlreadyAssociated() =>
        Error(nameof(LoginAlreadyAssociated), "Ez a külső bejelentkezés már másik fiókhoz tartozik.");

    private static IdentityError Error(string code, string description) =>
        new()
        {
            Code = code,
            Description = description
        };
}
