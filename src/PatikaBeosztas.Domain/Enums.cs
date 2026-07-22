using System.Diagnostics.CodeAnalysis;

namespace PatikaBeosztas.Domain;

public enum ProfessionalRole
{
    PharmacyManager,
    Pharmacist,
    SpecialistAssistant,
    Assistant,
    PharmacistTrainee,
    AssistantTrainee,
    Cleaner,
    FinanceHelper,
    Other
}

public enum LocationType
{
    Central,
    Branch
}

public enum OpeningDayMode
{
    Closed,
    Open24Hours,
    CustomIntervals
}

[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Long is the explicit public shift-template category required by the domain contract.")]
public enum ShiftTemplateCategory
{
    Morning,
    Afternoon,
    Long,
    Custom
}

public enum StaffingCapability
{
    Pharmacist,
    SpecialistPharmacist,
    SpecialistAssistant,
    Assistant,
    Cleaner,
    Finance,
    Other
}

public enum CoverageSeverity
{
    Warning,
    Blocking
}

public enum ShiftQuotaDimension
{
    MorningShift,
    AfternoonShift,
    EveningShift,
    LongShift,
    SaturdayShift,
    SundayShift,
    OnCallDuty,
    Standby
}

public enum QuotaPeriod
{
    Week,
    Month
}

public enum QuotaSeverity
{
    Preferred,
    Required
}

public enum EmployeeTimeWindowType
{
    Preferred,
    Forbidden
}

public enum WorkPreferenceType
{
    Available,
    Preferred,
    Avoid,
    Unavailable,
    Fixed
}

public enum LeaveType
{
    AnnualLeave,
    SickLeave,
    UnpaidLeave,
    ParentalLeave,
    Other
}

public enum LeaveRequestStatus
{
    Draft,
    Pending,
    Approved,
    Rejected,
    Withdrawn,
    Cancelled,
    Reported,
    Recorded,
    Closed
}

public enum LeaveDecision
{
    Approve,
    Reject
}

public enum TimeType
{
    Work,
    Overtime,
    OnCallDuty,
    Standby,
    AnnualLeave,
    SickLeave,
    UnpaidLeave,
    ParentalLeave,
    Other
}

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "A term permission is the explicit public domain and API terminology.")]
public enum ApplicationPermission
{
    ViewOwnSchedule,
    ManageOwnLeaveRequests,
    ManageWorkPreferences,
    ManageAllLeaveRequests,
    ApproveLeaveRequests,
    RecordLeaveForOthers,
    ManageEmployees,
    ManageLocations,
    ManageCoverageRules,
    ManageSchedules,
    RunAutoFill,
    UseAiAssistant,
    ManageUsers
}
