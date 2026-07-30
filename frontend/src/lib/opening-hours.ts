import type {
  LocationOpeningHours,
  OpeningHoursDay,
  OpeningInterval,
  WeekdayKey,
} from "@/services/types";

export const WEEKDAY_KEYS: WeekdayKey[] = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

export function emptyDay(): OpeningHoursDay {
  return { mode: "closed", intervals: [] };
}

export function twentyFourDay(): OpeningHoursDay {
  return { mode: "twentyFour", intervals: [{ startMin: 0, endMin: 24 * 60 }] };
}

export function defaultOpeningHours(): LocationOpeningHours {
  return WEEKDAY_KEYS.reduce((acc, k) => {
    acc[k] = emptyDay();
    return acc;
  }, {} as LocationOpeningHours);
}

export interface DayValidationError {
  weekday: WeekdayKey;
  code: "overlap" | "order" | "outOfRange" | "modeMismatch";
  message: string;
  intervalIndex?: number;
}

function validateIntervals(
  weekday: WeekdayKey,
  intervals: OpeningInterval[],
): DayValidationError[] {
  const errors: DayValidationError[] = [];
  const sorted = intervals
    .map((iv, index) => ({ iv, index }))
    .sort((a, b) => a.iv.startMin - b.iv.startMin);
  sorted.forEach(({ iv, index }, i) => {
    if (iv.startMin < 0 || iv.endMin > 24 * 60) {
      errors.push({
        weekday,
        code: "outOfRange",
        message: "Az intervallum kilóg a 24 órából.",
        intervalIndex: index,
      });
    }
    if (iv.endMin <= iv.startMin) {
      errors.push({
        weekday,
        code: "order",
        message: "A befejezés a kezdés után kell legyen.",
        intervalIndex: index,
      });
    }
    if (i > 0) {
      const prev = sorted[i - 1].iv;
      if (iv.startMin < prev.endMin) {
        errors.push({
          weekday,
          code: "overlap",
          message: "Az intervallumok átfednek.",
          intervalIndex: index,
        });
      }
    }
  });
  return errors;
}

export function validateDay(weekday: WeekdayKey, day: OpeningHoursDay): DayValidationError[] {
  if (day.mode === "closed") return [];
  if (day.mode === "twentyFour") {
    if (
      day.intervals.length !== 1 ||
      day.intervals[0].startMin !== 0 ||
      day.intervals[0].endMin !== 24 * 60
    ) {
      return [
        {
          weekday,
          code: "modeMismatch",
          message: "24 órás mód: pontosan egy 00:00–24:00 intervallum kell.",
        },
      ];
    }
    return [];
  }
  if (day.intervals.length === 0) {
    return [
      {
        weekday,
        code: "modeMismatch",
        message: "Egyedi módhoz legalább egy intervallum kell.",
      },
    ];
  }
  return validateIntervals(weekday, day.intervals);
}

export function validateOpeningHours(hours: LocationOpeningHours): DayValidationError[] {
  return WEEKDAY_KEYS.flatMap((k) => validateDay(k, hours[k]));
}

export function isWithinOpening(day: OpeningHoursDay, startMin: number, endMin: number): boolean {
  if (day.mode === "closed") return false;
  if (day.mode === "twentyFour") return true;
  return day.intervals.some((iv) => iv.startMin <= startMin && iv.endMin >= endMin);
}

export function isMinuteOpen(day: OpeningHoursDay, min: number): boolean {
  if (day.mode === "closed") return false;
  if (day.mode === "twentyFour") return true;
  return day.intervals.some((iv) => iv.startMin <= min && iv.endMin > min);
}

export function weekdayFromISO(iso: string): WeekdayKey {
  const d = new Date(iso);
  const idx = (d.getDay() + 6) % 7;
  return WEEKDAY_KEYS[idx];
}
