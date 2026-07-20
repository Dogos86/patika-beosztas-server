namespace PatikaBeosztas.Domain;

public sealed class Organization
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string TimeZoneId { get; set; } = "Europe/Budapest";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
