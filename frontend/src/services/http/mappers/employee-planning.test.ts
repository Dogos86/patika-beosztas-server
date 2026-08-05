import { describe, it, expect } from "vitest";
import {
  mapCapabilitiesFromBackend,
  mapCapabilitiesUpdateRequest,
  mapQuotaRuleFromBackend,
  mapWorkProfileFromBackend,
  mapWorkProfileUpdateRequest,
} from "./employee-planning";
import type {
  EmployeeCapabilitiesResponseDto,
  EmployeeShiftQuotaRuleResponseDto,
  EmployeeWorkProfileResponseDto,
} from "../dto/employee-planning";
import type { EmployeeWorkProfile } from "@/services/types";

function workProfile(patch: Partial<EmployeeWorkProfile> = {}): EmployeeWorkProfile {
  return {
    id: "wp1",
    version: 7,
    contractedMonthlyMinutes: 10_080,
    contractedWeeklyMinutes: null,
    standardShiftMinutes: 480,
    minimumShiftMinutes: 240,
    maximumRegularShiftMinutes: 600,
    maximumDailyMinutes: 720,
    allowsLongShift: true,
    maximumLongShiftMinutes: 720,
    allowsFullOpeningHoursShift: true,
    allowsOvertime: false,
    maximumOvertimeMinutesPerMonth: null,
    allowsOnCallDuty: false,
    maximumOnCallAssignmentsPerMonth: null,
    allowsStandby: false,
    maximumStandbyAssignmentsPerMonth: null,
    allowsSaturday: false,
    maximumSaturdaysPerMonth: null,
    allowsSunday: false,
    maximumSundaysPerMonth: null,
    includeInAutoFill: true,
    ...patch,
  };
}

describe("employee-planning mapperek", () => {
  it("capabilities: backend PascalCase → UI enum + version (string/number)", () => {
    const dto: EmployeeCapabilitiesResponseDto = {
      employeeId: "e1",
      employeeDisplayName: "Kiss",
      assignedCapabilities: ["Pharmacist", "SpecialistAssistant"],
      effectiveCapabilities: ["Pharmacist", "SpecialistAssistant", "Assistant"],
      countsAsPharmacistCompatibility: true,
      employeeVersion: "3" as unknown as number,
    };
    const out = mapCapabilitiesFromBackend(dto);
    expect(out.assignedCapabilities).toEqual(["pharmacist", "senior_assistant"]);
    expect(out.effectiveCapabilities).toContain("assistant");
    expect(out.employeeVersion).toBe(3);
  });

  it("capabilities update: UI → PascalCase + expectedEmployeeVersion", () => {
    const req = mapCapabilitiesUpdateRequest(["pharmacist", "cleaner"], 5);
    expect(req).toEqual({
      capabilities: ["Pharmacist", "Cleaner"],
      expectedEmployeeVersion: 5,
    });
  });

  it("work profile: minden mező helyesen tükröződik", () => {
    const dto: EmployeeWorkProfileResponseDto = {
      id: "wp1",
      employeeId: "e1",
      employeeDisplayName: "K",
      contractedMonthlyMinutes: "10000" as unknown as number,
      contractedWeeklyMinutes: null,
      standardShiftMinutes: 480,
      minimumShiftMinutes: 240,
      maximumRegularShiftMinutes: 600,
      maximumDailyMinutes: 720,
      allowsLongShift: true,
      maximumLongShiftMinutes: 720,
      allowsFullOpeningHoursShift: false,
      allowsOvertime: false,
      maximumOvertimeMinutesPerMonth: null,
      allowsOnCallDuty: true,
      maximumOnCallAssignmentsPerMonth: 4,
      allowsStandby: false,
      maximumStandbyAssignmentsPerMonth: null,
      allowsSaturday: true,
      maximumSaturdaysPerMonth: 2,
      allowsSunday: false,
      maximumSundaysPerMonth: null,
      includeInAutoFill: true,
      version: 2,
      createdAtUtc: "2025-01-01T00:00:00Z",
      updatedAtUtc: "2025-01-02T00:00:00Z",
    };
    const wp = mapWorkProfileFromBackend(dto);
    expect(wp.contractedMonthlyMinutes).toBe(10000);
    expect(wp.contractedWeeklyMinutes).toBeNull();
    expect(wp.version).toBe(2);
    const req = mapWorkProfileUpdateRequest(wp);
    expect(req.expectedVersion).toBe(2);
    expect(req.allowsLongShift).toBe(true);
    expect(req.maximumOnCallAssignmentsPerMonth).toBe(4);
  });

  it("true + 720 hosszú műszakot és expectedVersiont küld", () => {
    const request = mapWorkProfileUpdateRequest(workProfile());
    expect(request.allowsLongShift).toBe(true);
    expect(request.maximumLongShiftMinutes).toBe(720);
    expect(request.expectedVersion).toBe(7);
    expect(request).toHaveProperty("allowsFullOpeningHoursShift", true);
    expect(request).not.toHaveProperty("longShiftAllowed");
    expect(request).not.toHaveProperty("fullOpeningHoursAllowed");
  });

  it("false esetén nullra normalizálja a hosszú műszak maximumát", () => {
    const request = mapWorkProfileUpdateRequest(
      workProfile({ allowsLongShift: false, maximumLongShiftMinutes: 720 }),
    );
    expect(request.maximumLongShiftMinutes).toBeNull();
  });

  it("nem enged true + null/0 hosszú műszak requestet", () => {
    expect(() =>
      mapWorkProfileUpdateRequest(workProfile({ maximumLongShiftMinutes: null })),
    ).toThrow("Hosszú műszak engedélyezésekor adj meg pozitív maximumot.");
    expect(() => mapWorkProfileUpdateRequest(workProfile({ maximumLongShiftMinutes: 0 }))).toThrow(
      "Hosszú műszak engedélyezésekor adj meg pozitív maximumot.",
    );
  });

  it("quota rule: backend válasz mappelése", () => {
    const dto: EmployeeShiftQuotaRuleResponseDto = {
      id: "q1",
      employeeId: "e1",
      employeeDisplayName: "K",
      dimension: "MorningShift",
      period: "Month",
      minimum: 2,
      target: 4,
      maximum: 6,
      severity: "Required",
      isActive: true,
      version: 1,
      createdAtUtc: "2025-01-01",
      updatedAtUtc: "2025-01-01",
    };
    const q = mapQuotaRuleFromBackend(dto);
    expect(q.dimension).toBe("MorningShift");
    expect(q.severity).toBe("Required");
    expect(q.target).toBe(4);
  });
});
