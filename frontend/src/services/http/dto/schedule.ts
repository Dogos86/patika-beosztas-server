// Phase 3B — Schedule / ScheduleGeneration wire DTO-k.
// A backend numerikus mezőket `integer | string` alakban is küldhet a wire-en
// (json-schema pattern), ezért UI-oldalon minden szám mezőt `Number(...)`-rel
// olvasunk (mapperek). Enumokat 1:1 tükrözzük a szerződéssel.

import type { BackendStaffingCapability } from "./enums";
import type { BackendLeaveStatus, BackendLeaveType } from "./leave";

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

export type BackendShiftChangeKind = "New" | "Modified" | "Deleted" | "Unchanged";

export type BackendShiftAssignmentSource =
  "Generated" | "Replacement" | "Imported" | "ManualCorrection";

export type BackendScheduleStatus =
  "Generating" | "Draft" | "UnderReview" | "Approved" | "Published" | "Archived";

export type BackendScheduleGenerationStatus =
  "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled";

export type BackendScheduleSolverStatus =
  | "NotStarted"
  | "Optimal"
  | "Feasible"
  | "Infeasible"
  | "Unknown"
  | "ModelInvalid"
  | "Failed"
  | "Cancelled"
  | "HeuristicFallback";

export type BackendPendingLeaveHandling = "IgnorePending" | "TreatAsTemporaryAbsence";

export type BackendCoverageSeverityWire = "Info" | "Warning" | "Blocking";
export type BackendIssueSeverity = "Info" | "Warning" | "Blocking";

export type BackendRegenerationScopeType =
  "FullPeriod" | "Day" | "DateRange" | "Week" | "Location" | "CapabilityAndTimeType" | "Issues";

export type BackendSuggestionExclusionScope = "Run" | "Schedule" | "Period";

type IntWire = number | string;

// ─── Segments / Shifts ─────────────────────────────────────────────

export interface ShiftSegmentResponseDto {
  id: string;
  startTime: string;
  endTime: string;
  timeType: BackendTimeType;
  minutes: IntWire;
}

export interface ShiftAssignmentResponseDto {
  id: string;
  employeeId: string;
  employeeDisplayName: string;
  locationId: string;
  locationName: string;
  date: string;
  startTime: string;
  endTime: string;
  source: BackendShiftAssignmentSource;
  isLocked: boolean;
  generatedByRunId: string | null;
  replacesShiftId: string | null;
  changeKind: BackendShiftChangeKind;
  segments: ShiftSegmentResponseDto[];
  version: IntWire;
}

// ─── Summary / Weights ─────────────────────────────────────────────

export interface ScheduleGenerationSummaryResponseDto {
  blockingCoveragePercent: IntWire;
  blockingIssueCount: IntWire;
  warningIssueCount: IntWire;
  preferenceFulfillmentPercent: IntWire;
  employeesOutsideTargetCount: IntWire;
  pendingLeaveOverlapShiftCount: IntWire;
  multiLocationConflictCount: IntWire;
  newShiftCount: IntWire;
  modifiedShiftCount: IntWire;
  deletedShiftCount: IntWire;
  unchangedShiftCount: IntWire;
  plannedOvertimeMinutes: IntWire;
}

export interface ScheduleGenerationWeightsRequestDto {
  blockingShortage: number | null;
  warningShortage: number | null;
  preferredWindowMatch: number | null;
  avoidWindowViolation: number | null;
  targetHoursDeviation: number | null;
  overtime: number | null;
  weekendFairness: number | null;
  eveningFairness: number | null;
  locationChange: number | null;
  quotaTarget: number | null;
  longShiftPreference: number | null;
  pendingLeaveOverlap: number | null;
  previousScheduleChange: number | null;
  preserveAcceptedDecision: number | null;
}

// ─── Solver ────────────────────────────────────────────────────────

export interface ScheduleSolverStatisticsResponseDto {
  candidateOptionCount: IntWire;
  variableCount: IntWire;
  constraintCount: IntWire;
  wallTimeSeconds: number | string;
  bestObjectiveBound: IntWire | null;
  conflicts: IntWire | null;
  branches: IntWire | null;
}

// ─── Generation Run ────────────────────────────────────────────────

