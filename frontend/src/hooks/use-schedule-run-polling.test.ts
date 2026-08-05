import { describe, expect, it } from "vitest";
import { isTerminalRunStatus, nextRunPollingInterval } from "./use-schedule-run-polling";

describe("schedule run polling", () => {
  it("Queued → Running alatt folytatja, Succeeded állapotnál leáll", () => {
    expect(nextRunPollingInterval("Queued", 2000)).toBe(2000);
    expect(nextRunPollingInterval("Running", 2000)).toBe(2000);
    expect(nextRunPollingInterval("Succeeded", 2000)).toBe(false);
    expect(isTerminalRunStatus("Failed")).toBe(true);
    expect(isTerminalRunStatus("Cancelled")).toBe(true);
  });
});
