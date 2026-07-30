// Phase 3B — Schedule mapperek. A backend `integer | string` mezőit
// `Number(...)`-rel olvassuk, az időmezőket "HH:mm"-re rövidítjük.
// Enumokat exhaustívan mapelünk; ismeretlen érték → dobás.

import type {
  EmployeeScheduleDayCellResponseDto,
  EmployeeScheduleMatrixResponseDto,
  EmployeeScheduleRowResponseDto,
  LeaveMarkerResponseDto,
  LocationCoverageResponseDto,
  LocationCoverageSlotResponseDto,
  OwnScheduleResponseDto,
  OwnShiftResponseDto,
  ScheduleAlternativeResponseDto,
  ScheduleChangeResponseDto,
  ScheduleGenerationRunResponseDto,
  ScheduleGenerationSummaryResponseDto,
  ScheduleIssueResponseDto,
  ScheduleListItemResponseDto,
  SchedulePlanResponseDto,
  ScheduleSolverStatisticsResponseDto,
  ShiftAssignmentResponseDto,
  ShiftExplanationResponseDto,
  ShiftSegmentResponseDto,
  BackendRegenerationScopeType,
  RegenerationScopeRequestDto,
  BackendTimeType,
} from "../dto/schedule";
import type { BackendStaffingCapability } from "../dto/enums";
import { mapCapabilityToBackend, mapCapabilityFromBackend } from "./coverage";
import type {
  ShiftAssignment,
  AssignmentSegment,
  ShiftAssignmentExplanation,
  ScheduleAlternative,
  ScheduleGenerationRun,
  ScheduleGenerationSummary,
  ScheduleListItem,
  SchedulePlan,
  EmployeeScheduleMatrix,
  EmployeeScheduleRow,
  EmployeeScheduleDayCell,
  LeaveMarker,
  LocationCoverage,
  LocationCoverageSlot,
  ScheduleIssueRow,
  ScheduleChange,
  ScheduleSolverStatistics,
  RegenerationScopeInput,
  TimeType as ScheduleTimeType,
} from "@/services/types";

function hhmm(t: string | null | undefined): string {
  if (!t) return "";
  return t.length >= 5 ? t.slice(0, 5) : t;
}

function num(v: number | string | null | undefined, fallback = 0): number {
  if (v === null || v === undefined || v === "") return fallback;
  const n = typeof v === "number" ? v : Number(v);
  return Number.isFinite(n) ? n : fallback;
}

const TIME_TYPE_FROM: Record<BackendTimeType, ScheduleTimeType> = {
  Work: "work",
  Overtime: "overtime",
  OnCallDuty: "on_call",
  Standby: "standby",
  AnnualLeave: "vacation",
  SickLeave: "sick",
  UnpaidLeave: "unpaid",
  ParentalLeave: "parental",
  Other: "other",
};
const TIME_TYPE_TO = Object.fromEntries(
  Object.entries(TIME_TYPE_FROM).map(([k, v]) => [v, k]),
) as Record<ScheduleTimeType, BackendTimeType>;

export function mapTimeTypeFromBackend(v: BackendTimeType): ScheduleTimeType {
  const out = TIME_TYPE_FROM[v];
  if (!out) throw new Error(`Ismeretlen TimeType: ${v}`);
  return out;
}

export function mapTimeTypeToBackend(v: ScheduleTimeType): BackendTimeType {
  return TIME_TYPE_TO[v];
}

function mapSegment(dto: ShiftSegmentResponseDto): AssignmentSegment {
  return {
    id: dto.id,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
    timeType: mapTimeTypeFromBackend(dto.timeType),
    minutes: num(dto.minutes),
  };
}

export function mapShiftAssignmentFromBackend(dto: ShiftAssignmentResponseDto): ShiftAssignment {
  return {
    id: dto.id,
    employeeId: dto.employeeId,
    employeeDisplayName: dto.employeeDisplayName,
    locationId: dto.locationId,
    locationName: dto.locationName,
    date: dto.date,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
    source: dto.source,
    isLocked: dto.isLocked,
    generatedByRunId: dto.generatedByRunId,
    replacesShiftId: dto.replacesShiftId,
    changeKind: dto.changeKind,
    segments: (dto.segments ?? []).map(mapSegment),
    version: num(dto.version),
  };
}

