namespace PharmacyScheduler.WinForms.ViewModels;

public sealed class LocationGridRow
{
    public Guid Id { get; init; }
    public string Név { get; init; } = string.Empty;
    public string Cím { get; init; } = string.Empty;
    public bool Aktív { get; init; }
}

public sealed class EmployeeGridRow
{
    public Guid Id { get; init; }
    public string TeljesNév { get; init; } = string.Empty;
    public string MegjelenítésiNév { get; init; } = string.Empty;
    public string Szerepkör { get; init; } = string.Empty;
    public string Telephelyek { get; init; } = string.Empty;
    public decimal HaviKeret { get; init; }
    public decimal MaxNapiÓra { get; init; }
    public string PreferáltIdősávok { get; init; } = string.Empty;
    public bool Aktív { get; init; }
}

public sealed class CoverageGridRow
{
    public Guid Id { get; init; }
    public string Telephely { get; init; } = string.Empty;
    public string Nap { get; init; } = string.Empty;
    public string Kezdés { get; init; } = string.Empty;
    public string Vége { get; init; } = string.Empty;
    public string Szerepkör { get; init; } = string.Empty;
    public int MinimumLétszám { get; init; }
    public string Súlyosság { get; init; } = string.Empty;
}

public sealed class LeaveGridRow
{
    public Guid Id { get; init; }
    public string Dolgozó { get; init; } = string.Empty;
    public string Kezdete { get; init; } = string.Empty;
    public string Vége { get; init; } = string.Empty;
    public string Típus { get; init; } = string.Empty;
    public string Megjegyzés { get; init; } = string.Empty;
}

public sealed class ShiftGridRow
{
    public Guid Id { get; init; }
    public string Dátum { get; init; } = string.Empty;
    public string Kezdés { get; init; } = string.Empty;
    public string Vége { get; init; } = string.Empty;
    public string Dolgozó { get; init; } = string.Empty;
    public string Telephely { get; init; } = string.Empty;
    public string Szerepkör { get; init; } = string.Empty;
    public string IdőTípus { get; init; } = string.Empty;
    public decimal Óraszám { get; init; }
    public string Megjegyzés { get; init; } = string.Empty;
}
