import { describe, it, expect } from "vitest";
import {
  mapLegacyRuleKindToWorkPreferenceType,
  mapWorkPreferenceDeactivateRequest,
  mapWorkPreferenceFromBackend,
  mapWorkPreferenceToCreateRequest,
  mapWorkPreferenceToUpdateRequest,
  mapWorkPreferenceTypeFromBackend,
  timeFromWire,
  timeToWire,
} from "./work-preference";
import type { WorkPreferenceInput, WorkPreferenceType } from "@/services/types";
import type { WorkPreferenceResponseDto } from "../dto/work-preference";

const dto: WorkPreferenceResponseDto = {
  id: "wp1",
  employeeId: "e1",
  employeeDisplayName: "Teszt Elek",
  type: "Preferred",
  dateFrom: "2026-01-01",
  dateTo: "2026-03-31",
  dayOfWeek: "Monday",
  isFullDay: false,
  startTime: "08:00:00",
  endTime: "16:30:00",
  locationId: "l1",
  locationName: "Fiók",
  note: "reggeli műszak",
  isActive: true,
  version: 3,
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-02T00:00:00Z",
};

const input: WorkPreferenceInput = {
  type: "Unavailable",
  dateFrom: "2026-02-01",
  dateTo: "2026-02-10",
  weekday: "sat",
  isFullDay: false,
  startTime: "9:5",
  endTime: "17:00",
  locationId: null,
  note: "  ",
};

describe("work preference mapperek", () => {
  it("minden enum értéket leképez", () => {
    const all: WorkPreferenceType[] = ["Available", "Preferred", "Avoid", "Unavailable", "Fixed"];
    for (const t of all) expect(mapWorkPreferenceTypeFromBackend(t)).toBe(t);
    expect(() => mapWorkPreferenceTypeFromBackend("Nope")).toThrow();
  });

  it("FixedTemplate → Fixed migráció", () => {
    expect(mapLegacyRuleKindToWorkPreferenceType("FixedTemplate")).toBe("Fixed");
    expect(mapLegacyRuleKindToWorkPreferenceType("Avoid")).toBe("Avoid");
  });

  it("idő oda-vissza konverzió", () => {
    expect(timeFromWire("08:00:00")).toBe("08:00");
    expect(timeFromWire(null)).toBeNull();
    expect(timeToWire("9:5")).toBe("09:05:00");
    expect(timeToWire(null)).toBeNull();
  });

  it("időtartományos választ képez le", () => {
    const wp = mapWorkPreferenceFromBackend(dto);
    expect(wp).toMatchObject({
      id: "wp1",
      type: "Preferred",
      weekday: "mon",
      startTime: "08:00",
      endTime: "16:30",
      locationId: "l1",
      version: 3,
    });
  });

  it("teljes napos válaszban nincs idősáv, nap nélkül null weekday", () => {
    const wp = mapWorkPreferenceFromBackend({
      ...dto,
      isFullDay: true,
      dayOfWeek: null,
      startTime: "08:00:00",
      endTime: "16:00:00",
      locationId: null,
      locationName: null,
    });
    expect(wp.startTime).toBeNull();
    expect(wp.endTime).toBeNull();
    expect(wp.weekday).toBeNull();
    expect(wp.locationId).toBeNull();
  });

  it("create request nem tartalmaz employeeId-t és normalizál", () => {
    const req = mapWorkPreferenceToCreateRequest(input);
    expect(req).not.toHaveProperty("employeeId");
    expect(req).toMatchObject({
      type: "Unavailable",
      dayOfWeek: "Saturday",
      startTime: "09:05:00",
      endTime: "17:00:00",
      locationId: null,
      note: null,
      dateFrom: "2026-02-01",
      dateTo: "2026-02-10",
    });
  });

  it("teljes napos create-ben nincs idősáv", () => {
    const req = mapWorkPreferenceToCreateRequest({ ...input, isFullDay: true });
    expect(req.startTime).toBeNull();
    expect(req.endTime).toBeNull();
  });

  it("update és deactivate viszi az expectedVersion-t", () => {
    expect(mapWorkPreferenceToUpdateRequest(input, 7).expectedVersion).toBe(7);
    expect(mapWorkPreferenceDeactivateRequest(4)).toEqual({ expectedVersion: 4 });
  });
});
