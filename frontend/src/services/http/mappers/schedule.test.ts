import { describe, it, expect } from "vitest";
import {
  mapShiftAssignmentFromBackend,
  mapMatrixFromBackend,
  mapCoverageProjectionFromBackend,
  mapIssueFromBackend,
  mapRegenerationScopeToBackend,
  mapShiftExplanationFromBackend,
  mapGenerationRunFromBackend,
  mapSchedulePlanFromBackend,
  mapOwnScheduleFromBackend,
} from "./schedule";

describe("schedule mappers", () => {
  it("shift assignment: HH:mm rövidítés, numerikus verzió, segments", () => {
    const s = mapShiftAssignmentFromBackend({
      id: "s1",
      employeeId: "e1",
      employeeDisplayName: "Alma",
      locationId: "l1",
      locationName: "Fő",
      date: "2026-01-05",
      startTime: "08:00:00",
      endTime: "16:00:00",
      source: "Generated",
      isLocked: false,
      generatedByRunId: "r1",
      replacesShiftId: null,
      changeKind: "New",
      segments: [
        {
          id: "seg1",
          startTime: "08:00:00",
          endTime: "16:00:00",
          timeType: "Work",
          minutes: "480",
        },
      ],
      version: "3",
    });
    expect(s.startTime).toBe("08:00");
    expect(s.version).toBe(3);
    expect(s.segments[0].minutes).toBe(480);
    expect(s.segments[0].timeType).toBe("work");
  });

  it("matrix rows: numerikus mezők", () => {
    const m = mapMatrixFromBackend({
      scheduleId: "sch1",
      periodStart: "2026-01-05",
      periodEnd: "2026-01-11",
      scheduleVersion: "7",
      employees: [
        {
          employeeId: "e1",
          employeeDisplayName: "Alma",
          days: [],
          assignedMinutes: "0",
          targetMinutes: "9600",
          plannedOvertimeMinutes: "0",
          weekendShiftCount: "0",
          eveningShiftCount: "0",
          locationChangeCount: "0",
          warningIssueCount: "0",
        },
      ],
    });
    expect(m.scheduleVersion).toBe(7);
    expect(m.employees[0].targetMinutes).toBe(9600);
  });

  it("coverage slot: severity, capability, timeType mapping", () => {
    const c = mapCoverageProjectionFromBackend({
      scheduleId: "sch1",
      periodStart: "2026-01-05",
      periodEnd: "2026-01-11",
      scheduleVersion: 1,
      slots: [
        {
          locationId: "l1",
          locationName: "Fő",
          date: "2026-01-05",
          startTime: "08:00:00",
          endTime: "12:00:00",
          requiredCapability: "Pharmacist",
          timeType: "Work",
          requiredCount: "2",
          actualCount: "1",
          shortage: "1",
          severity: "Blocking",
          status: "Understaffed",
          employeeIds: ["e1"],
        },
      ],
    });
    expect(c.slots[0].severity).toBe("blocking");
    expect(c.slots[0].requiredCapability).toBe("pharmacist");
    expect(c.slots[0].timeType).toBe("work");
  });

  it("issue: parametersJson parse + fallback", () => {
    const i = mapIssueFromBackend({
      id: "i1",
      code: "MissingPharmacist",
      severity: "Warning",
      employeeId: null,
      locationId: "l1",
      shiftAssignmentId: null,
      date: "2026-01-05",
      startTime: "08:00:00",
      endTime: "12:00:00",
      parametersJson: '{"missing":1}',
      isResolved: false,
      isAcknowledged: false,
      version: "1",
    });
    expect(i.severity).toBe("warning");
    expect(i.parameters.missing).toBe(1);

    const bad = mapIssueFromBackend({
      id: "i2",
      code: "X",
      severity: "Info",
      employeeId: null,
      locationId: null,
      shiftAssignmentId: null,
      date: null,
      startTime: null,
      endTime: null,
      parametersJson: "not json",
      isResolved: false,
      isAcknowledged: false,
      version: 1,
    });
    expect(bad.parameters.raw).toBe("not json");
  });

  it("regeneration scope: minden típus a helyes backend kódra", () => {
    expect(mapRegenerationScopeToBackend({ type: "full" }).type).toBe("FullPeriod");
    expect(mapRegenerationScopeToBackend({ type: "day", dateFrom: "2026-01-05" }).type).toBe("Day");
    expect(
      mapRegenerationScopeToBackend({ type: "range", dateFrom: "2026-01-05", dateTo: "2026-01-07" })
        .type,
    ).toBe("DateRange");
    expect(mapRegenerationScopeToBackend({ type: "week", dateFrom: "2026-01-05" }).type).toBe(
      "Week",
    );
    expect(mapRegenerationScopeToBackend({ type: "location", locationId: "l1" }).type).toBe(
      "Location",
    );
    const r = mapRegenerationScopeToBackend({
      type: "capability_time",
      capability: "pharmacist",
      timeType: "work",
    });
    expect(r.type).toBe("CapabilityAndTimeType");
    expect(r.capability).toBe("Pharmacist");
    expect(r.timeType).toBe("Work");
    expect(mapRegenerationScopeToBackend({ type: "issues", issueIds: ["i1"] }).type).toBe("Issues");
  });

  it("explanation: score mezők számmá", () => {
    const e = mapShiftExplanationFromBackend({
      shiftAssignmentId: "s1",
      generationRunId: "r1",
      algorithmVersion: "1.0.0",
      reasonCodes: ["PreferredWindowMatch"],
      scoreComponents: { total: "42" },
      alternatives: [
        {
          employeeId: "e2",
          employeeDisplayName: "B",
          scoreDifference: "-5",
          scoreComponents: { fairness: "3" },
          tradeoffCodes: ["WeekendFairness"],
        },
      ],
    });
    expect(e.scoreComponents.total).toBe(42);
    expect(e.alternatives[0].scoreDifference).toBe(-5);
  });

  it("generation run + schedule plan: numerikus mezők", () => {
    const run = mapGenerationRunFromBackend({
      id: "r1",
      schedulePlanId: "sch1",
      status: "Running",
      solverStatus: "Feasible",
      requestedAtUtc: "2026-01-01T00:00:00Z",
      startedAtUtc: null,
      completedAtUtc: null,
      cancellationRequestedAtUtc: null,
      algorithmVersion: "1.0.0",
      deterministicSeed: "42",
      inputSnapshotHash: "abc",
      objectiveValue: "100",
      statistics: {
        candidateOptionCount: "1",
        variableCount: "1",
        constraintCount: "1",
        wallTimeSeconds: "0.5",
        bestObjectiveBound: null,
        conflicts: null,
        branches: null,
      },
      errorCode: null,
      redactedError: null,
      version: "2",
    });
    expect(run.deterministicSeed).toBe(42);
    expect(run.version).toBe(2);

    const plan = mapSchedulePlanFromBackend({
      id: "sch1",
      periodStart: "2026-01-05",
      periodEnd: "2026-01-11",
      timeZoneId: "Europe/Budapest",
      status: "Draft",
      basedOnScheduleId: null,
      publishedRevisionNumber: "0",
      algorithmVersion: "1.0.0",
      inputSnapshotHash: "abc",
      shifts: [],
      summary: {
        blockingCoveragePercent: "100",
        blockingIssueCount: "0",
        warningIssueCount: "0",
        preferenceFulfillmentPercent: "80",
        employeesOutsideTargetCount: "0",
        pendingLeaveOverlapShiftCount: "0",
        multiLocationConflictCount: "0",
        newShiftCount: "0",
        modifiedShiftCount: "0",
        deletedShiftCount: "0",
        unchangedShiftCount: "0",
        plannedOvertimeMinutes: "0",
      },
      version: "1",
      createdAtUtc: "2026-01-01T00:00:00Z",
      updatedAtUtc: "2026-01-01T00:00:00Z",
      reviewRequestedAtUtc: null,
      approvedAtUtc: null,
      publishedAtUtc: null,
      archivedAtUtc: null,
    });
    expect(plan.version).toBe(1);
    expect(plan.summary.blockingCoveragePercent).toBe(100);
  });

  it("own schedule: HH:mm shift rövidítés + numerikus revision", () => {
    const own = mapOwnScheduleFromBackend({
      scheduleId: "sch1",
      periodStart: "2026-01-05",
      periodEnd: "2026-01-11",
      publishedRevisionNumber: "1",
      publishedAtUtc: "2026-01-04T12:00:00Z",
      shifts: [
        {
          id: "s1",
          locationId: "l1",
          locationName: "Fő",
          date: "2026-01-05",
          startTime: "08:00:00",
          endTime: "16:00:00",
          segments: [],
        },
      ],
    });
    expect(own.publishedRevisionNumber).toBe(1);
    expect(own.shifts[0].startTime).toBe("08:00");
  });
});