function mapSummary(dto: ScheduleGenerationSummaryResponseDto): ScheduleGenerationSummary {
  return {
    blockingCoveragePercent: num(dto.blockingCoveragePercent),
    blockingIssueCount: num(dto.blockingIssueCount),
    warningIssueCount: num(dto.warningIssueCount),
    preferenceFulfillmentPercent: num(dto.preferenceFulfillmentPercent),
    employeesOutsideTargetCount: num(dto.employeesOutsideTargetCount),
    pendingLeaveOverlapShiftCount: num(dto.pendingLeaveOverlapShiftCount),
    multiLocationConflictCount: num(dto.multiLocationConflictCount),
    newShiftCount: num(dto.newShiftCount),
    modifiedShiftCount: num(dto.modifiedShiftCount),
    deletedShiftCount: num(dto.deletedShiftCount),
    unchangedShiftCount: num(dto.unchangedShiftCount),
    plannedOvertimeMinutes: num(dto.plannedOvertimeMinutes),
  };
}

function mapStats(dto: ScheduleSolverStatisticsResponseDto): ScheduleSolverStatistics {
  return {
    candidateOptionCount: num(dto.candidateOptionCount),
    variableCount: num(dto.variableCount),
    constraintCount: num(dto.constraintCount),
    wallTimeSeconds: num(dto.wallTimeSeconds),
    bestObjectiveBound: dto.bestObjectiveBound == null ? null : num(dto.bestObjectiveBound),
    conflicts: dto.conflicts == null ? null : num(dto.conflicts),
    branches: dto.branches == null ? null : num(dto.branches),
  };
}

export function mapGenerationRunFromBackend(
  dto: ScheduleGenerationRunResponseDto,
): ScheduleGenerationRun {
  return {
    id: dto.id,
    schedulePlanId: dto.schedulePlanId,
    status: dto.status,
    solverStatus: dto.solverStatus,
    requestedAtUtc: dto.requestedAtUtc,
    startedAtUtc: dto.startedAtUtc,
    completedAtUtc: dto.completedAtUtc,
    cancellationRequestedAtUtc: dto.cancellationRequestedAtUtc,
    algorithmVersion: dto.algorithmVersion,
    deterministicSeed: dto.deterministicSeed == null ? null : num(dto.deterministicSeed),
    inputSnapshotHash: dto.inputSnapshotHash,
    objectiveValue: dto.objectiveValue == null ? null : num(dto.objectiveValue),
    statistics: mapStats(dto.statistics),
    errorCode: dto.errorCode,
    redactedError: dto.redactedError,
    version: num(dto.version),
  };
}

export function mapScheduleListItemFromBackend(dto: ScheduleListItemResponseDto): ScheduleListItem {
  return {
    id: dto.id,
    periodStart: dto.periodStart,
    periodEnd: dto.periodEnd,
    timeZoneId: dto.timeZoneId,
    status: dto.status,
    basedOnScheduleId: dto.basedOnScheduleId,
    publishedRevisionNumber: num(dto.publishedRevisionNumber),
    algorithmVersion: dto.algorithmVersion,
    inputSnapshotHash: dto.inputSnapshotHash,
    shiftCount: num(dto.shiftCount),
    blockingIssueCount: num(dto.blockingIssueCount),
    warningIssueCount: num(dto.warningIssueCount),
    version: num(dto.version),
    updatedAtUtc: dto.updatedAtUtc,
  };
}

