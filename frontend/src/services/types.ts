// Domain típusok. Angol azonosítók, magyar címkék a UI-ban.
// Az enum értékek 1:1 mapelhetők a backend PascalCase szerződésére
// (lásd docs/api-integration.md és contracts/).

export type AppPermission =
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

export type ProfessionalRole =
  | "pharmacy_manager"
  | "pharmacist"
  | "specialist_assistant"
  | "assistant"
  | "pharmacist_trainee"
  | "assistant_trainee"
  | "cleaner"
  | "finance_helper"
  | "other";

/** Bejelentkezési fiók (ApplicationUser). Külön a dolgozói szakmai adatoktól.
 *  A backend permission-alapú (nincs AppRole), a UI kizárólag ezekből dönt. */
export interface User {
  id: string;
  organizationId?: string;
  email: string;
  displayName: string;
  active?: boolean;
  permissions: AppPermission[];
  linkedEmployee: LinkedEmployeeInfo | null;
}

/** Snapshot a dolgozói kapcsolatról a session válaszban. */
export interface LinkedEmployeeInfo {
  id: string;
  displayName: string;
  professionalRole: ProfessionalRole;
  active: boolean;
  schedulable: boolean;
}

/** Admin nézethez összegzett felhasználó rekord (lapozott lista elem). */
export interface AdminUserSummary extends User {
  createdAt?: string;
  version: number;
}

/** Lapozott válasz — a Phase 1 backend `PagedResponse<T>` mintáját tükrözi. */
export interface PagedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Location {
  id: string;
  name: string;
  kind: "headquarters" | "branch";
  active: boolean;
  /** Backend cím mező (nullable). */
  address?: string | null;
  /** Optimista konkurencia — API módban mindig jön. */
  version?: number;
  /** Heti nyitvatartás — opcionális, ha nincs kitöltve az UI és a generátor
   *  „24 órás" alapértelmezéssel dolgozik. */
  openingHours?: LocationOpeningHours;
  /** Phase 2B — telephelyhez tartozó műszaksablonok. */
  templates?: ShiftTemplate[];
}

/** Heti nyitvatartás önálló erőforrásként (saját verzióval). */
export interface LocationWeeklyOpening {
  locationId: string;
  hours: LocationOpeningHours;
  warnings: string[];
  version: number;
}

export type WeekdayKey = "mon" | "tue" | "wed" | "thu" | "fri" | "sat" | "sun";

export type OpeningHoursMode = "closed" | "twentyFour" | "custom";

export interface OpeningInterval {
  startMin: number; // 0-1440, helyi idő
  endMin: number; // >startMin, ≤1440
}

export interface OpeningHoursDay {
  mode: OpeningHoursMode;
  intervals: OpeningInterval[]; // custom módnál értékes
}

export type LocationOpeningHours = Record<WeekdayKey, OpeningHoursDay>;

export type ShiftTemplateCategory = "AM" | "PM" | "Long" | "Custom";

export interface ShiftTemplate {
  id: string;
  locationId: string;
  name: string;
  category: ShiftTemplateCategory;
  days: WeekdayKey[];
  startMin: number;
  endMin: number;
  active: boolean;
  requiredCapability?: StaffingCapability;
  /** Saját optimista konkurencia verzió. */
  version?: number;
}

/** Műszaksablon create/update bemenet (id és version nélkül). */
export type ShiftTemplateInput = Omit<ShiftTemplate, "id" | "locationId" | "version">;

// ─── Kompetencia (Phase 2B) ────────────────────────────────────────
// A `StaffingCapability` a lefedettségi szabályok és a beosztás-generátor
// egyetlen forrása: azt írja le, hogy MIT tud a dolgozó, függetlenül a
// szakmai címétől. A régi `ProfessionalRole` HR/címzés célra megmarad.
export type StaffingCapability =
  | "pharmacist"
  | "specialist_pharmacist"
  | "senior_assistant"
  | "assistant"
  | "cleaner"
  | "finance"
  | "other";