export interface CreateScheduleGenerationRequestDto {
  periodStart: string;
  periodEnd: string;
  deterministicSeed: number | null;
  maxSolveSeconds: number | null;
  workerCount: number | null;
  pendingLeaveHandling?: BackendPendingLeaveHandling;
  weights?: ScheduleGenerationWeightsRequestDto | null;
}

export interface ScheduleGenerationRunResponseDto {
  id: string;
  schedulePlanId: string;
  status: BackendScheduleGenerationStatus;
  solverStatus: BackendScheduleSolverStatus;
  requestedAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  cancellationRequestedAtUtc: string | null;
  algorithmVersion: string;
  deterministicSeed: number | string | null;
  inputSnapshotHash: string;
  objectiveValue: IntWire | null;
  statistics: ScheduleSolverStatisticsResponseDto | null;
  errorCode: string | null;
  redactedError: string | null;
  version: IntWire;
}

export interface CancelScheduleGenerationRequestDto {
  expectedVersion: number;
}

// ─── Schedule List / Plan ──────────────────────────────────────────

export interface ScheduleListItemResponseDto {
  id: string;
  periodStart: string;
  periodEnd: string;
  timeZoneId: string;
  status: BackendScheduleStatus;
  basedOnScheduleId: string | null;
  publishedRevisionNumber: IntWire;
  algorithmVersion: string;
  inputSnapshotHash: string;
  shiftCount: IntWire;
  blockingIssueCount: IntWire;
  warningIssueCount: IntWire;
  version: IntWire;
  updatedAtUtc: string;
}

export interface SchedulePlanResponseDto {
  id: string;
  periodStart: string;
  periodEnd: string;
  timeZoneId: string;
  status: BackendScheduleStatus;
  basedOnScheduleId: string | null;
  publishedRevisionNumber: IntWire;
  algorithmVersion: string;
  inputSnapshotHash: string;
  shifts: ShiftAssignmentResponseDto[];
  summary: ScheduleGenerationSummaryResponseDto;
  version: IntWire;
  createdAtUtc: string;
  updatedAtUtc: string;
  reviewRequestedAtUtc: string | null;
  approvedAtUtc: string | null;
  publishedAtUtc: string | null;
  archivedAtUtc: string | null;
}

export interface CloneScheduleDraftRequestDto {
  expectedVersion: number;
}

// ─── Projections ───────────────────────────────────────────────────

export interface LeaveMarkerResponseDto {
  leaveRequestId: string;
  type: BackendLeaveType;
  status: BackendLeaveStatus;
  isFullDay: boolean;
  startTime: string | null;
  endTime: string | null;
}

export interface EmployeeScheduleDayCellResponseDto {
  date: string;
  shifts: ShiftAssignmentResponseDto[];
  leaveMarkers: LeaveMarkerResponseDto[];
  issueCount: IntWire;
}

export interface EmployeeScheduleRowResponseDto {
  employeeId: string;
  employeeDisplayName: string;
  days: EmployeeScheduleDayCellResponseDto[];
  assignedMinutes: IntWire;
  targetMinutes: IntWire;
  hasWorkProfile: boolean;
  plannedOvertimeMinutes: IntWire;
  weekendShiftCount: IntWire;
  eveningShiftCount: IntWire;
  locationChangeCount: IntWire;
  warningIssueCount: IntWire;
}

export interface EmployeeScheduleMatrixResponseDto {
  scheduleId: string;
  periodStart: string;
  periodEnd: string;
  scheduleVersion: IntWire;
  employees: EmployeeScheduleRowResponseDto[];
}

export interface LocationCoverageSlotResponseDto {
  locationId: string;
  locationName: string;
  date: string;
  startTime: string;
  endTime: string;
  requiredCapability: BackendStaffingCapability;
  timeType: BackendTimeType;
  requiredCount: IntWire;
  actualCount: IntWire;
  shortage: IntWire;
  severity: BackendCoverageSeverityWire;
  status: string;
  employeeIds: string[];
}

export interface LocationCoverageResponseDto {
  scheduleId: string;
  periodStart: string;
  periodEnd: string;
  scheduleVersion: IntWire;
  hasConfiguredRequirements: boolean;
  slots: LocationCoverageSlotResponseDto[];
}

