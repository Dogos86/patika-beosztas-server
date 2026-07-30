// Backend PascalCase enumok. A DTO shapek 1:1 leképezhetők a Swaggerből.
// A frontend enumok külön élnek (types.ts) — a mapperek végzik a fordítást.

export type BackendProfessionalRole =
  | "PharmacyManager"
  | "Pharmacist"
  | "SpecialistAssistant"
  | "Assistant"
  | "PharmacistTrainee"
  | "AssistantTrainee"
  | "Cleaner"
  | "FinanceHelper"
  | "Other";

export type BackendLocationType = "Central" | "Branch";

export type BackendTimeType =
  | "Work"
  | "Overtime"
  | "OnCallDuty"
  | "Standby"
  | "AnnualLeave"
  | "SickLeave"
  | "UnpaidLeave"
  | "ParentalLeave"
  | "Other";

export type BackendPermission =
  | "ViewOwnSchedule"
  | "ManageOwnLeaveRequests"
  | "ManageWorkPreferences"
  | "ManageAllLeaveRequests"
  | "ApproveLeaveRequests"
  | "RecordLeaveForOthers"
  | "ManageEmployees"
  | "ManageLocations"
  | "ManageCoverageRules"
  | "ManageSchedules"
  | "RunAutoFill"
  | "ApproveSchedules"
  | "PublishSchedules"
  | "UseAiAssistant"
  | "ManageUsers"
  | "ManagePayrollOnboarding"
  | "ViewPayrollSensitiveData"
  | "ReviewTaxAllowanceSurvey"
  | "ExportPayrollData";

export type BackendDayOfWeek =
  "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export type BackendStaffingCapability =
  | "Pharmacist"
  | "SpecialistPharmacist"
  | "SpecialistAssistant"
  | "Assistant"
  | "Cleaner"
  | "Finance"
  | "Other";
