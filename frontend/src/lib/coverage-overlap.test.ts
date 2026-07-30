import { describe, expect, it } from "vitest";
import type { CoverageRule } from "@/services/types";
import { buildDemandCurve, maxRequiredBetween } from "./coverage-overlap";

function rule(patch: Partial<CoverageRule>): CoverageRule {
  return {
    id: patch.id ?? "r",
    locationId: "L",
    weekday: 0,
    range: { start: "08:00", end: "16:00" },
    capability: "pharmacist",
    requiredCount: 1,
    severity: "blocking",
    active: true,
    ...patch,
  };
}

describe("coverage-overlap", () => {
  it("átfedő szabályok MAX-ot adnak (nem összeget)", () => {
    const rules = [
      rule({ id: "a", range: { start: "08:00", end: "16:00" }, requiredCount: 1 }),
      rule({ id: "b", range: { start: "10:00", end: "12:00" }, requiredCount: 2 }),
    ];
    const curve = buildDemandCurve(rules, 0, "pharmacist");
    expect(maxRequiredBetween(curve, 10 * 60, 12 * 60)).toBe(2);
    expect(maxRequiredBetween(curve, 14 * 60, 15 * 60)).toBe(1);
  });

  it("inaktív szabály figyelmen kívül", () => {
    const rules = [rule({ active: false, requiredCount: 5 })];
    expect(buildDemandCurve(rules, 0, "pharmacist")).toEqual([]);
  });

  it("más kompetencia figyelmen kívül", () => {
    const rules = [rule({ capability: "assistant", requiredCount: 3 })];
    expect(buildDemandCurve(rules, 0, "pharmacist")).toEqual([]);
  });
});