// ─── Munkaidőprofil / elérhetőség / szabályok / kvóták ─────────────

export interface WorkTimeProfile {
  /** Szerződéses havi idő percben. */
  contractedMonthlyMinutes: number;
  /** Opcionális heti idő percben. */
  contractedWeeklyMinutes?: number;
  standardShiftMinutes: number;
  minShiftMinutes: number;
  maxNormalShiftMinutes: number;
  maxDailyTotalMinutes: number;
  longShiftAllowed: boolean;
  longShiftMaxMinutes?: number;
  fullDayAllowed: boolean;
  overtimeAllowed: boolean;
  overtimeMonthlyMaxMinutes?: number;
  autoAssign: boolean;
}

export interface AvailabilityProfile {
  onCallAllowed: boolean;
  onCallMonthlyMax?: number;
  standbyAllowed: boolean;
  standbyMonthlyMax?: number;
  saturdayAllowed: boolean;
  saturdayMonthlyMax?: number;
  sundayAllowed: boolean;
  sundayMonthlyMax?: number;
}

export type RecurringRuleKind =
  "Available" | "Preferred" | "Avoid" | "Unavailable" | "FixedTemplate";

export interface RecurringWorkRule {
  id: string;
  kind: RecurringRuleKind;
  fromDate?: string;
  toDate?: string;
  weekday: Weekday;
  fullDay: boolean;
  startMin?: number;
  endMin?: number;
  locationId?: string;
  note?: string;
  active: boolean;
}

export type QuotaDimension = "AM" | "PM" | "Sat" | "Sun" | "OnCall" | "Standby" | "Long";
export type QuotaPeriod = "week" | "month";

export interface ShiftQuota {
  id: string;
  dimension: QuotaDimension;
  period: QuotaPeriod;
  min?: number;
  target?: number;
  max?: number;
  mandatory: boolean;
}

// ─── Phase 2E — API-alapú planning shape-ek ────────────────────────
// A backend a Kompetenciák, Munkaidőprofil és Kvóta-szabályok területet
// saját endpointokkal kezeli, függetlenül az EmployeeResponse-tól. Ezek
// a shape-ek 1:1 tükrözik a szerződést; a UI PascalCase enumokat használ,
// hogy a leképezés csak a DTO határon történjen.

export interface EmployeeCapabilitiesData {
  employeeId: string;
  assignedCapabilities: StaffingCapability[];
  effectiveCapabilities: StaffingCapability[];
  countsAsPharmacistCompatibility: boolean;
  employeeVersion: number;
}

export interface EmployeeWorkProfile {
  /** null, ha még nincs perzisztált profil (404 → új profil űrlap). */
  id: string | null;
  version: number | null;
  contractedMonthlyMinutes: number;
  contractedWeeklyMinutes: number | null;
  standardShiftMinutes: number;
  minimumShiftMinutes: number;
  maximumRegularShiftMinutes: number;
  maximumDailyMinutes: number;
  allowsLongShift: boolean;
  maximumLongShiftMinutes: number | null;
  allowsFullOpeningHoursShift: boolean;
  allowsOvertime: boolean;
  maximumOvertimeMinutesPerMonth: number | null;
  allowsOnCallDuty: boolean;
  maximumOnCallAssignmentsPerMonth: number | null;
  allowsStandby: boolean;
  maximumStandbyAssignmentsPerMonth: number | null;
  allowsSaturday: boolean;
  maximumSaturdaysPerMonth: number | null;
  allowsSunday: boolean;
  maximumSundaysPerMonth: number | null;
  includeInAutoFill: boolean;
}

export type ApiShiftQuotaDimension =
  | "MorningShift"
  | "AfternoonShift"
  | "EveningShift"
  | "LongShift"
  | "SaturdayShift"
  | "SundayShift"
  | "OnCallDuty"
  | "Standby";
