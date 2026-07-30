import type { CoverageRule, StaffingCapability } from "@/services/types";
import { parseHm } from "./duration";

export interface CoverageDemandPoint {
  minute: number;
  required: number;
}

export function buildDemandCurve(
  rules: CoverageRule[],
  weekday: number,
  capability: StaffingCapability,
): CoverageDemandPoint[] {
  const filtered = rules.filter(
    (r) => r.active && r.weekday === weekday && r.capability === capability,
  );
  if (filtered.length === 0) return [];
  const boundaries = new Set<number>();
  for (const r of filtered) {
    const s = parseHm(r.range.start);
    const e = parseHm(r.range.end);
    if (s === null || e === null || e <= s) continue;
    boundaries.add(s);
    boundaries.add(e);
  }
  const points: CoverageDemandPoint[] = [];
  const sortedBoundaries = [...boundaries].sort((a, b) => a - b);
  for (const t of sortedBoundaries) {
    let required = 0;
    for (const r of filtered) {
      const s = parseHm(r.range.start);
      const e = parseHm(r.range.end);
      if (s === null || e === null) continue;
      if (s <= t && e > t) required = Math.max(required, r.requiredCount);
    }
    points.push({ minute: t, required });
  }
  return points;
}

export function maxRequiredBetween(
  points: CoverageDemandPoint[],
  startMin: number,
  endMin: number,
): number {
  let max = 0;
  for (let i = 0; i < points.length; i++) {
    const p = points[i];
    const next = points[i + 1]?.minute ?? 24 * 60;
    if (next <= startMin || p.minute >= endMin) continue;
    max = Math.max(max, p.required);
  }
  return max;
}
