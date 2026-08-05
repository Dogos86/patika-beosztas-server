export interface HoursAndMinutes {
  hours: number;
  minutes: number;
}

function nonNegativeInteger(value: number): number {
  return Number.isFinite(value) ? Math.max(0, Math.trunc(value)) : 0;
}

export function hoursAndMinutesToMinutes(hours: number, minutes: number): number {
  return nonNegativeInteger(hours) * 60 + Math.min(59, nonNegativeInteger(minutes));
}

export function splitMinutes(totalMinutes: number): HoursAndMinutes {
  const normalized = nonNegativeInteger(totalMinutes);
  return {
    hours: Math.floor(normalized / 60),
    minutes: normalized % 60,
  };
}
