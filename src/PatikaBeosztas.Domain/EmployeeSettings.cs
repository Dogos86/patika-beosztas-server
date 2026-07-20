namespace PatikaBeosztas.Domain;

public sealed class EmployeeLocation
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid LocationId { get; set; }

    public bool Enabled { get; set; } = true;

    public Employee? Employee { get; set; }

    public Location? Location { get; set; }
}

public sealed class EmployeeTimeWindow
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public DayOfWeek? DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public EmployeeTimeWindowType Type { get; set; }

    public Employee? Employee { get; set; }
}

public sealed class EmployeeAllowedTimeType
{
    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public TimeType TimeType { get; set; }

    public Employee? Employee { get; set; }
}
