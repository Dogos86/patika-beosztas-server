namespace PharmacyScheduler.Core.Models;

public sealed class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; } = new DateTime(1990, 1, 1);
    public EmployeeRole Role { get; set; } = EmployeeRole.Pharmacist;
    public decimal MonthlyHoursLimit { get; set; } = 168m;
    public decimal MaxDailyHours { get; set; } = 8m;
    public string PreferredWindows { get; set; } = string.Empty;
    public string ForbiddenWindows { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<TimeType> AllowedTimeTypes { get; set; } = Enum.GetValues<TimeType>().ToList();
    public List<Guid> AllowedLocationIds { get; set; } = new();

    /// <summary>
    /// Ha true, az automatikus beosztásgenerálás figyelembe veszi ezt a dolgozót.
    /// Gyógyszertárvezetõnél állítható, hogy részt vegyen-e a generálásban.
    /// </summary>
    public bool IncludeInAutoSchedule { get; set; } = true;

    /// <summary>
    /// Ha a gyógyszertárvezetõ beosztható, milyen szerepkörben vegyen részt a generálásban.
    /// null = saját szerepkör, egyébként a megadott szerepkör (pl. Gyógyszerész).
    /// </summary>
    public EmployeeRole? AutoScheduleRoleOverride { get; set; }

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? FullName : DisplayName;
}
