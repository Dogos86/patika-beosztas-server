import { describe, expect, it } from "vitest";
import type { Employee } from "@/services/types";
import { employeeCapabilities, hasCapability, migrateEmployeeCapabilities } from "./capability-map";

function baseEmployee(patch: Partial<Employee>): Employee {
  return {
    id: "e",
    fullName: "Teszt",
    displayName: "Teszt",
    professionalRole: "assistant",
    active: true,
    schedulable: true,
    includeInAutoFill: true,
    countsAsPharmacist: false,
    locationIds: [],
    monthlyHoursTarget: 160,
    maxDailyMinutes: 480,
    allowedShiftTypes: ["work"],
    preferredWindows: [],
    blockedWindows: [],
    ...patch,
  };
}

describe("capability-map", () => {
  it("pharmacy_manager kap pharmacist kompetenciát", () => {
    expect(employeeCapabilities(baseEmployee({ professionalRole: "pharmacy_manager" }))).toContain(
      "pharmacist",
    );
  });

  it("legacy countsAsPharmacist migrálódik", () => {
    const caps = employeeCapabilities(
      baseEmployee({ professionalRole: "assistant", countsAsPharmacist: true }),
    );
    expect(caps).toContain("pharmacist");
    expect(caps).toContain("assistant");
  });

  it("specialist_assistant senior_assistant + assistant", () => {
    expect(
      employeeCapabilities(baseEmployee({ professionalRole: "specialist_assistant" })),
    ).toEqual(expect.arrayContaining(["senior_assistant", "assistant"]));
  });

  it("explicit capabilities elsődleges", () => {
    const caps = employeeCapabilities(
      baseEmployee({ professionalRole: "assistant", capabilities: ["cleaner"] }),
    );
    expect(caps).toEqual(["cleaner"]);
  });

  it("hasCapability boolean", () => {
    const e = baseEmployee({ professionalRole: "pharmacist" });
    expect(hasCapability(e, "pharmacist")).toBe(true);
    expect(hasCapability(e, "cleaner")).toBe(false);
  });

  it("migrateEmployeeCapabilities backfill", () => {
    const m = migrateEmployeeCapabilities(baseEmployee({ professionalRole: "pharmacist" }));
    expect(m.capabilities).toContain("pharmacist");
  });
});
