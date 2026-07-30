// Óra + perc ↔ percek segédek. UI barátságos, belül percben tárol.

export function toMinutesFromParts(hours: number, minutes: number): number {
  return Math.max(0, hours) * 60 + Math.max(0, minutes);
}

export function splitMinutes(total: number): { hours: number; minutes: number } {
  const clamped = Math.max(0, Math.round(total));
  return { hours: Math.floor(clamped / 60), minutes: clamped % 60 };
}

/** "HH:mm" → perc; érvénytelen inputra null. */
export function parseHm(hm: string): number | null {
  const m = /^([0-2]\d):([0-5]\d)$/.exec(hm);
  if (!m) return null;
  const h = Number(m[1]);
  const mm = Number(m[2]);
  if (h > 24 || (h === 24 && mm !== 0)) return null;
  return h * 60 + mm;
}

/** Perc → "HH:mm". */
export function formatHm(min: number): string {
  const clamped = Math.max(0, Math.min(24 * 60, Math.round(min)));
  const h = Math.floor(clamped / 60);
  const m = clamped % 60;
  return `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}`;
}

/** Perc → "H ó M p" ember-olvasható. */
export function humanMinutes(min: number): string {
  const { hours, minutes } = splitMinutes(min);
  if (hours && minutes) return `${hours} ó ${minutes} p`;
  if (hours) return `${hours} ó`;
  return `${minutes} p`;
}
