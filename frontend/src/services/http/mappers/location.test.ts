import { describe, it, expect } from "vitest";
import {
  mapLocationFromBackend,
  mapLocationToCreateRequest,
  mapLocationToUpdateRequest,
  mapShiftTemplateFromBackend,
  mapShiftTemplateToCreateRequest,
  mapShiftTemplateToUpdateRequest,
  mapWeeklyOpeningFromBackend,
  mapWeeklyOpeningToUpdateRequest,
} from "./location";
import { mapPagedResponse } from "@/lib/pagination";
import type { LocationResponseDto } from "../dto";
import type {
  LocationShiftTemplateResponseDto,
  LocationWeeklyOpeningResponseDto,
} from "../dto/location";

const locDto: LocationResponseDto = {
  id: "l1",
  name: "Központi patika",
  type: "Central",
  address: "Fő utca 1.",
  isActive: true,
  version: 4,
};

describe("location mapper", () => {
  it("lapozott telephely választ képez le", () => {
    const paged = mapPagedResponse(
      { items: [locDto], totalCount: 31, page: 2, pageSize: 20 },
      mapLocationFromBackend,
    );
    expect(paged.total).toBe(31);
    expect(paged.items[0]).toEqual({
      id: "l1",
      name: "Központi patika",
      kind: "headquarters",
      active: true,
      address: "Fő utca 1.",
      version: 4,
    });
  });

  it("string verziót számmá alakít", () => {
    expect(mapLocationFromBackend({ ...locDto, version: "7" as unknown as number }).version).toBe(
      7,
    );
  });

  it("create és update request (expectedVersion)", () => {
    const loc = mapLocationFromBackend(locDto);
    expect(mapLocationToCreateRequest(loc)).toEqual({
      name: "Központi patika",
      type: "Central",
      address: "Fő utca 1.",
      isActive: true,
    });
    expect(mapLocationToUpdateRequest(loc, 4).expectedVersion).toBe(4);
  });
});

const openingDto: LocationWeeklyOpeningResponseDto = {
  id: "o1",
  locationId: "l1",
  locationName: "Központi patika",
  locationIsActive: true,
  version: "3",
  warnings: ["Vasárnap zárva."],
  days: [
    { dayOfWeek: "Monday", mode: "Closed", intervals: [] },
    {
      dayOfWeek: "Tuesday",
      mode: "Open24Hours",
      intervals: [{ id: "i0", startTime: "00:00:00", endTime: null }],
    },
    {
      dayOfWeek: "Wednesday",
      mode: "CustomIntervals",
      intervals: [
        { id: "i1", startTime: "08:00:00", endTime: "12:00:00" },
        { id: "i2", startTime: "14:00:00", endTime: null },
      ],
    },
  ],
};

describe("weekly opening mapper", () => {
  it("Closed / Open24Hours / CustomIntervals + több intervallum", () => {
    const w = mapWeeklyOpeningFromBackend(openingDto);
    expect(w.version).toBe(3);
    expect(w.warnings).toEqual(["Vasárnap zárva."]);
    expect(w.hours.mon).toEqual({ mode: "closed", intervals: [] });
    expect(w.hours.tue).toEqual({ mode: "twentyFour", intervals: [{ startMin: 0, endMin: 1440 }] });
    expect(w.hours.wed).toEqual({
      mode: "custom",
      intervals: [
        { startMin: 480, endMin: 720 },
        { startMin: 840, endMin: 1440 },
      ],
    });
    // A hiányzó napok alapértelmezetten zártak
    expect(w.hours.sun.mode).toBe("closed");
  });

  it("update request mind a 7 napot küldi, expectedVersion-nel", () => {
    const w = mapWeeklyOpeningFromBackend(openingDto);
    const req = mapWeeklyOpeningToUpdateRequest(w.hours, w.version);
    expect(req.expectedVersion).toBe(3);
    expect(req.days).toHaveLength(7);
    expect(req.days[0]).toEqual({ dayOfWeek: "Monday", mode: "Closed", intervals: [] });
    expect(req.days[1]).toEqual({ dayOfWeek: "Tuesday", mode: "Open24Hours", intervals: [] });
    expect(req.days[2].intervals).toEqual([
      { startTime: "08:00:00", endTime: "12:00:00" },
      { startTime: "14:00:00", endTime: null },
    ]);
  });

  it("első mentéskor expectedVersion lehet null", () => {
    const w = mapWeeklyOpeningFromBackend(openingDto);
    expect(mapWeeklyOpeningToUpdateRequest(w.hours, null).expectedVersion).toBeNull();
  });
});

const tplDto: LocationShiftTemplateResponseDto = {
  id: "t1",
  locationId: "l1",
  locationName: "Központi patika",
  category: "Afternoon",
  name: "Délutános",
  weekdays: ["Monday", "Saturday"],
  startTime: "14:00:00",
  endTime: "20:00:00",
  isActive: true,
  requiredCapability: "SpecialistPharmacist",
  version: "2",
};

describe("shift template mapper", () => {
  it("response → UI modell", () => {
    expect(mapShiftTemplateFromBackend(tplDto)).toEqual({
      id: "t1",
      locationId: "l1",
      name: "Délutános",
      category: "PM",
      days: ["mon", "sat"],
      startMin: 840,
      endMin: 1200,
      active: true,
      requiredCapability: "specialist_pharmacist",
      version: 2,
    });
  });

  it("create request kompetencia nélkül null-t küld", () => {
    const t = mapShiftTemplateFromBackend({ ...tplDto, requiredCapability: null });
    const req = mapShiftTemplateToCreateRequest(t);
    expect(req.requiredCapability).toBeNull();
    expect(req).toMatchObject({
      name: "Délutános",
      category: "Afternoon",
      weekdays: ["Monday", "Saturday"],
      startTime: "14:00:00",
      endTime: "20:00:00",
      isActive: true,
    });
  });

  it("update request expectedVersion-t tesz hozzá", () => {
    const t = mapShiftTemplateFromBackend(tplDto);
    expect(mapShiftTemplateToUpdateRequest(t, 2).expectedVersion).toBe(2);
  });
});
