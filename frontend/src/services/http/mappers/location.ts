import type {
  LocationCreateRequestDto,
  LocationResponseDto,
  LocationUpdateRequestDto,
} from "../dto";
import type {
  BackendOpeningDayMode,
  BackendShiftTemplateCategory,
  CreateLocationShiftTemplateRequestDto,
  LocationShiftTemplateResponseDto,
  LocationWeeklyOpeningResponseDto,
  OpeningDayRequestDto,
  UpdateLocationShiftTemplateRequestDto,
  UpdateLocationWeeklyOpeningRequestDto,
} from "../dto/location";
import type { BackendDayOfWeek } from "../dto/enums";
import type {
  Location,
  LocationOpeningHours,
  LocationWeeklyOpening,
  OpeningHoursDay,
  ShiftTemplate,
  ShiftTemplateCategory,
  ShiftTemplateInput,
  WeekdayKey,
} from "@/services/types";
import { mapLocationKindFromBackend, mapLocationKindToBackend } from "./enums";
import { mapCapabilityFromBackend, mapCapabilityToBackend } from "./coverage";
import { WEEKDAY_KEYS, defaultOpeningHours } from "@/lib/opening-hours";

// ─── Alap telephely ────────────────────────────────────────────────

export function mapLocationFromBackend(dto: LocationResponseDto): Location {
  return {
    id: dto.id,
    name: dto.name,
    kind: mapLocationKindFromBackend(dto.type),
    active: dto.isActive,
    address: dto.address ?? null,
    version: Number(dto.version),
  };
}

export function mapLocationToCreateRequest(l: Location): LocationCreateRequestDto {
  return {
    name: l.name,
    type: mapLocationKindToBackend(l.kind),
    address: l.address ?? null,
    isActive: l.active,
  };
}

export function mapLocationToUpdateRequest(
  l: Location,
  expectedVersion: number,
): LocationUpdateRequestDto {
  return { ...mapLocationToCreateRequest(l), expectedVersion };
}

// ─── Idő + nap segédek ─────────────────────────────────────────────

/** "HH:mm:ss" | "HH:mm" → perc. */
export function timeToMinutes(t: string): number {
  const [h, m] = t.split(":");
  return Number(h) * 60 + Number(m);
}

