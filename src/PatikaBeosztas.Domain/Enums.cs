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
