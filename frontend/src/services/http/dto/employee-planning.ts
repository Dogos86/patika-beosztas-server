// Employee planning DTO-k: kompetenciák, munkaidőprofil, kvóta-szabályok.
// Backend szerződés: openapi.phase2d.json.

import type { BackendStaffingCapability } from "./enums";

export type BackendShiftQuotaDimension =
  | "MorningShift"
  | "AfternoonShift"
  | "EveningShift"
  | "LongShift"
  | "SaturdayShift"
  | "SundayShift"
  | "OnCallDuty"
  | "Standby";

export type BackendQuotaPeriod = "Week" | "Month";
export type BackendQuotaSeverity = "Preferred" | "Required";

export interface EmployeeCapabilitiesResponseDto {
  employeeId: string;
  employeeDisplayName: string;
  assignedCapabilities: BackendStaffingCapability[];
  effectiveCapabilities: BackendStaffingCapability[];
  countsAsPharmacistCompatibility: boolean;
  employeeVersion: number;
}

export interface UpdateEmployeeCapabilitiesRequestDto {
  capabilities: BackendStaffingCapability[];
  expectedEmployeeVersion: number;
}

export interface EmployeeWorkProfileResponseDto {
  id: string;
  employeeId: string;
  employeeDisplayName: string;
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
  version: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateEmployeeWorkProfileRequestDto {
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
  /** Első létrehozásnál null; utána a legutóbb kapott `version`. */
  expectedVersion: number | null;
}

export interface EmployeeShiftQuotaRuleResponseDto {
  id: string;
  employeeId: string;
  employeeDisplayName: string;
  dimension: BackendShiftQuotaDimension;
  period: BackendQuotaPeriod;
  minimum: number;
  target: number;
  maximum: number;
  severity: BackendQuotaSeverity;
  isActive: boolean;
  version: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateEmployeeShiftQuotaRuleRequestDto {
  dimension: BackendShiftQuotaDimension;
  period: BackendQuotaPeriod;
  minimum: number;
  target: number;
  maximum: number;
  severity: BackendQuotaSeverity;
  isActive: boolean;
}

export interface UpdateEmployeeShiftQuotaRuleRequestDto extends CreateEmployeeShiftQuotaRuleRequestDto {
  expectedVersion: number;
}

export interface DeactivateEmployeeShiftQuotaRuleRequestDto {
  expectedVersion: number;
}
