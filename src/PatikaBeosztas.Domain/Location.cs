namespace PatikaBeosztas.Domain;

public sealed class Location
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public required string Name { get; set; }

    public LocationType Type { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Organization? Organization { get; set; }

    public ICollection<EmployeeLocation> Employees { get; } = new List<EmployeeLocation>();
}
