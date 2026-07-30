import type { WorkPreference, WorkPreferenceInput, WorkPreferenceType } from "@/services/types";
import type {
  BackendWorkPreferenceType,
  CreateWorkPreferenceRequestDto,
  DeactivateWorkPreferenceRequestDto,
  UpdateWorkPreferenceRequestDto,
  WorkPreferenceResponseDto,
} from "../dto/work-preference";
import { mapWeekdayFromBackend, mapWeekdayToBackend } from "./location";

const TYPES: readonly BackendWorkPreferenceType[] = [
  "Available",
  "Preferred",
  "Avoid",
  "Unavailable",
  "Fixed",
];

export function mapWorkPreferenceTypeFromBackend(v: string): WorkPreferenceType {
  if (!TYPES.includes(v as BackendWorkPreferenceType)) {
    throw new Error(`Ismeretlen WorkPreferenceType: ${v}`);
  }
  return v as WorkPreferenceType;
}

/** "HH:mm:ss" → "HH:mm"; null marad null. */
export function timeFromWire(t: string | null): string | null {
  if (!t) return null;
  const [h, m] = t.split(":");
  return `${h.padStart(2, "0")}:${(m ?? "00").padStart(2, "0")}`;
}

/** "HH:mm" → "HH:mm:ss"; üres/null marad null. */
export function timeToWire(t: string | null | undefined): string | null {
  if (!t) return null;
  const parts = t.split(":");
  const h = (parts[0] ?? "00").padStart(2, "0");
  const m = (parts[1] ?? "00").padStart(2, "0");
  return `${h}:${m}:00`;
}

export function mapWorkPreferenceFromBackend(dto: WorkPreferenceResponseDto): WorkPreference {
  return {
    id: dto.id,
    employeeId: dto.employeeId,
    employeeDisplayName: dto.employeeDisplayName,
    type: mapWorkPreferenceTypeFromBackend(dto.type),
    dateFrom: dto.dateFrom,
    dateTo: dto.dateTo,
    weekday: dto.dayOfWeek ? mapWeekdayFromBackend(dto.dayOfWeek) : null,
    isFullDay: dto.isFullDay,
    startTime: dto.isFullDay ? null : timeFromWire(dto.startTime),
    endTime: dto.isFullDay ? null : timeFromWire(dto.endTime),
    locationId: dto.locationId,
    locationName: dto.locationName,
    note: dto.note,
    isActive: dto.isActive,
    version: Number(dto.version),
  };
}

export function mapWorkPreferenceToCreateRequest(
  input: WorkPreferenceInput,
): CreateWorkPreferenceRequestDto {
  return {
    type: input.type,
    dateFrom: input.dateFrom,
    dateTo: input.dateTo,
    dayOfWeek: input.weekday ? mapWeekdayToBackend(input.weekday) : null,
    isFullDay: input.isFullDay,
    // Egész napos kérésnél a szerver nem fogad idősávot.
    startTime: input.isFullDay ? null : timeToWire(input.startTime),
    endTime: input.isFullDay ? null : timeToWire(input.endTime),
    locationId: input.locationId ?? null,
    note: input.note?.trim() ? input.note.trim() : null,
  };
}

export function mapWorkPreferenceToUpdateRequest(
  input: WorkPreferenceInput,
  expectedVersion: number,
): UpdateWorkPreferenceRequestDto {
  return { ...mapWorkPreferenceToCreateRequest(input), expectedVersion };
}

export function mapWorkPreferenceDeactivateRequest(
  expectedVersion: number,
): DeactivateWorkPreferenceRequestDto {
  return { expectedVersion };
}
/** Legacy frontend érték ("FixedTemplate") migrálása a wire contract "Fixed" értékére. */
export function mapLegacyRuleKindToWorkPreferenceType(kind: string): WorkPreferenceType {
  return mapWorkPreferenceTypeFromBackend(kind === "FixedTemplate" ? "Fixed" : kind);
}
