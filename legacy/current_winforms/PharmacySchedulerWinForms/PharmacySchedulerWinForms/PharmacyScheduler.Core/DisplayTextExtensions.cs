using System.Globalization;

namespace PharmacyScheduler.Core;

public static class DisplayTextExtensions
{
    public static string ToDisplayText(this EmployeeRole role) => role switch
    {
        EmployeeRole.PharmacyManager => "Gyógyszertárvezető",
        EmployeeRole.Pharmacist => "Gyógyszerész",
        EmployeeRole.DeputyPharmacist => "Helyettes gyógyszerész",
        EmployeeRole.ExpediatingAssistant => "Expediáló szakasszisztens",
        EmployeeRole.Assistant => "Asszisztens",
        EmployeeRole.DeputyAssistant => "Helyettes szakasszisztens",
        EmployeeRole.AssistantIntern => "Asszisztens gyakornok",
        EmployeeRole.SeniorAssistantIntern => "Szakasszisztens gyakornok",
        EmployeeRole.PharmacistIntern => "Gyógyszerész gyakornok",
        EmployeeRole.Cleaner => "Takarító",
        EmployeeRole.FinanceHelper => "Pénzügyi kisegítő",
        EmployeeRole.OtherHelper => "Egyéb kisegítő",
        _ => role.ToString()
    };

    public static string ToDisplayText(this TimeType timeType) => timeType switch
    {
        TimeType.Work => "Munkaidő",
        TimeType.Overtime => "Túlóra",
        TimeType.OnCall => "Ügyelet",
        TimeType.StandBy => "Készenlét",
        TimeType.Vacation => "Szabadság",
        TimeType.SickLeave => "Betegszabadság",
        TimeType.UnpaidLeave => "Fizetetlen szabadság",
        TimeType.MaternityLeave => "Szülési szabadság",
        _ => timeType.ToString()
    };

    public static string ToCode(this TimeType timeType) => timeType switch
    {
        TimeType.Work => "WORK",
        TimeType.Overtime => "OVERTIME",
        TimeType.OnCall => "ONCALL",
        TimeType.StandBy => "STANDBY",
        TimeType.Vacation => "VAC",
        TimeType.SickLeave => "SICK",
        TimeType.UnpaidLeave => "UNPAID",
        TimeType.MaternityLeave => "MAT",
        _ => timeType.ToString().ToUpperInvariant()
    };

    public static string ToDisplayText(this Severity severity) => severity switch
    {
        Severity.Soft => "Figyelmeztetés",
        Severity.Hard => "Blokkoló szabály",
        _ => severity.ToString()
    };

    public static string ToDisplayText(this ScheduleStatus status) => status switch
    {
        ScheduleStatus.Draft => "Piszkozat",
        ScheduleStatus.Approved => "Jóváhagyva",
        _ => status.ToString()
    };

    public static string ToHungarianDayName(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Hétfő",
        DayOfWeek.Tuesday => "Kedd",
        DayOfWeek.Wednesday => "Szerda",
        DayOfWeek.Thursday => "Csütörtök",
        DayOfWeek.Friday => "Péntek",
        DayOfWeek.Saturday => "Szombat",
        DayOfWeek.Sunday => "Vasárnap",
        _ => CultureInfo.GetCultureInfo("hu-HU").DateTimeFormat.GetDayName(day)
    };

    public static bool IsWorkLike(this TimeType timeType) =>
        timeType is TimeType.Work or TimeType.Overtime or TimeType.OnCall or TimeType.StandBy;
}
