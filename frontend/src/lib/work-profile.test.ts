import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";
import type { EmployeeWorkProfile } from "@/services/types";
import { mapErrorResponse } from "@/services/http/errors";
import {
  getWorkProfileFieldErrors,
  refetchEmployeeWorkProfile,
  setLongShiftAllowed,
} from "./work-profile";

function profile(patch: Partial<EmployeeWorkProfile> = {}): EmployeeWorkProfile {
  return {
    id: null,
    version: null,
    contractedMonthlyMinutes: 10_080,
    contractedWeeklyMinutes: null,
    standardShiftMinutes: 480,
    minimumShiftMinutes: 240,
    maximumRegularShiftMinutes: 600,
    maximumDailyMinutes: 720,
    allowsLongShift: false,
    maximumLongShiftMinutes: null,
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

describe("munkaidőprofil űrlap", () => {
  it("bekapcsoláskor a napi maximummal inicializálja a hosszú műszakot", () => {
    expect(setLongShiftAllowed(profile(), true).maximumLongShiftMinutes).toBe(720);
  });

  it("kikapcsoláskor nullra normalizálja és megtartja a tiltott állapotot", () => {
    const result = setLongShiftAllowed(
      profile({ allowsLongShift: true, maximumLongShiftMinutes: 720 }),
      false,
    );
    expect(result.allowsLongShift).toBe(false);
    expect(result.maximumLongShiftMinutes).toBeNull();
  });

  it("a 422 kódot magyar mezőhibává alakítja", () => {
    const error = mapErrorResponse(422, {
      code: "VALIDATION_FAILED",
      errors: [
        {
          field: "maximumLongShiftMinutes",
          code: "LONG_SHIFT_LIMIT_REQUIRED",
          message: "backend message",
        },
      ],
    });
    expect(getWorkProfileFieldErrors(error).maximumLongShiftMinutes).toBe(
      "Hosszú műszak engedélyezésekor adj meg pozitív maximumot.",
    );
  });

  it("mentés után invalidál és a backend új verzióját tölti vissza", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const queryFn = vi.fn().mockResolvedValue(profile({ id: "wp1", version: 3 }));

    const result = await refetchEmployeeWorkProfile(queryClient, "e1", queryFn);

    expect(queryFn).toHaveBeenCalledOnce();
    expect(result?.id).toBe("wp1");
    expect(result?.version).toBe(3);
    expect(queryClient.getQueryData(["employee-work-profile", "e1"])).toEqual(result);
  });
});