export type ApiQuotaPeriod = "Week" | "Month";
export type ApiQuotaSeverity = "Preferred" | "Required";

export interface EmployeeShiftQuotaRule {
  id: string;
  employeeId: string;
  dimension: ApiShiftQuotaDimension;
  period: ApiQuotaPeriod;
  minimum: number;
  target: number;
  maximum: number;
  severity: ApiQuotaSeverity;
  isActive: boolean;
  version: number;
}

export interface CreateShiftQuotaRuleInput {
  dimension: ApiShiftQuotaDimension;
  period: ApiQuotaPeriod;
  minimum: number;
  target: number;
  maximum: number;
  severity: ApiQuotaSeverity;
  isActive: boolean;
}

export interface UpdateShiftQuotaRuleInput extends CreateShiftQuotaRuleInput {
  expectedVersion: number;
}

export interface TimeRange {
  start: string; // "HH:mm"
  end: string; // "HH:mm"
}

/** Gyógyszertári dolgozó — kizárólag szakmai és beosztási adatok.
 *  A felhasználói fiók (User) külön entitás, opcionálisan kapcsolódik. */
export interface Employee {
  id: string;
  fullName: string;
  displayName: string;
  professionalRole: ProfessionalRole;
  active: boolean;
  schedulable: boolean;
  includeInAutoFill: boolean;
  countsAsPharmacist: boolean;
  locationIds: string[];
  /** Havi munkaidőcél órában (UI egység); backend percben tárolja. 0 = nincs limit. */
  monthlyHoursTarget: number;
  maxDailyMinutes: number;
  /** Opcionális törzsadatok (Phase 2B). */
  birthDate?: string | null;
  externalPayrollId?: string | null;
  /** Telephely–enabled párok (a `locationIds` a hátrafelé kompatibilitás miatt marad; ez pontosabb). */
  locationAssignments?: { locationId: string; enabled: boolean; locationName?: string }[];
  /** A backend által visszaadott figyelmeztetések (pl. „adatprobléma"). */
  warnings?: string[];
  /** Ha van kapcsolt belépési fiók (EmployeeResponse.linkedUser). */
  linkedUser?: {
    userId: string;
    email: string;
    displayName: string;
    active: boolean;
  } | null;
  allowedShiftTypes: ShiftType[];
  preferredWindows: PreferenceWindow[];
  blockedWindows: PreferenceWindow[];
  /** Phase 2B — kompetenciák. Ha üres, a `professionalRole`-ból
   *  automatikusan levezethető (lásd capability-map). */
  capabilities?: StaffingCapability[];
  workProfile?: WorkTimeProfile;
  availability?: AvailabilityProfile;
  recurringRules?: RecurringWorkRule[];
  quotas?: ShiftQuota[];
}

export type Weekday = "every" | "mon" | "tue" | "wed" | "thu" | "fri" | "sat" | "sun";

export interface PreferenceWindow {
  weekday: Weekday;
  start: string; // HH:mm
  end: string; // HH:mm
  kind: "preferred" | "blocked";
}

export type ShiftType = "work" | "on_call" | "training" | "meeting";

/** Belső időszegmens típusa egy műszakon belül (spec „Munkaidő" lista). */
export type TimeType =
  | "work"
  | "overtime"
  | "on_call"
  | "standby"
  | "vacation"
  | "sick"
  | "unpaid"
  | "parental"
  | "other";

export interface ShiftSegment {
  type: TimeType;
  startMin: number;
  endMin: number;
}

export type ScheduleStatus = "draft" | "approved" | "archived";

