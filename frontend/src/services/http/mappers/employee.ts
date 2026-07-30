import type {
  EmployeeCreateRequestDto,
  EmployeeResponseDto,
  EmployeeUpdateRequestDto,
  BackendTimeWindowDto,
} from "../dto";
import type { Employee, PreferenceWindow, ShiftType, TimeType, Weekday } from "@/services/types";
import { mapProfessionalRoleFromBackend, mapProfessionalRoleToBackend } from "./enums";

const WEEKDAY_FROM: Record<string, Weekday> = {
  Every: "every",
  Mon: "mon",
  Tue: "tue",
  Wed: "wed",
  Thu: "thu",
  Fri: "fri",
  Sat: "sat",
  Sun: "sun",
};
const WEEKDAY_TO: Record<Weekday, string> = {
  every: "Every",
  mon: "Mon",
  tue: "Tue",
  wed: "Wed",
  thu: "Thu",
  fri: "Fri",
  sat: "Sat",
  sun: "Sun",
};

/** UI `ShiftType` ↔ backend `TimeType` értékek közti leképzés az
 *  `allowedShiftTypes` / `allowedTimeTypes` mezőkhöz. Csak azokra a
 *  backend értékekre reagálunk, amiket a UI ShiftType-ként ismer. */
const SHIFT_TYPE_FROM: Record<string, ShiftType | null> = {
  Work: "work",
  OnCallDuty: "on_call",
  Training: "training",
  Meeting: "meeting",
};
const SHIFT_TYPE_TO: Record<ShiftType, string> = {
  work: "Work",
  on_call: "OnCallDuty",
  training: "Training",
  meeting: "Meeting",
};

function mapWindow(dto: BackendTimeWindowDto): PreferenceWindow {
  const weekday = WEEKDAY_FROM[dto.dayOfWeek];
  if (!weekday) throw new Error(`Ismeretlen dayOfWeek: ${dto.dayOfWeek}`);
  const kind: PreferenceWindow["kind"] = dto.type === "Preferred" ? "preferred" : "blocked";
  return { weekday, start: dto.startTime, end: dto.endTime, kind };
}

function mapWindowToBackend(w: PreferenceWindow): BackendTimeWindowDto {
  return {
    dayOfWeek: WEEKDAY_TO[w.weekday],
    startTime: w.start,
    endTime: w.end,
    type: w.kind === "preferred" ? "Preferred" : "Blocked",
  };
}

export function mapEmployeeFromBackend(dto: EmployeeResponseDto): Employee & { version: number } {
  const allowedShiftTypes: ShiftType[] = [];
  for (const t of dto.allowedTimeTypes ?? []) {
    const ui = SHIFT_TYPE_FROM[t];
    if (ui) allowedShiftTypes.push(ui);
  }
  const windows = dto.timeWindows ?? [];
  const preferredWindows = windows.filter((w) => w.type === "Preferred").map(mapWindow);
  const blockedWindows = windows.filter((w) => w.type === "Blocked").map(mapWindow);
  const minutes = dto.monthlyMinutesLimit ?? 0;
  return {
    id: dto.id,
    fullName: dto.fullName,
    displayName: dto.displayName,
    professionalRole: mapProfessionalRoleFromBackend(dto.professionalRole),
    active: dto.isActive,
    schedulable: dto.isSchedulable,
    includeInAutoFill: dto.includeInAutoFill,
    countsAsPharmacist: dto.countsAsPharmacist,
    locationIds: (dto.locations ?? []).map((l) => l.locationId),
    locationAssignments: (dto.locations ?? []).map((l) => ({
      locationId: l.locationId,
      enabled: l.enabled,
      locationName: l.locationName,
    })),
    monthlyHoursTarget: Math.round((minutes / 60) * 100) / 100,
    maxDailyMinutes: dto.maxDailyMinutes ?? 0,
    birthDate: dto.birthDate ?? null,
    externalPayrollId: dto.externalPayrollId ?? null,
    linkedUser: dto.linkedUser
      ? {
          userId: dto.linkedUser.userId,
          email: dto.linkedUser.email,
          displayName: dto.linkedUser.displayName,
          active: dto.linkedUser.isActive,
        }
      : null,
    warnings: dto.warnings,
    allowedShiftTypes,
    preferredWindows,
    blockedWindows,
    version: dto.version,
  };
}

function buildAllowedTimeTypes(types: ShiftType[]): string[] {
  const out = new Set<string>();
  for (const t of types) out.add(SHIFT_TYPE_TO[t]);
  return [...out];
}

export function mapEmployeeToCreateRequest(e: Employee): EmployeeCreateRequestDto {
  const assignments =
    e.locationAssignments && e.locationAssignments.length > 0
      ? e.locationAssignments.map((a) => ({ locationId: a.locationId, enabled: a.enabled }))
      : e.locationIds.map((id) => ({ locationId: id, enabled: true }));
  const timeWindows: BackendTimeWindowDto[] = [
    ...e.preferredWindows.map(mapWindowToBackend),
    ...e.blockedWindows.map(mapWindowToBackend),
  ];
  return {
    fullName: e.fullName,
    displayName: e.displayName,
    professionalRole: mapProfessionalRoleToBackend(e.professionalRole),
    isActive: e.active,
    isSchedulable: e.schedulable,
    includeInAutoFill: e.includeInAutoFill,
    countsAsPharmacist: e.countsAsPharmacist,
    locations: assignments,
    monthlyMinutesLimit: e.monthlyHoursTarget > 0 ? Math.round(e.monthlyHoursTarget * 60) : null,
    maxDailyMinutes: e.maxDailyMinutes > 0 ? e.maxDailyMinutes : null,
    birthDate: e.birthDate ?? null,
    externalPayrollId: e.externalPayrollId ?? null,
    timeWindows,
    allowedTimeTypes: buildAllowedTimeTypes(e.allowedShiftTypes),
  };
}

export function mapEmployeeToUpdateRequest(
  e: Employee,
  expectedVersion: number,
): EmployeeUpdateRequestDto {
  return { ...mapEmployeeToCreateRequest(e), expectedVersion };
}

export function mapWindowsToBackend(windows: PreferenceWindow[]): BackendTimeWindowDto[] {
  return windows.map(mapWindowToBackend);
}

/** Kényelmi export: UI TimeType → backend TimeType érték. */
export const uiTimeTypeToBackend: Record<TimeType, string> = {
  work: "Work",
  overtime: "Overtime",
  on_call: "OnCallDuty",
  standby: "Standby",
  vacation: "AnnualLeave",
  sick: "SickLeave",
  unpaid: "UnpaidLeave",
  parental: "ParentalLeave",
  other: "Other",
};
