import { describe, it, expect } from "vitest";
import {
  mapEmployeeFromBackend,
  mapEmployeeToCreateRequest,
  mapEmployeeToUpdateRequest,
} from "./employee";
import type { Employee } from "@/services/types";

const baseDto = {
  id: "e1",
  fullName: "Kovács Anna",
  displayName: "Anna",
  professionalRole: "Pharmacist" as const,
  isActive: true,
  isSchedulable: true,
  includeInAutoFill: true,
  countsAsPharmacist: true,
  locations: [
    { locationId: "loc-a", locationName: "A", enabled: true },
    { locationId: "loc-b", locationName: "B", enabled: true },
  ],
  monthlyMinutesLimit: 9600,
  maxDailyMinutes: 720,
  birthDate: null,
  externalPayrollId: null,
  allowedTimeTypes: ["Work", "OnCallDuty", "AnnualLeave"],
  timeWindows: [
    { dayOfWeek: "Mon", startTime: "08:00", endTime: "12:00", type: "Preferred" as const },
  ],
  linkedUser: null,
  version: 3,
};

describe("employee mapper", () => {
  it("isActive/isSchedulable → active/schedulable, minutes → hours, locations objektumlista", () => {
    const e = mapEmployeeFromBackend(baseDto);
    expect(e.active).toBe(true);
    expect(e.schedulable).toBe(true);
    expect(e.monthlyHoursTarget).toBe(160);
    expect(e.locationIds).toEqual(["loc-a", "loc-b"]);
    expect(e.allowedShiftTypes).toEqual(["work", "on_call"]);
    expect(e.preferredWindows[0].kind).toBe("preferred");
    expect(e.version).toBe(3);
  });

  it("create request-be percet ír, update-be expectedVersion-t is", () => {
    const ui: Employee = { ...mapEmployeeFromBackend(baseDto) } as Employee;
    const create = mapEmployeeToCreateRequest(ui);
    expect(create.monthlyMinutesLimit).toBe(9600);
    expect(create.isActive).toBe(true);
    expect(create).not.toHaveProperty("expectedVersion");
    const update = mapEmployeeToUpdateRequest(ui, 7);
    expect(update.expectedVersion).toBe(7);
  });
});
