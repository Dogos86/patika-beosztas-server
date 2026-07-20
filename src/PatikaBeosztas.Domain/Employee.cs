namespace PatikaBeosztas.Domain;

public sealed class Employee
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public required string FullName { get; set; }

    public required string DisplayName { get; set; }

    public ProfessionalRole ProfessionalRole { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSchedulable { get; set; }

    public bool IncludeInAutoFill { get; set; }

    public bool CountsAsPharmacist { get; set; }

    public int? MonthlyMinutesLimit { get; set; }

    public int? MaxDailyMinutes { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? ExternalPayrollId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Organization? Organization { get; set; }

    public ICollection<EmployeeLocation> Locations { get; } = new List<EmployeeLocation>();

    public ICollection<EmployeeTimeWindow> TimeWindows { get; } = new List<EmployeeTimeWindow>();

    public ICollection<EmployeeAllowedTimeType> AllowedTimeTypes { get; } =
        new List<EmployeeAllowedTimeType>();
}
