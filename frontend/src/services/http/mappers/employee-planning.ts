import type {
  CreateEmployeeShiftQuotaRuleRequestDto,
  DeactivateEmployeeShiftQuotaRuleRequestDto,
  EmployeeCapabilitiesResponseDto,
  EmployeeShiftQuotaRuleResponseDto,
  EmployeeWorkProfileResponseDto,
  UpdateEmployeeCapabilitiesRequestDto,
  UpdateEmployeeShiftQuotaRuleRequestDto,
  UpdateEmployeeWorkProfileRequestDto,
} from "../dto/employee-planning";
import type {
  CreateShiftQuotaRuleInput,
  EmployeeCapabilitiesData,
  EmployeeShiftQuotaRule,
  EmployeeWorkProfile,
  StaffingCapability,
  UpdateShiftQuotaRuleInput,
} from "@/services/types";
import { mapCapabilityFromBackend, mapCapabilityToBackend } from "./coverage";
import { normalizeConditionalPositiveLimit } from "@/lib/work-profile";

/** A backend számos numerikus mezőt `integer | string` alakban is elfogad. */
function num(v: number | string): number {
  return typeof v === "number" ? v : Number(v);
}
function numOrNull(v: number | string | null | undefined): number | null {
  if (v === null || v === undefined) return null;
  return num(v);
}

export function mapCapabilitiesFromBackend(
  dto: EmployeeCapabilitiesResponseDto,
): EmployeeCapabilitiesData {
  return {
    employeeId: dto.employeeId,
    assignedCapabilities: dto.assignedCapabilities.map(mapCapabilityFromBackend),
    effectiveCapabilities: dto.effectiveCapabilities.map(mapCapabilityFromBackend),
    countsAsPharmacistCompatibility: dto.countsAsPharmacistCompatibility,
    employeeVersion: num(dto.employeeVersion),
  };
}

export function mapCapabilitiesUpdateRequest(
  capabilities: StaffingCapability[],
  expectedEmployeeVersion: number,
): UpdateEmployeeCapabilitiesRequestDto {
  return {
    capabilities: capabilities.map(mapCapabilityToBackend),
    expectedEmployeeVersion,
  };
}

export function mapWorkProfileFromBackend(
  dto: EmployeeWorkProfileResponseDto,
): EmployeeWorkProfile {
  return {
    id: dto.id,
    version: num(dto.version),
    contractedMonthlyMinutes: num(dto.contractedMonthlyMinutes),
    contractedWeeklyMinutes: numOrNull(dto.contractedWeeklyMinutes),
    standardShiftMinutes: num(dto.standardShiftMinutes),
    minimumShiftMinutes: num(dto.minimumShiftMinutes),
    maximumRegularShiftMinutes: num(dto.maximumRegularShiftMinutes),
    maximumDailyMinutes: num(dto.maximumDailyMinutes),
    allowsLongShift: dto.allowsLongShift,
    maximumLongShiftMinutes: numOrNull(dto.maximumLongShiftMinutes),
    allowsFullOpeningHoursShift: dto.allowsFullOpeningHoursShift,
    allowsOvertime: dto.allowsOvertime,
    maximumOvertimeMinutesPerMonth: numOrNull(dto.maximumOvertimeMinutesPerMonth),
    allowsOnCallDuty: dto.allowsOnCallDuty,
    maximumOnCallAssignmentsPerMonth: numOrNull(dto.maximumOnCallAssignmentsPerMonth),
    allowsStandby: dto.allowsStandby,
    maximumStandbyAssignmentsPerMonth: numOrNull(dto.maximumStandbyAssignmentsPerMonth),
    allowsSaturday: dto.allowsSaturday,
    maximumSaturdaysPerMonth: numOrNull(dto.maximumSaturdaysPerMonth),
    allowsSunday: dto.allowsSunday,
    maximumSundaysPerMonth: numOrNull(dto.maximumSundaysPerMonth),
    includeInAutoFill: dto.includeInAutoFill,
  };
}