export function mapSchedulePlanFromBackend(dto: SchedulePlanResponseDto): SchedulePlan {
  return {
    id: dto.id,
    periodStart: dto.periodStart,
    periodEnd: dto.periodEnd,
    timeZoneId: dto.timeZoneId,
    status: dto.status,
    basedOnScheduleId: dto.basedOnScheduleId,
    publishedRevisionNumber: num(dto.publishedRevisionNumber),
    algorithmVersion: dto.algorithmVersion,
    inputSnapshotHash: dto.inputSnapshotHash,
    shifts: (dto.shifts ?? []).map(mapShiftAssignmentFromBackend),
    summary: mapSummary(dto.summary),
    version: num(dto.version),
    createdAtUtc: dto.createdAtUtc,
    updatedAtUtc: dto.updatedAtUtc,
    reviewRequestedAtUtc: dto.reviewRequestedAtUtc,
    approvedAtUtc: dto.approvedAtUtc,
    publishedAtUtc: dto.publishedAtUtc,
    archivedAtUtc: dto.archivedAtUtc,
  };
}

function mapLeaveMarker(dto: LeaveMarkerResponseDto): LeaveMarker {
  return {
    leaveRequestId: dto.leaveRequestId,
    type: dto.type,
    status: dto.status,
    isFullDay: dto.isFullDay,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
  };
}

function mapDayCell(dto: EmployeeScheduleDayCellResponseDto): EmployeeScheduleDayCell {
  return {
    date: dto.date,
    shifts: (dto.shifts ?? []).map(mapShiftAssignmentFromBackend),
    leaveMarkers: (dto.leaveMarkers ?? []).map(mapLeaveMarker),
    issueCount: num(dto.issueCount),
  };
}

function mapMatrixRow(dto: EmployeeScheduleRowResponseDto): EmployeeScheduleRow {
  return {
    employeeId: dto.employeeId,
    employeeDisplayName: dto.employeeDisplayName,
    days: (dto.days ?? []).map(mapDayCell),
    assignedMinutes: num(dto.assignedMinutes),
    targetMinutes: num(dto.targetMinutes),
    plannedOvertimeMinutes: num(dto.plannedOvertimeMinutes),
    weekendShiftCount: num(dto.weekendShiftCount),
    eveningShiftCount: num(dto.eveningShiftCount),
    locationChangeCount: num(dto.locationChangeCount),
    warningIssueCount: num(dto.warningIssueCount),
  };
}

export function mapMatrixFromBackend(
  dto: EmployeeScheduleMatrixResponseDto,
): EmployeeScheduleMatrix {
  return {
    scheduleId: dto.scheduleId,
    periodStart: dto.periodStart,
    periodEnd: dto.periodEnd,
    scheduleVersion: num(dto.scheduleVersion),
    employees: (dto.employees ?? []).map(mapMatrixRow),
  };
}

function mapCoverageSlot(dto: LocationCoverageSlotResponseDto): LocationCoverageSlot {
  return {
    locationId: dto.locationId,
    locationName: dto.locationName,
    date: dto.date,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
    requiredCapability: mapCapabilityFromBackend(dto.requiredCapability),
    timeType: mapTimeTypeFromBackend(dto.timeType),
    requiredCount: num(dto.requiredCount),
    actualCount: num(dto.actualCount),
    shortage: num(dto.shortage),
    severity:
      dto.severity === "Blocking" ? "blocking" : dto.severity === "Warning" ? "warning" : "info",
    status: dto.status,
    employeeIds: dto.employeeIds ?? [],
  };
}

export function mapCoverageProjectionFromBackend(
  dto: LocationCoverageResponseDto,
): LocationCoverage {
  return {
    scheduleId: dto.scheduleId,
    periodStart: dto.periodStart,
    periodEnd: dto.periodEnd,
    scheduleVersion: num(dto.scheduleVersion),
    slots: (dto.slots ?? []).map(mapCoverageSlot),
  };
}

export function mapIssueFromBackend(dto: ScheduleIssueResponseDto): ScheduleIssueRow {
  let parameters: Record<string, unknown> = {};
  if (dto.parametersJson) {
    try {
      parameters = JSON.parse(dto.parametersJson) as Record<string, unknown>;
    } catch {
      parameters = { raw: dto.parametersJson };
    }
  }
  return {
    id: dto.id,
    code: dto.code,
    severity:
      dto.severity === "Blocking" ? "blocking" : dto.severity === "Warning" ? "warning" : "info",
    employeeId: dto.employeeId,
    locationId: dto.locationId,
    shiftAssignmentId: dto.shiftAssignmentId,
    date: dto.date,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
    parameters,
    isResolved: dto.isResolved,
    isAcknowledged: dto.isAcknowledged,
    version: num(dto.version),
  };
}

