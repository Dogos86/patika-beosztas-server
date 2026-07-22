namespace PatikaBeosztas.Domain;

public sealed class LocationWeeklyOpening
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid LocationId { get; set; }

    public OpeningDayMode SundayMode { get; set; }

    public OpeningDayMode MondayMode { get; set; }

    public OpeningDayMode TuesdayMode { get; set; }

    public OpeningDayMode WednesdayMode { get; set; }

    public OpeningDayMode ThursdayMode { get; set; }

    public OpeningDayMode FridayMode { get; set; }

    public OpeningDayMode SaturdayMode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Location? Location { get; set; }

    public ICollection<OpeningInterval> Intervals { get; } =
        new List<OpeningInterval>();

    public OpeningDayMode GetMode(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
        {
            DayOfWeek.Sunday => SundayMode,
            DayOfWeek.Monday => MondayMode,
            DayOfWeek.Tuesday => TuesdayMode,
            DayOfWeek.Wednesday => WednesdayMode,
            DayOfWeek.Thursday => ThursdayMode,
            DayOfWeek.Friday => FridayMode,
            DayOfWeek.Saturday => SaturdayMode,
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek))
        };

    public void SetMode(DayOfWeek dayOfWeek, OpeningDayMode mode)
    {
        switch (dayOfWeek)
        {
            case DayOfWeek.Sunday:
                SundayMode = mode;
                break;
            case DayOfWeek.Monday:
                MondayMode = mode;
                break;
            case DayOfWeek.Tuesday:
                TuesdayMode = mode;
                break;
            case DayOfWeek.Wednesday:
                WednesdayMode = mode;
                break;
            case DayOfWeek.Thursday:
                ThursdayMode = mode;
                break;
            case DayOfWeek.Friday:
                FridayMode = mode;
                break;
            case DayOfWeek.Saturday:
                SaturdayMode = mode;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dayOfWeek));
        }
    }
}

public sealed class OpeningInterval
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid LocationWeeklyOpeningId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    // Null means the end-of-day boundary 24:00, which TimeOnly cannot represent.
    public TimeOnly? EndTime { get; set; }

    public LocationWeeklyOpening? WeeklyOpening { get; set; }
}

public sealed class LocationShiftTemplate
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid LocationId { get; set; }

    public required string Name { get; set; }

    public ShiftTemplateCategory Category { get; set; }

    public int WeekdayMask { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    public StaffingCapability? RequiredCapability { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Location? Location { get; set; }
}

public sealed class CoverageRequirement
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid LocationId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public StaffingCapability RequiredCapability { get; set; }

    public int RequiredCount { get; set; }

    public CoverageSeverity Severity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Location? Location { get; set; }
}