export interface ScheduleGenerationDiagnosticCountsResponseDto {
  activeLocationCount: IntWire;
  openingIntervalCount: IntWire;
  activeShiftTemplateCount: IntWire;
  applicableShiftTemplateCount: IntWire;
  coverageRequirementCount: IntWire;
  activeEmployeeCount: IntWire;
  schedulableEmployeeCount: IntWire;
  autoFillEmployeeCount: IntWire;
  locationAssignedEmployeeCount: IntWire;
  workProfileEmployeeCount: IntWire;
  capableEmployeeCount: IntWire;
  candidateOptionCount: IntWire;
}

export interface ScheduleGenerationPreflightIssueResponseDto {
  code: string;
  severity: BackendIssueSeverity;
  message: string;
  settingsPath: string | null;
}

export interface ScheduleGenerationPreflightResponseDto {
  canStart: boolean;
  counts: ScheduleGenerationDiagnosticCountsResponseDto;
  issues: ScheduleGenerationPreflightIssueResponseDto[];
}

export interface ScheduleIssueResponseDto {
  id: string;
  code: string;
  severity: BackendIssueSeverity;
  employeeId: string | null;
  locationId: string | null;
  shiftAssignmentId: string | null;
  date: string | null;
  startTime: string | null;
  endTime: string | null;
  parametersJson: string;
  isResolved: boolean;
  isAcknowledged: boolean;
  version: IntWire;
}

export interface ScheduleChangeResponseDto {
  changeKind: BackendShiftChangeKind;
  shiftAssignmentId: string | null;
  basedOnShiftId: string | null;
  employeeId: string;
  locationId: string;
  date: string;
  startTime: string;
  endTime: string;
}

// ─── Explanation / Alternatives ────────────────────────────────────

export interface ScheduleAlternativeResponseDto {
  employeeId: string;
  employeeDisplayName: string;
  scoreDifference: IntWire;
  scoreComponents: Record<string, IntWire>;
  tradeoffCodes: string[];
}

export interface ShiftExplanationResponseDto {
  shiftAssignmentId: string;
  generationRunId: string;
  algorithmVersion: string;
  reasonCodes: string[];
  scoreComponents: Record<string, IntWire>;
  alternatives: ScheduleAlternativeResponseDto[];
}

// ─── Corrections / Regeneration ────────────────────────────────────

export interface ShiftVersionRequestDto {
  expectedShiftVersion: number;
  expectedScheduleVersion: number;
  reason?: string | null;
}

export interface ScheduleVersionRequestDto {
  expectedVersion: number;
}

export interface RejectGeneratedSuggestionRequestDto {
  expectedShiftVersion: number;
  expectedScheduleVersion: number;
  reason: string;
  exclusionScope?: BackendSuggestionExclusionScope;
}

export interface ReplaceShiftRequestDto {
  replacementEmployeeId: string;
  expectedShiftVersion: number;
  expectedScheduleVersion: number;
  reason: string;
}

export interface RegenerationScopeRequestDto {
  type: BackendRegenerationScopeType;
  dateFrom: string | null;
  dateTo: string | null;
  locationId: string | null;
  capability: BackendStaffingCapability | null;
  timeType: BackendTimeType | null;
  issueIds: string[] | null;
}

export interface RegenerateScheduleRequestDto {
  scope: RegenerationScopeRequestDto;
  expectedVersion: number;
  deterministicSeed: number | null;
  maxSolveSeconds: number | null;
  workerCount: number | null;
  pendingLeaveHandling?: BackendPendingLeaveHandling;
  weights?: ScheduleGenerationWeightsRequestDto | null;
}

// ─── Own schedule ──────────────────────────────────────────────────

export interface OwnShiftResponseDto {
  id: string;
  locationId: string;
  locationName: string;
  date: string;
  startTime: string;
  endTime: string;
  segments: ShiftSegmentResponseDto[];
}

export interface OwnScheduleResponseDto {
  scheduleId: string;
  periodStart: string;
  periodEnd: string;
  publishedRevisionNumber: IntWire;
  publishedAtUtc: string;
  shifts: OwnShiftResponseDto[];
}