export function mapWorkProfileUpdateRequest(
  wp: EmployeeWorkProfile,
): UpdateEmployeeWorkProfileRequestDto {
  return {
    contractedMonthlyMinutes: wp.contractedMonthlyMinutes,
    contractedWeeklyMinutes: wp.contractedWeeklyMinutes,
    standardShiftMinutes: wp.standardShiftMinutes,
    minimumShiftMinutes: wp.minimumShiftMinutes,
    maximumRegularShiftMinutes: wp.maximumRegularShiftMinutes,
    maximumDailyMinutes: wp.maximumDailyMinutes,
    allowsLongShift: wp.allowsLongShift,
    maximumLongShiftMinutes: normalizeConditionalPositiveLimit(
      wp.allowsLongShift,
      wp.maximumLongShiftMinutes,
      "maximumLongShiftMinutes",
      "LONG_SHIFT_LIMIT_REQUIRED",
    ),
    allowsFullOpeningHoursShift: wp.allowsFullOpeningHoursShift,
    allowsOvertime: wp.allowsOvertime,
    maximumOvertimeMinutesPerMonth: normalizeConditionalPositiveLimit(
      wp.allowsOvertime,
      wp.maximumOvertimeMinutesPerMonth,
      "maximumOvertimeMinutesPerMonth",
      "OVERTIME_LIMIT_REQUIRED",
    ),
    allowsOnCallDuty: wp.allowsOnCallDuty,
    maximumOnCallAssignmentsPerMonth: normalizeConditionalPositiveLimit(
      wp.allowsOnCallDuty,
      wp.maximumOnCallAssignmentsPerMonth,
      "maximumOnCallAssignmentsPerMonth",
      "ON_CALL_LIMIT_REQUIRED",
    ),
    allowsStandby: wp.allowsStandby,
    maximumStandbyAssignmentsPerMonth: normalizeConditionalPositiveLimit(
      wp.allowsStandby,
      wp.maximumStandbyAssignmentsPerMonth,
      "maximumStandbyAssignmentsPerMonth",
      "STANDBY_LIMIT_REQUIRED",
    ),
    allowsSaturday: wp.allowsSaturday,
    maximumSaturdaysPerMonth: normalizeConditionalPositiveLimit(
      wp.allowsSaturday,
      wp.maximumSaturdaysPerMonth,
      "maximumSaturdaysPerMonth",
      "SATURDAY_LIMIT_REQUIRED",
    ),
    allowsSunday: wp.allowsSunday,
    maximumSundaysPerMonth: normalizeConditionalPositiveLimit(
      wp.allowsSunday,
      wp.maximumSundaysPerMonth,
      "maximumSundaysPerMonth",
      "SUNDAY_LIMIT_REQUIRED",
    ),
    includeInAutoFill: wp.includeInAutoFill,
    expectedVersion: wp.version,
  };
}

export function mapQuotaRuleFromBackend(
  dto: EmployeeShiftQuotaRuleResponseDto,
): EmployeeShiftQuotaRule {
  return {
    id: dto.id,
    employeeId: dto.employeeId,
    dimension: dto.dimension,
    period: dto.period,
    minimum: num(dto.minimum),
    target: num(dto.target),
    maximum: num(dto.maximum),
    severity: dto.severity,
    isActive: dto.isActive,
    version: num(dto.version),
  };
}

export function mapQuotaCreateRequest(
  input: CreateShiftQuotaRuleInput,
): CreateEmployeeShiftQuotaRuleRequestDto {
  return { ...input };
}

export function mapQuotaUpdateRequest(
  input: UpdateShiftQuotaRuleInput,
): UpdateEmployeeShiftQuotaRuleRequestDto {
  return { ...input };
}

export function mapQuotaDeactivateRequest(
  expectedVersion: number,
): DeactivateEmployeeShiftQuotaRuleRequestDto {
  return { expectedVersion };
}