export interface Shift {
  id: string;
  employeeId: string;
  locationId: string;
  date: string; // helyi naptári dátum "YYYY-MM-DD"
  start: string; // HH:mm
  end: string; // HH:mm
  type: ShiftType;
  changed?: boolean;
  status: "draft" | "published"; // egyedi műszak státusz
  /** Admin által lakatolt — újrageneráláskor megmarad. */
  locked?: boolean;
  /** Magyarázat: „miért ezt választotta a generátor". */
  explanation?: ShiftExplanation;
  /** Melyik generálási futáshoz tartozik (mock). */
  runId?: string;
  /** Phase 2B — egy nap-egy műszak, több szegmenssel (Work/Overtime/OnCall/Standby). */
  segments?: ShiftSegment[];
}

export interface ShiftExplanation {
  reasons: string[];
  alternatives: ShiftAlternative[];
}

export interface ShiftAlternative {
  employeeId: string;
  tradeoffs: string[];
}

// ─── Beosztás-generáló munkatér ────────────────────────────────────

export type PeriodKind = "week" | "biweek" | "month";

export type WorkspaceView = "employee" | "coverage" | "issues";

/** A teljes beosztás életciklusa a spec 10. pontja szerint. */
export type ScheduleRunStatus =
  "Generating" | "Draft" | "UnderReview" | "Approved" | "Published" | "Archived";

export type IssueSeverity = "blocking" | "warning" | "info";

export type IssueKind =
  | "missing_pharmacist"
  | "missing_specialist_assistant"
  | "missing_assistant"
  | "multi_location_conflict"
  | "leave_conflict"
  | "daily_cap_exceeded"
  | "monthly_cap_exceeded"
  | "blocked_window_violation"
  | "preference_missed"
  | "pending_request_overlap"
  | "inactive_location_used"
  | "other";

export interface ScheduleIssue {
  id: string;
  kind: IssueKind;
  severity: IssueSeverity;
  message: string;
  date?: string;
  locationId?: string;
  employeeId?: string;
  shiftId?: string;
  professionalRole?: ProfessionalRole;
}

export interface ScheduleRunSummary {
  coveragePct: number;
  blocking: number;
  warnings: number;
  requestFulfillmentPct: number;
  employeesOverTarget: number;
  pendingRequestOverlaps: number;
  multiLocationConflicts: number;
  added: number;
  modified: number;
  removed: number;
}

export interface CoverageCell {
  date: string;
  locationId: string;
  status: "ok" | "warning" | "blocking" | "closed" | "inactive";
  required: number;
  actual: number;
  details: { role: ProfessionalRole; required: number; actual: number }[];
}

export interface ScheduleRun {
  id: string;
  from: string;
  to: string;
  status: ScheduleRunStatus;
  generatedAt: string;
  locationIds: string[];
  summary: ScheduleRunSummary;
  shifts: Shift[];
  issues: ScheduleIssue[];
  coverage: CoverageCell[];
  /** Snapshot az utolsó közzétett beosztásról az összehasonlításhoz. */
  previousShifts: Shift[];
}

export interface GenerateRunInput {
  from: string;
  to: string;
  locationIds?: string[]; // üres/undefined → minden aktív telephely
  keepLocked?: boolean;
  previousRunId?: string;
}

export interface RegenerateScope {
  date?: string;
  weekStart?: string;
  locationId?: string;
  professionalRole?: ProfessionalRole;
  issueIds?: string[];
}

export interface Schedule {
  id: string;
  locationId: string;
  weekStart: string;
  status: ScheduleStatus;
}

export type LeaveType = "annual_leave" | "sick_leave" | "unpaid_leave" | "parental_leave" | "other";

/** Kérelem-életciklus. UI kizárólag a folyamathoz értelmes státuszokat jeleníti meg. */
export type LeaveStatus =
  | "draft"
  | "pending"
  | "approved"
  | "rejected"
  | "withdrawn" // dolgozó visszavonta
  | "cancelled" // admin lezárta jóváhagyás után
  | "reported" // betegállomány bejelentés
  | "recorded"
  | "closed";

