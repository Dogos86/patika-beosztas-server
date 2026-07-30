import type { ShiftSegment, TimeType } from "@/services/types";

export interface RawSegment {
  type: TimeType;
  startMin: number;
  endMin: number;
  locationId: string;
}

export type MergeError =
  | { code: "multi_location"; locations: string[] }
  | { code: "gap"; between: [number, number] }
  | { code: "empty" };

export interface MergeResult {
  ok: boolean;
  segments: ShiftSegment[];
  locationId?: string;
  errors: MergeError[];
}

const PRIMARY_TYPES: TimeType[] = ["work", "overtime"];

export function isPrimary(t: TimeType): boolean {
  return PRIMARY_TYPES.includes(t);
}

export function mergeSegments(segments: RawSegment[]): MergeResult {
  if (segments.length === 0) {
    return { ok: false, segments: [], errors: [{ code: "empty" }] };
  }
  const locations = Array.from(new Set(segments.map((s) => s.locationId)));
  if (locations.length > 1) {
    return { ok: false, segments: [], errors: [{ code: "multi_location", locations }] };
  }
  const sorted = [...segments].sort((a, b) => a.startMin - b.startMin);
  const primary = sorted.filter((s) => isPrimary(s.type));
  for (let i = 1; i < primary.length; i++) {
    const prev = primary[i - 1];
    const cur = primary[i];
    if (cur.startMin > prev.endMin) {
      return {
        ok: false,
        segments: [],
        errors: [{ code: "gap", between: [prev.endMin, cur.startMin] }],
      };
    }
  }
  const byType = new Map<TimeType, ShiftSegment[]>();
  for (const s of sorted) {
    const arr = byType.get(s.type) ?? [];
    if (arr.length === 0) {
      arr.push({ type: s.type, startMin: s.startMin, endMin: s.endMin });
    } else {
      const last = arr[arr.length - 1];
      if (s.startMin <= last.endMin) last.endMin = Math.max(last.endMin, s.endMin);
      else arr.push({ type: s.type, startMin: s.startMin, endMin: s.endMin });
    }
    byType.set(s.type, arr);
  }
  const merged: ShiftSegment[] = [];
  for (const arr of byType.values()) merged.push(...arr);
  merged.sort((a, b) => a.startMin - b.startMin || a.type.localeCompare(b.type));
  return { ok: true, segments: merged, locationId: locations[0], errors: [] };
}

export function dailyTotalMinutes(segments: ShiftSegment[]): number {
  return segments.reduce((a, s) => a + (s.endMin - s.startMin), 0);
}

export function primaryMinutes(segments: ShiftSegment[]): number {
  return segments.filter((s) => isPrimary(s.type)).reduce((a, s) => a + (s.endMin - s.startMin), 0);
}

export function continuousPresenceMinutes(segments: ShiftSegment[]): number {
  const primary = segments.filter((s) => isPrimary(s.type));
  if (primary.length === 0) return 0;
  const start = Math.min(...primary.map((s) => s.startMin));
  const end = Math.max(...primary.map((s) => s.endMin));
  return end - start;
}

export function minutesByType(segments: ShiftSegment[]): Record<string, number> {
  const out: Record<string, number> = {};
  for (const s of segments) out[s.type] = (out[s.type] ?? 0) + (s.endMin - s.startMin);
  return out;
}
