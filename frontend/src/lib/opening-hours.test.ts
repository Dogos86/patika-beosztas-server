import { describe, expect, it } from "vitest";
import {
  defaultOpeningHours,
  emptyDay,
  twentyFourDay,
  validateDay,
  validateOpeningHours,
  isWithinOpening,
  weekdayFromISO,
} from "./opening-hours";

describe("opening-hours", () => {
  it("alapból minden nap zárva, hiba nélkül", () => {
    const h = defaultOpeningHours();
    expect(h.mon.mode).toBe("closed");
    expect(validateOpeningHours(h)).toEqual([]);
  });

  it("24 órás mód csak 00:00-24:00-val érvényes", () => {
    expect(validateDay("mon", twentyFourDay())).toEqual([]);
    const err = validateDay("mon", {
      mode: "twentyFour",
      intervals: [{ startMin: 60, endMin: 120 }],
    });
    expect(err).toHaveLength(1);
  });

  it("egyedi mód nem lehet üres", () => {
    const err = validateDay("mon", { mode: "custom", intervals: [] });
    expect(err[0].code).toBe("modeMismatch");
  });

  it("átfedő intervallumokat jelzi", () => {
    const errs = validateDay("mon", {
      mode: "custom",
      intervals: [
        { startMin: 8 * 60, endMin: 14 * 60 },
        { startMin: 13 * 60, endMin: 18 * 60 },
      ],
    });
    expect(errs.some((e) => e.code === "overlap")).toBe(true);
  });

  it("end<=start hibát dob", () => {
    const errs = validateDay("mon", {
      mode: "custom",
      intervals: [{ startMin: 14 * 60, endMin: 12 * 60 }],
    });
    expect(errs.some((e) => e.code === "order")).toBe(true);
  });

  it("isWithinOpening a teljes intervallumra vonatkozik", () => {
    const day = { mode: "custom" as const, intervals: [{ startMin: 480, endMin: 1080 }] };
    expect(isWithinOpening(day, 500, 700)).toBe(true);
    expect(isWithinOpening(day, 500, 1200)).toBe(false);
    expect(isWithinOpening(emptyDay(), 500, 600)).toBe(false);
  });

  it("weekdayFromISO hétfő-elsős", () => {
    expect(weekdayFromISO("2024-01-01")).toBe("mon");
    expect(weekdayFromISO("2024-01-07")).toBe("sun");
  });
});