export interface LeaveRequest {
  id: string;
  employeeId: string;
  type: LeaveType;
  fullDay: boolean;
  startDate: string;
  endDate: string;
  startTime?: string;
  endTime?: string;
  note?: string;
  status: LeaveStatus;
  createdAt: string;
  createdByUserId: string;
  decisionNote?: string;
  history: LeaveHistoryEntry[];
  /** Optimista konkurencia — backend `LeaveRequestResponse.version`. */
  version?: number;
}

export interface LeaveHistoryEntry {
  at: string;
  actorUserId: string;
  action: "created" | "approved" | "rejected" | "withdrawn" | "cancelled" | "reported";
  note?: string;
}

export interface CoverageRule {
  id: string;
  locationId: string;
  weekday: number; // 0-6 (0=hétfő)
  range: TimeRange;
  requiredCount: number;
  severity: "warning" | "blocking";
  /** Kompetenciára szűkített szabály (Phase 2B). */
  capability: StaffingCapability;
  active: boolean;
  /** Legacy — mock migráció után nem használt; opcionálisan megőrizve az
   *  átmenet idejére. */
  requiredProfessionalRole?: ProfessionalRole;
}

export interface Notification {
  id: string;
  kind: "shift_changed" | "request_approved" | "request_rejected" | "approval_pending";
  title: string;
  body: string;
  createdAt: string;
  read: boolean;
  targetUserId: string;
}

export interface AiActionPreview {
  id: string;
  kind: "leave_request" | "shift_swap" | "shift_add";
  summary: string;
  details: string[];
  warnings: string[];
}

/** AI művelet-előnézet a végleges UX szerződés szerint. */
export interface AiCommandPreview {
  previewId: string;
  summary: string;
  transcript: string;
  resolvedActions: AiResolvedAction[];
  clarifications: AiClarification[];
  warnings: string[];
  canExecute: boolean;
  expiresAt: string;
  confirmationToken: string;
}

export interface AiResolvedAction {
  kind: "leave_request" | "shift_swap" | "shift_add";
  summary: string;
  details: string[];
}

export interface AiClarification {
  id: string;
  question: string;
  answered: boolean;
}

// ─── Phase 2D — HR / bérszámfejtés ─────────────────────────────────
// A payroll domain-t közvetlenül a DTO alakok tükrözik (backend PascalCase
// enumok), mert a HR/adóügyi terület jogilag kötött szótárral dolgozik és
// a fordítás csak felesleges hibalehetőséget hozna. A címkéket a
// `payroll-labels.ts` állítja elő.
export type {
  PayrollProfileStatus,
  SurveyStatus,
  SurveyAnswer,
  MonthlyAllowancePreference,
  MaritalStatus,
  FamilyAllowanceClaimMode,
  MotherAllowanceQualifyingChildrenCount,
  Under25AllowanceOptOut,
  ForeignTaxResidencyOrSimilarForeignBenefit,
  DeclarationRequirementStatus,
  DeclarationType,
  TaxAllowanceSurveyAnswersDto as TaxAllowanceSurveyAnswers,
  EmployeePayrollProfileResponseDto as EmployeePayrollProfile,
  TaxDeclarationRequirementResponseDto as TaxDeclarationRequirement,
  TaxAllowanceSurveyResponseDto as TaxAllowanceSurvey,
  PayrollOnboardingSummaryResponseDto as PayrollOnboardingSummary,
} from "./http/dto/payroll";

// ─── Phase 3B — Schedule domain ────────────────────────────────────
// Az új Schedule domain a Phase 3A backend szerződéssel egyezik: minden
// state-változtató hívás `expectedVersion`-t igényel; a workflow lépések
// (Draft→UnderReview→Approved→Published→Archived) explicit endpointokkal
// történnek. A régi `Shift`/`ScheduleRun` mock-orientált shape-ek megmaradnak.

