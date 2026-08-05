import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/services/http/errors";
import {
  isConcurrencyError,
  regenerateWithLatestScheduleVersion,
  refreshScheduleAfterGeneration,
  SCHEDULE_REFRESHED_MESSAGE,
} from "./schedule-generation-flow";

describe("schedule generation flow", () => {
  it("Succeeded után listát, detailt és minden projekciót frissít", async () => {
    const invalidateQueries = vi.fn(async () => undefined);
    const refetchQueries = vi.fn(async () => undefined);
    await refreshScheduleAfterGeneration(
      { invalidateQueries, refetchQueries } as never,
      "schedule-1",
    );

    expect(refetchQueries).toHaveBeenCalledWith({
      queryKey: ["schedule", "schedule-1", "detail"],
    });
    expect(invalidateQueries).toHaveBeenNthCalledWith(1, { queryKey: ["schedules"] });
    expect(invalidateQueries).toHaveBeenNthCalledWith(2, {
      queryKey: ["schedule", "schedule-1", "matrix"],
    });
    expect(invalidateQueries).toHaveBeenNthCalledWith(3, {
      queryKey: ["schedule", "schedule-1", "coverage"],
    });
    expect(invalidateQueries).toHaveBeenNthCalledWith(4, {
      queryKey: ["schedule", "schedule-1", "issues"],
    });
    expect(invalidateQueries).toHaveBeenNthCalledWith(5, {
      queryKey: ["schedule", "schedule-1", "changes"],
    });
  });

  it("409-et felismer és magyar újratöltési üzenetet ad", () => {
    expect(isConcurrencyError(new ApiError("CONFLICT", "x", 409))).toBe(true);
    expect(SCHEDULE_REFRESHED_MESSAGE).toContain("Az adatokat újratöltöttük");
  });

  it("újragenerálás előtt friss detailből veszi az expectedVersion értéket", async () => {
    const get = vi.fn(async () => ({ version: 17 }));
    const regenerate = vi.fn(async () => ({ id: "run-1" }));
    const fetchQuery = vi.fn(async (options: { queryFn: () => Promise<unknown> }) =>
      options.queryFn(),
    );
    const scope = { type: "full" as const };

    await regenerateWithLatestScheduleVersion(
      { fetchQuery } as never,
      { get, regenerate } as never,
      "schedule-1",
      scope,
    );

    expect(fetchQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        queryKey: ["schedule", "schedule-1", "detail"],
        staleTime: 0,
      }),
    );
    expect(get).toHaveBeenCalledWith("schedule-1");
    expect(regenerate).toHaveBeenCalledWith("schedule-1", {
      scope,
      expectedVersion: 17,
    });
  });
});
