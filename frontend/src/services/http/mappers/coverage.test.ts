import { describe, expect, it } from "vitest";
import {
  mapCoverageFromBackend,
  mapCoverageToCreateRequest,
  mapCoverageToUpdateRequest,
  mapCapabilityFromBackend,
  mapCapabilityToBackend,
} from "./coverage";
import type { CoverageRequirementResponseDto } from "../dto/coverage";

const sampleDto: CoverageRequirementResponseDto = {
  id: "c1",
  locationId: "loc-1",
  locationName: "Központ",
  locationIsActive: true,
  dayOfWeek: "Monday",
  startTime: "08:00:00",
  endTime: "20:00:00",
  requiredCapability: "SpecialistAssistant",
  requiredCount: "2",
  severity: "Blocking",
  isActive: true,
  warnings: [],
  version: "3",
};

describe("coverage mapper", () => {
  it("mapCoverageFromBackend normalizes numeric strings and enums", () => {
    const r = mapCoverageFromBackend(sampleDto);
    expect(r.id).toBe("c1");
    expect(r.weekday).toBe(0);
    expect(r.range).toEqual({ start: "08:00", end: "20:00" });
    expect(r.capability).toBe("senior_assistant");
    expect(r.requiredCount).toBe(2);
    expect(r.severity).toBe("blocking");
    expect(r.active).toBe(true);
    expect(r.version).toBe(3);
  });

  it("mapCoverageToCreateRequest and update roundtrip", () => {
    const rule = mapCoverageFromBackend(sampleDto);
    const create = mapCoverageToCreateRequest(rule);
    expect(create.dayOfWeek).toBe("Monday");
    expect(create.startTime).toBe("08:00:00");
    expect(create.endTime).toBe("20:00:00");
    expect(create.requiredCapability).toBe("SpecialistAssistant");
    expect(create.severity).toBe("Blocking");
    expect(create.isActive).toBe(true);
    const upd = mapCoverageToUpdateRequest(rule, 7);
    expect(upd.expectedVersion).toBe(7);
  });

  it("mapCapabilityFromBackend rejects unknown values", () => {
    expect(() => mapCapabilityFromBackend("Unknown")).toThrow();
  });

  it("mapCoverageFromBackend rejects unknown day-of-week", () => {
    expect(() => mapCoverageFromBackend({ ...sampleDto, dayOfWeek: "Funday" as never })).toThrow();
  });

  it("capability roundtrip is symmetric", () => {
    for (const c of [
      "pharmacist",
      "specialist_pharmacist",
      "senior_assistant",
      "assistant",
      "cleaner",
      "finance",
      "other",
    ] as const) {
      expect(mapCapabilityFromBackend(mapCapabilityToBackend(c))).toBe(c);
    }
  });
});