export type ShiftChangeKind = "New" | "Modified" | "Deleted" | "Unchanged";
export type ShiftAssignmentSource = "Generated" | "Replacement" | "Imported" | "ManualCorrection";
export type ScheduleGenerationStatus = "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled";
export type ScheduleSolverStatusEnum =
  | "NotStarted"
  | "Optimal"
  | "Feasible"
  | "Infeasible"
  | "Unknown"
  | "ModelInvalid"
  | "Failed"
  | "Cancelled"
  | "HeuristicFallback";
export type PendingLeaveHandling = "IgnorePending" | "TreatAsTemporaryAbsence";

export interface AssignmentSegment {
  id: string;
  startTime: string;
  endTime: string;
  timeType: TimeType;
  minutes: number;
}

export interface ShiftAssignment {
  id: string;
  employeeId: string;
  employeeDisplayName: string;
  locationId: string;
  locationName: string;
  date: string;
  startTime: string;
  endTime: string;
  source: ShiftAssignmentSource;
  isLocked: boolean;
  generatedByRunId: string | null;
  replacesShiftId: string | null;
  changeKind: ShiftChangeKind;
  segments: AssignmentSegment[];
  version: number;
}

export interface ScheduleGenerationSummary {
  blockingCoveragePercent: number;
  blockingIssueCount: number;
  warningIssueCount: number;
  preferenceFulfillmentPercent: number;
  employeesOutsideTargetCount: number;
  pendingLeaveOverlapShiftCount: number;
  multiLocationConflictCount: number;
  newShiftCount: number;
  modifiedShiftCount: number;
  deletedShiftCount: number;
  unchangedShiftCount: number;
  plannedOvertimeMinutes: number;
}

export interface ScheduleSolverStatistics {
  candidateOptionCount: number;
  variableCount: number;
  constraintCount: number;
  wallTimeSeconds: number;
  bestObjectiveBound: number | null;
  conflicts: number | null;
  branches: number | null;
}

export interface ScheduleGenerationRun {
  id: string;
  schedulePlanId: string;
  status: ScheduleGenerationStatus;
  solverStatus: ScheduleSolverStatusEnum;
  requestedAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  cancellationRequestedAtUtc: string | null;
  algorithmVersion: string;
  deterministicSeed: number | null;
  inputSnapshotHash: string;
  objectiveValue: number | null;
  statistics: ScheduleSolverStatistics;
  errorCode: string | null;
  redactedError: string | null;
  version: number;
}

export interface ScheduleListItem {
  id: string;
  periodStart: string;
  periodEnd: string;
  timeZoneId: string;
  status: ScheduleRunStatus;
  basedOnScheduleId: string | null;
  publishedRevisionNumber: number;
  algorithmVersion: string;
  inputSnapshotHash: string;
  shiftCount: number;
  blockingIssueCount: number;
  warningIssueCount: number;
  version: number;
  updatedAtUtc: string;
}

export interface SchedulePlan {
  id: string;
  periodStart: string;
  periodEnd: string;
  timeZoneId: string;
  status: ScheduleRunStatus;
  basedOnScheduleId: string | null;
  publishedRevisionNumber: number;
  algorithmVersion: string;
  inputSnapshotHash: string;
  shifts: ShiftAssignment[];
  summary: ScheduleGenerationSummary;
  version: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  reviewRequestedAtUtc: string | null;
  approvedAtUtc: string | null;
  publishedAtUtc: string | null;
  archivedAtUtc: string | null;
}

export interface LeaveMarker {
  leaveRequestId: string;
  type: import("./http/dto/leave").BackendLeaveType;
  status: import("./http/dto/leave").BackendLeaveStatus;
  isFullDay: boolean;
  startTime: string;
  endTime: string;
}

export interface EmployeeScheduleDayCell {
  date: string;
  shifts: ShiftAssignment[];
  leaveMarkers: LeaveMarker[];
  issueCount: number;
}

