import { describe, it, expect } from "vitest";
import {
  mapLocationKindFromBackend,
  mapLocationKindToBackend,
  mapProfessionalRoleFromBackend,
  mapProfessionalRoleToBackend,
  mapTimeTypeToLeaveType,
} from "./enums";

describe("enum mapperek", () => {
  it("ProfessionalRole oda-vissza", () => {
    expect(mapProfessionalRoleFromBackend("Pharmacist")).toBe("pharmacist");
    expect(mapProfessionalRoleToBackend("pharmacy_manager")).toBe("PharmacyManager");
  });

  it("Ismeretlen role dob", () => {
    expect(() => mapProfessionalRoleFromBackend("Xxx")).toThrow();
  });

  it("LocationKind: Central ↔ headquarters", () => {
    expect(mapLocationKindFromBackend("Central")).toBe("headquarters");
    expect(mapLocationKindToBackend("headquarters")).toBe("Central");
    expect(mapLocationKindToBackend("branch")).toBe("Branch");
  });

  it("TimeType → LeaveType szűrés", () => {
    expect(mapTimeTypeToLeaveType("AnnualLeave")).toBe("annual_leave");
    expect(mapTimeTypeToLeaveType("Work")).toBeNull();
    expect(() => mapTimeTypeToLeaveType("Xxx")).toThrow();
  });
});
