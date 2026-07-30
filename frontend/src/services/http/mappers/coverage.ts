// Coverage requirement mapperek. A backend numerikus mezői `integer | string`
// alakban is érkezhetnek, ezért mindenhol `Number(...)`-t használunk.

import type {
  CoverageRequirementResponseDto,
  CreateCoverageRequirementRequestDto,
  UpdateCoverageRequirementRequestDto,
  BackendCoverageSeverity,
} from "../dto/coverage";
import type { BackendDayOfWeek, BackendStaffingCapability } from "../dto/enums";
import type { CoverageRule, StaffingCapability } from "@/services/types";

// UI weekday: 0..6 ahol 0 = hétfő. Backend: Sunday..Saturday.
const DOW_FROM: Record<BackendDayOfWeek, number> = {
  Monday: 0,
  Tuesday: 1,
  Wednesday: 2,
  Thursday: 3,
  Friday: 4,
  Saturday: 5,
  Sunday: 6,
};
const DOW_TO: BackendDayOfWeek[] = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

const CAP_FROM: Record<BackendStaffingCapability, StaffingCapability> = {
  Pharmacist: "pharmacist",
  SpecialistPharmacist: "specialist_pharmacist",
  SpecialistAssistant: "senior_assistant",
  Assistant: "assistant",
  Cleaner: "cleaner",
  Finance: "finance",
  Other: "other",
};
const CAP_TO = Object.fromEntries(Object.entries(CAP_FROM).map(([k, v]) => [v, k])) as Record<
  StaffingCapability,
  BackendStaffingCapability
>;

export function mapCapabilityFromBackend(v: string): StaffingCapability {
  const out = CAP_FROM[v as BackendStaffingCapability];
  if (!out) throw new Error(`Ismeretlen StaffingCapability: ${v}`);
  return out;
}
export function mapCapabilityToBackend(v: StaffingCapability): BackendStaffingCapability {
  return CAP_TO[v];
}

/** "HH:mm:ss" → "HH:mm" (a backend `time` formátum HH:mm:ss lehet). */
function hhmm(t: string): string {
  return t.length >= 5 ? t.slice(0, 5) : t;
}

export function mapCoverageFromBackend(
  dto: CoverageRequirementResponseDto,
): CoverageRule & { version: number } {
  const weekday = DOW_FROM[dto.dayOfWeek];
  if (weekday === undefined) throw new Error(`Ismeretlen dayOfWeek: ${dto.dayOfWeek}`);
  return {
    id: dto.id,
    locationId: dto.locationId,
    weekday,
    range: { start: hhmm(dto.startTime), end: hhmm(dto.endTime) },
    requiredCount: Number(dto.requiredCount),
    severity: dto.severity === "Blocking" ? "blocking" : "warning",
    capability: mapCapabilityFromBackend(dto.requiredCapability),
    active: dto.isActive,
    version: Number(dto.version),
  };
}

export function mapCoverageToCreateRequest(r: CoverageRule): CreateCoverageRequirementRequestDto {
  const dow = DOW_TO[r.weekday];
  if (!dow) throw new Error(`Érvénytelen weekday index: ${r.weekday}`);
  return {
    locationId: r.locationId,
    dayOfWeek: dow,
    startTime: `${r.range.start}:00`,
    endTime: `${r.range.end}:00`,
    requiredCapability: mapCapabilityToBackend(r.capability),
    requiredCount: r.requiredCount,
    severity: r.severity === "blocking" ? "Blocking" : "Warning",
    isActive: r.active,
  };
}

export function mapCoverageToUpdateRequest(
  r: CoverageRule,
  expectedVersion: number,
): UpdateCoverageRequirementRequestDto {
  return { ...mapCoverageToCreateRequest(r), expectedVersion };
}

/** Backend severity szűrő értékek — a lista végpont opcionális filtere. */
export type CoverageSeverityFilter = "warning" | "blocking";
export function mapSeverityFilterToBackend(v: CoverageSeverityFilter): BackendCoverageSeverity {
  return v === "blocking" ? "Blocking" : "Warning";
}