/** Perc → "HH:mm:ss" (1440 → "23:59:00", mert a wire `time` nem enged 24-et). */
export function minutesToTime(min: number): string {
  const clamped = Math.max(0, Math.min(24 * 60 - 1, Math.round(min)));
  const h = Math.floor(clamped / 60);
  const m = clamped % 60;
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}:00`;
}

const DOW_FROM: Record<BackendDayOfWeek, WeekdayKey> = {
  Monday: "mon",
  Tuesday: "tue",
  Wednesday: "wed",
  Thursday: "thu",
  Friday: "fri",
  Saturday: "sat",
  Sunday: "sun",
};
const DOW_TO: Record<WeekdayKey, BackendDayOfWeek> = {
  mon: "Monday",
  tue: "Tuesday",
  wed: "Wednesday",
  thu: "Thursday",
  fri: "Friday",
  sat: "Saturday",
  sun: "Sunday",
};

export function mapWeekdayFromBackend(v: string): WeekdayKey {
  const out = DOW_FROM[v as BackendDayOfWeek];
  if (!out) throw new Error(`Ismeretlen DayOfWeek: ${v}`);
  return out;
}
export function mapWeekdayToBackend(v: WeekdayKey): BackendDayOfWeek {
  return DOW_TO[v];
}

// ─── Heti nyitvatartás ─────────────────────────────────────────────

const MODE_FROM: Record<BackendOpeningDayMode, OpeningHoursDay["mode"]> = {
  Closed: "closed",
  Open24Hours: "twentyFour",
  CustomIntervals: "custom",
};
const MODE_TO: Record<OpeningHoursDay["mode"], BackendOpeningDayMode> = {
  closed: "Closed",
  twentyFour: "Open24Hours",
  custom: "CustomIntervals",
};

export function mapWeeklyOpeningFromBackend(
  dto: LocationWeeklyOpeningResponseDto,
): LocationWeeklyOpening {
  const hours: LocationOpeningHours = defaultOpeningHours();
  for (const day of dto.days) {
    const key = mapWeekdayFromBackend(day.dayOfWeek);
    const mode = MODE_FROM[day.mode];
    if (!mode) throw new Error(`Ismeretlen OpeningDayMode: ${day.mode}`);
    if (mode === "twentyFour") {
      hours[key] = { mode, intervals: [{ startMin: 0, endMin: 24 * 60 }] };
    } else if (mode === "closed") {
      hours[key] = { mode, intervals: [] };
    } else {
      hours[key] = {
        mode,
        intervals: day.intervals.map((iv) => ({
          startMin: timeToMinutes(iv.startTime),
          endMin: iv.endTime === null ? 24 * 60 : timeToMinutes(iv.endTime),
        })),
      };
    }
  }
  return {
    locationId: dto.locationId,
    hours,
    warnings: dto.warnings ?? [],
    version: Number(dto.version),
  };
}

export function mapWeeklyOpeningToUpdateRequest(
  hours: LocationOpeningHours,
  expectedVersion: number | null,
): UpdateLocationWeeklyOpeningRequestDto {
  const days: OpeningDayRequestDto[] = WEEKDAY_KEYS.map((key) => {
    const day = hours[key];
    return {
      dayOfWeek: mapWeekdayToBackend(key),
      mode: MODE_TO[day.mode],
      intervals:
        day.mode === "custom"
          ? day.intervals.map((iv) => ({
              startTime: minutesToTime(iv.startMin),
              endTime: iv.endMin >= 24 * 60 ? null : minutesToTime(iv.endMin),
            }))
          : [],
    };
  });
  return { days, expectedVersion };
}

// ─── Műszaksablonok ────────────────────────────────────────────────

const CATEGORY_FROM: Record<BackendShiftTemplateCategory, ShiftTemplateCategory> = {
  Morning: "AM",
  Afternoon: "PM",
  Long: "Long",
  Custom: "Custom",
};
const CATEGORY_TO: Record<ShiftTemplateCategory, BackendShiftTemplateCategory> = {
  AM: "Morning",
  PM: "Afternoon",
  Long: "Long",
  Custom: "Custom",
};

export function mapShiftTemplateFromBackend(dto: LocationShiftTemplateResponseDto): ShiftTemplate {
  const category = CATEGORY_FROM[dto.category];
  if (!category) throw new Error(`Ismeretlen ShiftTemplateCategory: ${dto.category}`);
  return {
    id: dto.id,
    locationId: dto.locationId,
    name: dto.name,
    category,
    days: dto.weekdays.map(mapWeekdayFromBackend),
    startMin: timeToMinutes(dto.startTime),
    endMin: timeToMinutes(dto.endTime),
    active: dto.isActive,
    requiredCapability: dto.requiredCapability
      ? mapCapabilityFromBackend(dto.requiredCapability)
      : undefined,
    version: Number(dto.version),
  };
}

export function mapShiftTemplateToCreateRequest(
  input: ShiftTemplateInput,
): CreateLocationShiftTemplateRequestDto {
  return {
    name: input.name,
    category: CATEGORY_TO[input.category],
    weekdays: input.days.map(mapWeekdayToBackend),
    startTime: minutesToTime(input.startMin),
    endTime: minutesToTime(input.endMin),
    isActive: input.active,
    requiredCapability: input.requiredCapability
      ? mapCapabilityToBackend(input.requiredCapability)
      : null,
  };
}

export function mapShiftTemplateToUpdateRequest(
  input: ShiftTemplateInput,
  expectedVersion: number,
): UpdateLocationShiftTemplateRequestDto {
  return { ...mapShiftTemplateToCreateRequest(input), expectedVersion };
}