export interface EmployeeScheduleRow {
  employeeId: string;
  employeeDisplayName: string;
  days: EmployeeScheduleDayCell[];
  assignedMinutes: number;
  targetMinutes: number;
  plannedOvertimeMinutes: number;
  weekendShiftCount: number;
  eveningShiftCount: number;
  locationChangeCount: number;
  warningIssueCount: number;
}

export interface EmployeeScheduleMatrix {
  scheduleId: string;
  periodStart: string;
  periodEnd: string;
  scheduleVersion: number;
  employees: EmployeeScheduleRow[];
}

export interface LocationCoverageSlot {
  locationId: string;
  locationName: string;
  date: string;
  startTime: string;
  endTime: string;
  requiredCapability: StaffingCapability;
  timeType: TimeType;
  requiredCount: number;
  actualCount: number;
  shortage: number;
  severity: IssueSeverity;
  status: string;
  employeeIds: string[];
}

export interface LocationCoverage {
  scheduleId: string;
  periodStart: string;
  periodEnd: string;
  scheduleVersion: number;
  slots: LocationCoverageSlot[];
}

export interface ScheduleIssueRow {
  id: string;
  code: string;
  severity: IssueSeverity;
  employeeId: string | null;
  locationId: string | null;
  shiftAssignmentId: string | null;
  date: string | null;
  startTime: string;
  endTime: string;
  parameters: Record<string, unknown>;
  isResolved: boolean;
  isAcknowledged: boolean;
  version: number;
}

export interface ScheduleChange {
  changeKind: ShiftChangeKind;
  shiftAssignmentId: string | null;
  basedOnShiftId: string | null;
  employeeId: string;
  locationId: string;
  date: string;
  startTime: string;
  endTime: string;
}

export interface ScheduleAlternative {
  employeeId: string;
  employeeDisplayName: string;
  scoreDifference: number;
  scoreComponents: Record<string, number>;
  tradeoffCodes: string[];
}

export interface ShiftAssignmentExplanation {
  shiftAssignmentId: string;
  generationRunId: string;
  algorithmVersion: string;
  reasonCodes: string[];
  scoreComponents: Record<string, number>;
  alternatives: ScheduleAlternative[];
}

export type RegenerationScopeType =
  "full" | "day" | "range" | "week" | "location" | "capability_time" | "issues";

export interface RegenerationScopeInput {
  type: RegenerationScopeType;
  dateFrom?: string;
  dateTo?: string;
  locationId?: string;
  capability?: StaffingCapability;
  timeType?: TimeType;
  issueIds?: string[];
}

// ─── Munkavégzési kérések és visszatérő szabályok (Phase 2E.6) ─────
// Backend: /api/me/work-preferences és /api/admin/employees/{id}/work-preferences.

export type WorkPreferenceType = "Available" | "Preferred" | "Avoid" | "Unavailable" | "Fixed";

export interface WorkPreference {
  id: string;
  employeeId: string;
  employeeDisplayName: string;
  type: WorkPreferenceType;
  /** ISO dátum (YYYY-MM-DD). */
  dateFrom: string;
  dateTo: string;
  /** null → a tartomány minden napja; egyébként visszatérő heti nap. */
  weekday: WeekdayKey | null;
  isFullDay: boolean;
  /** "HH:mm" — csak ha nem egész napos. */
  startTime: string | null;
  endTime: string | null;
  locationId: string | null;
  locationName: string | null;
  note: string | null;
  isActive: boolean;
  version: number;
}

/** Létrehozás/módosítás bemenete. Self-service esetben NINCS employeeId —
 *  az identitást a session dönti el. */
export interface WorkPreferenceInput {
  type: WorkPreferenceType;
  dateFrom: string;
  dateTo: string;
  weekday: WeekdayKey | null;
  isFullDay: boolean;
  startTime: string | null;
  endTime: string | null;
  locationId: string | null;
  note: string | null;
}
