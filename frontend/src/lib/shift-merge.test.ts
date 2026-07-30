import { describe, expect, it } from "vitest";
import {
  continuousPresenceMinutes,
  dailyTotalMinutes,
  mergeSegments,
  minutesByType,
  primaryMinutes,
} from "./shift-merge";

const h = (n: number) => n * 60;

describe("shift-merge invariánsok", () => {
  it("08-14 + 14-18 azonos típus → 08-18 összevonás", () => {
    const res = mergeSegments([
      { type: "work", startMin: h(8), endMin: h(14), locationId: "L" },
      { type: "work", startMin: h(14), endMin: h(18), locationId: "L" },
    ]);
    expect(res.ok).toBe(true);
    expect(res.segments).toEqual([{ type: "work", startMin: h(8), endMin: h(18) }]);
  });

  it("08-14 + 13-18 átfedés → 08-18", () => {
    const res = mergeSegments([
      { type: "work", startMin: h(8), endMin: h(14), locationId: "L" },
      { type: "work", startMin: h(13), endMin: h(18), locationId: "L" },
    ]);
    expect(res.ok).toBe(true);
    expect(res.segments).toEqual([{ type: "work", startMin: h(8), endMin: h(18) }]);
  });

  it("08-14 + 15-18 hézagos split → tiltott", () => {
    const res = mergeSegments([
      { type: "work", startMin: h(8), endMin: h(14), locationId: "L" },
      { type: "work", startMin: h(15), endMin: h(18), locationId: "L" },
    ]);
    expect(res.ok).toBe(false);
    expect(res.errors[0]).toMatchObject({ code: "gap" });
  });

  it("ugyanaznap két telephely → tiltott", () => {
    const res = mergeSegments([
      { type: "work", startMin: h(8), endMin: h(14), locationId: "A" },
      { type: "work", startMin: h(14), endMin: h(18), locationId: "B" },
    ]);
    expect(res.ok).toBe(false);
    expect(res.errors[0]).toMatchObject({ code: "multi_location" });
  });

  it("08-16 Work + 16-18 Overtime → egy műszak, két szegmens", () => {
    const res = mergeSegments([
      { type: "work", startMin: h(8), endMin: h(16), locationId: "L" },
      { type: "overtime", startMin: h(16), endMin: h(18), locationId: "L" },
    ]);
    expect(res.ok).toBe(true);
    expect(res.segments).toHaveLength(2);
    expect(primaryMinutes(res.segments)).toBe(h(10));
    expect(continuousPresenceMinutes(res.segments)).toBe(h(10));
  });

  it("dailyTotalMinutes minden szegmenst összegez", () => {
    const res = mergeSegments([
      { type: "work", startMin: h(8), endMin: h(16), locationId: "L" },
      { type: "on_call", startMin: h(20), endMin: h(22), locationId: "L" },
    ]);
    expect(res.ok).toBe(true);
    expect(dailyTotalMinutes(res.segments)).toBe(h(10));
    expect(minutesByType(res.segments).on_call).toBe(h(2));
  });
});
