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
