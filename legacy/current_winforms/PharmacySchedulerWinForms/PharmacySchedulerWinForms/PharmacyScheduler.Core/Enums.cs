namespace PharmacyScheduler.Core;

public enum EmployeeRole
{
    PharmacyManager = 0,
    Pharmacist = 1,
    DeputyPharmacist = 2,
    ExpediatingAssistant = 3,
    Assistant = 4,
    DeputyAssistant = 5,
    AssistantIntern = 6,
    SeniorAssistantIntern = 7,
    PharmacistIntern = 8,
    Cleaner = 9,
    FinanceHelper = 10,
    OtherHelper = 11
}

public enum TimeType
{
    Work = 0,
    Overtime = 1,
    OnCall = 2,
    StandBy = 3,
    Vacation = 4,
    SickLeave = 5,
    UnpaidLeave = 6,
    MaternityLeave = 7
}

public enum Severity
{
    Soft = 0,
    Hard = 1
}

public enum ScheduleStatus
{
    Draft = 0,
    Approved = 1
}
