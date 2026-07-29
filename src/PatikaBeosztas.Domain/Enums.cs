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

public enum ScheduleStatus
{
    Generating,
    Draft,
    UnderReview,
    Approved,
    Published,
    Archived
}

public enum ScheduleGenerationStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum ScheduleSolverStatus
{
    NotStarted,
    Optimal,
    Feasible,
    Infeasible,
    Unknown,
    ModelInvalid,
    Failed,
    Cancelled,
    HeuristicFallback
}

public enum ShiftAssignmentSource
{
    Generated,
    Replacement,
    Imported,
    ManualCorrection
}

public enum ShiftChangeKind
{
    New,
    Modified,
    Deleted,
    Unchanged
}

public enum ScheduleIssueSeverity
{
    Info,
    Warning,
    Blocking
}

public enum GeneratedSuggestionDecisionType
{
    Accept,
    Reject,
    Lock,
    Unlock,
    Replace
}

public enum SuggestionExclusionScope
{
    Run,
    Schedule,
    Period
}

public enum PendingLeaveHandlingMode
{
    IgnorePending,
    TreatAsTemporaryAbsence
}

public enum RegenerationScopeType
{
    FullPeriod,
    Day,
    DateRange,
    Week,
    Location,
    CapabilityAndTimeType,
    Issues
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
    ManageUsers,
    ManagePayrollOnboarding,
    ViewPayrollSensitiveData,
    ReviewTaxAllowanceSurvey,
    ExportPayrollData,
    ApproveSchedules,
    PublishSchedules
}

public enum EmployeePayrollProfileStatus
{
    Draft,
    UnderReview,
    Complete,
    Archived
}

public enum TaxAllowanceSurveyStatus
{
    Draft,
    Submitted,
    NeedsClarification,
    Reviewed,
    Completed,
    Cancelled
}

public enum MonthlyAllowancePreference
{
    ApplyMonthly,
    AnnualReturnOnly,
    NeedsConsultation
}

[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Single is the explicit public marital-status value required by the survey contract.")]
public enum MaritalStatus
{
    Single,
    Married,
    Partnership,
    Divorced,
    Widowed,
    Other
}

public enum SurveyAnswer
{
    Yes,
    No,
    Unknown
}

public enum MotherAllowanceQualifyingChildrenCount
{
    None,
    One,
    Two,
    Three,
    FourPlus,
    Unknown
}

public enum FamilyAllowanceClaimMode
{
    NotRequested,
    Alone,
    Shared,
    Undecided
}

public enum Under25AllowanceOptOut
{
    No,
    Yes,
    NeedsConsultation
}

public enum ForeignTaxResidencyOrSimilarForeignBenefit
{
    None,
    PresentNeedsConsultation
}

public enum TaxDeclarationType
{
    Under25OptOut,
    Under30Mother,
    Anyacska,
    MultiChildMotherAllowance,
    FamilyAllowance,
    FirstMarriage,
    PersonalAllowance
}

public enum TaxDeclarationRequirementStatus
{
    NotRequired,
    Required,
    ToSend,
    Sent,
    ReceivedOnya,
    ReceivedPaper,
    Verified,
    Applied,
    Rejected,
    Expired
}