export function mapScheduleChangeFromBackend(dto: ScheduleChangeResponseDto): ScheduleChange {
  return {
    changeKind: dto.changeKind,
    shiftAssignmentId: dto.shiftAssignmentId,
    basedOnShiftId: dto.basedOnShiftId,
    employeeId: dto.employeeId,
    locationId: dto.locationId,
    date: dto.date,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
  };
}

function mapAlternative(dto: ScheduleAlternativeResponseDto): ScheduleAlternative {
  const components: Record<string, number> = {};
  for (const [k, v] of Object.entries(dto.scoreComponents ?? {})) components[k] = num(v);
  return {
    employeeId: dto.employeeId,
    employeeDisplayName: dto.employeeDisplayName,
    scoreDifference: num(dto.scoreDifference),
    scoreComponents: components,
    tradeoffCodes: dto.tradeoffCodes ?? [],
  };
}

export function mapShiftExplanationFromBackend(
  dto: ShiftExplanationResponseDto,
): ShiftAssignmentExplanation {
  const components: Record<string, number> = {};
  for (const [k, v] of Object.entries(dto.scoreComponents ?? {})) components[k] = num(v);
  return {
    shiftAssignmentId: dto.shiftAssignmentId,
    generationRunId: dto.generationRunId,
    algorithmVersion: dto.algorithmVersion,
    reasonCodes: dto.reasonCodes ?? [],
    scoreComponents: components,
    alternatives: (dto.alternatives ?? []).map(mapAlternative),
  };
}

const SCOPE_TYPE_TO: Record<RegenerationScopeInput["type"], BackendRegenerationScopeType> = {
  full: "FullPeriod",
  day: "Day",
  range: "DateRange",
  week: "Week",
  location: "Location",
  capability_time: "CapabilityAndTimeType",
  issues: "Issues",
};

export function mapRegenerationScopeToBackend(
  scope: RegenerationScopeInput,
): RegenerationScopeRequestDto {
  return {
    type: SCOPE_TYPE_TO[scope.type],
    dateFrom: scope.dateFrom ?? null,
    dateTo: scope.dateTo ?? null,
    locationId: scope.locationId ?? null,
    capability: scope.capability ? mapCapabilityToBackend(scope.capability) : null,
    timeType: scope.timeType ? mapTimeTypeToBackend(scope.timeType) : null,
    issueIds: scope.issueIds ?? null,
  };
}

// ─── Own schedule ──────────────────────────────────────────────────

export interface OwnScheduleShift {
  id: string;
  locationId: string;
  locationName: string;
  date: string;
  startTime: string;
  endTime: string;
  segments: AssignmentSegment[];
}

export interface OwnScheduleView {
  scheduleId: string;
  periodStart: string;
  periodEnd: string;
  publishedRevisionNumber: number;
  publishedAtUtc: string;
  shifts: OwnScheduleShift[];
}

function mapOwnShift(dto: OwnShiftResponseDto): OwnScheduleShift {
  return {
    id: dto.id,
    locationId: dto.locationId,
    locationName: dto.locationName,
    date: dto.date,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
    segments: (dto.segments ?? []).map(mapSegment),
  };
}

export function mapOwnScheduleFromBackend(dto: OwnScheduleResponseDto): OwnScheduleView {
  return {
    scheduleId: dto.scheduleId,
    periodStart: dto.periodStart,
    periodEnd: dto.periodEnd,
    publishedRevisionNumber: num(dto.publishedRevisionNumber),
    publishedAtUtc: dto.publishedAtUtc,
    shifts: (dto.shifts ?? []).map(mapOwnShift),
  };
}
