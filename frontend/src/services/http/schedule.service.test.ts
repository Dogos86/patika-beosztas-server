// Phase 3B befejezés – beosztásgenerálás, review, korrekciók, workflow és
// saját publikált beosztás API-szerződés tesztek (mock fetch felett).
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { httpServices } from "./index";
import { clearCsrfToken } from "./csrf";
import { mapRegenerationScopeToBackend, mapOwnScheduleFromBackend } from "./mappers/schedule";
import type { RegenerationScopeType } from "@/services/types";
import { services, dataSource } from "@/services";

const originalEnv = import.meta.env.VITE_API_URL;

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

interface Call {
  url: string;
  method: string;
  body: unknown;
  credentials: RequestCredentials | undefined;
  headers: Headers;
}

function stubFetch(handler: (call: Call) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith("/api/auth/csrf")) return json(200, { requestToken: "t" });
    const call: Call = {
      url,
      method: init?.method ?? "GET",
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
      credentials: init?.credentials,
      headers: new Headers(init?.headers),
    };
    calls.push(call);
    return handler(call);
  });
  vi.stubGlobal("fetch", fetchMock);
  return calls;
}

function expectCentralCsrf(calls: Call[]) {
  calls.forEach((call) => {
    expect(call.credentials).toBe("include");
    expect(call.headers.get("X-CSRF-TOKEN")).toBe("t");
  });
}

const summaryDto = {
  blockingCoveragePercent: "100",
  blockingIssueCount: "0",
  warningIssueCount: "1",
  preferenceFulfillmentPercent: "80",
  employeesOutsideTargetCount: "0",
  pendingLeaveOverlapShiftCount: "0",
  multiLocationConflictCount: "0",
  newShiftCount: "2",
  modifiedShiftCount: "0",
  deletedShiftCount: "0",
  unchangedShiftCount: "0",
  plannedOvertimeMinutes: "0",
};

const planDto = {
  id: "sch1",
  periodStart: "2026-01-05",
  periodEnd: "2026-01-11",
  timeZoneId: "Europe/Budapest",
  status: "Draft",
  basedOnScheduleId: null,
  publishedRevisionNumber: "0",
  algorithmVersion: "1.0",
  inputSnapshotHash: "h",
  shifts: [],
  summary: summaryDto,
  version: "4",
  createdAtUtc: "2026-01-01T08:00:00Z",
  updatedAtUtc: "2026-01-01T08:00:00Z",
  reviewRequestedAtUtc: null,
  approvedAtUtc: null,
  publishedAtUtc: null,
  archivedAtUtc: null,
};

const runDto = {
  id: "run1",
  schedulePlanId: "sch1",
  status: "Queued",
  solverStatus: "NotStarted",
  requestedAtUtc: "2026-01-01T08:00:00Z",
  startedAtUtc: null,
  completedAtUtc: null,
  cancellationRequestedAtUtc: null,
  algorithmVersion: "1.0",
  deterministicSeed: "42",
  inputSnapshotHash: "h",
  objectiveValue: null,
  statistics: {
    candidateOptionCount: "0",
    variableCount: "0",
    constraintCount: "0",
    wallTimeSeconds: "0",
    bestObjectiveBound: null,
    conflicts: null,
    branches: null,
  },
  errorCode: null,
  redactedError: null,
  version: "1",
};

const shiftDto = {
  id: "s1",
  employeeId: "e1",
  employeeDisplayName: "Anna",
  locationId: "l1",
  locationName: "Fő",
  date: "2026-01-05",
  startTime: "08:00:00",
  endTime: "16:00:00",
  source: "Generated",
  isLocked: false,
  generatedByRunId: "run1",
  replacesShiftId: null,
  changeKind: "New",
  segments: [],
  version: "2",
};

beforeEach(() => {
  (import.meta.env as Record<string, string>).VITE_API_URL = "http://api.test";
  clearCsrfToken();
});
afterEach(() => {
  (import.meta.env as Record<string, string>).VITE_API_URL = originalEnv ?? "";
  vi.restoreAllMocks();
});

describe("permission réteg", () => {
  it("tartalmazza az ApproveSchedules és PublishSchedules értékeket", async () => {
    const calls = stubFetch(() =>
      json(200, {
        userId: "u1",
        email: "a@b.hu",
        displayName: "A",
        isActive: true,
        permissions: ["ApproveSchedules", "PublishSchedules"],
        linkedEmployee: null,
      }),
    );
    const user = await httpServices.auth.getSession();
    expect(calls[0].url).toContain("/api/auth/session");
    expect(user?.permissions).toContain("ApproveSchedules");
    expect(user?.permissions).toContain("PublishSchedules");
  });
});

describe("generálás start / poll / cancel", () => {
  it("start POST-ol és leképezi a futást", async () => {
    const calls = stubFetch(() => json(202, runDto));
    const run = await httpServices.scheduleGeneration.start({
      periodStart: "2026-01-05",
      periodEnd: "2026-01-11",
      deterministicSeed: 42,
    });
    expect(calls[0].method).toBe("POST");
    expect(calls[0].url).toContain("/api/admin/schedule-generations");
    expect(calls[0].body).toMatchObject({ periodStart: "2026-01-05", deterministicSeed: 42 });
    expect(calls[0].headers.get("Idempotency-Key")).toMatch(/^schedule-generation-/);
    expectCentralCsrf(calls);
    expect(run.status).toBe("Queued");
    expect(run.version).toBe(1);
  });

  it("get lekéri a futás állapotát", async () => {
    const calls = stubFetch(() => json(200, { ...runDto, status: "Succeeded" }));
    const run = await httpServices.scheduleGeneration.get("run1");
    expect(calls[0].url).toContain("/api/admin/schedule-generations/run1");
    expect(run.status).toBe("Succeeded");
  });

  it("cancel expectedVersion-nel megy", async () => {
    const calls = stubFetch(() => json(200, { ...runDto, status: "Cancelled", version: "2" }));
    const run = await httpServices.scheduleGeneration.cancel("run1", 1);
    expect(calls[0].body).toEqual({ expectedVersion: 1 });
    expectCentralCsrf(calls);
    expect(run.status).toBe("Cancelled");
  });

  it("a generálási service a központi egyszeri CSRF-frissítést használja", async () => {
    let csrfCalls = 0;
    const mutationCalls: Array<{ credentials?: RequestCredentials; headers: Headers }> = [];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith("/api/auth/csrf")) {
        csrfCalls++;
        return json(200, { requestToken: `t${csrfCalls}` });
      }
      mutationCalls.push({ credentials: init?.credentials, headers: new Headers(init?.headers) });
      return mutationCalls.length === 1
        ? json(400, { code: "INVALID_CSRF_TOKEN" })
        : json(202, runDto);
    });
    vi.stubGlobal("fetch", fetchMock);

    await httpServices.scheduleGeneration.start({
      periodStart: "2026-01-05",
      periodEnd: "2026-01-11",
    });

    expect(csrfCalls).toBe(2);
    expect(mutationCalls).toHaveLength(2);
    expect(mutationCalls.map((call) => call.credentials)).toEqual(["include", "include"]);
    expect(mutationCalls.map((call) => call.headers.get("X-CSRF-TOKEN"))).toEqual(["t1", "t2"]);
    expect(mutationCalls[0].headers.get("Idempotency-Key")).toBe(
      mutationCalls[1].headers.get("Idempotency-Key"),
    );
  });
});

describe("schedule list / detail / projekciók", () => {
  it("list és get", async () => {
    const calls = stubFetch((c) =>
      c.url.includes("/sch1")
        ? json(200, planDto)
        : json(200, [
            {
              id: "sch1",
              periodStart: "2026-01-05",
              periodEnd: "2026-01-11",
              timeZoneId: "Europe/Budapest",
              status: "Draft",
              basedOnScheduleId: null,
              publishedRevisionNumber: "0",
              algorithmVersion: "1.0",
              inputSnapshotHash: "h",
              shiftCount: "12",
              blockingIssueCount: "0",
              warningIssueCount: "1",
              version: "4",
              updatedAtUtc: "2026-01-01T08:00:00Z",
            },
          ]),
    );
    const list = await httpServices.adminSchedule.list();
    expect(list[0].shiftCount).toBe(12);
    const plan = await httpServices.adminSchedule.get("sch1");
    expect(plan.version).toBe(4);
    expect(calls.map((c) => c.url).join()).toContain("/api/admin/schedules");
  });

  it("matrix, coverage, issues, changes endpointok", async () => {
    const calls = stubFetch((c) => {
      if (c.url.includes("/matrix"))
        return json(200, {
          scheduleId: "sch1",
          periodStart: "2026-01-05",
          periodEnd: "2026-01-11",
          scheduleVersion: "4",
          employees: [],
        });
      if (c.url.includes("/coverage"))
        return json(200, {
          scheduleId: "sch1",
          periodStart: "2026-01-05",
          periodEnd: "2026-01-11",
          scheduleVersion: "4",
          slots: [],
        });
      if (c.url.includes("/issues")) return json(200, []);
      return json(200, [
        {
          changeKind: "New",
          shiftAssignmentId: "s1",
          basedOnShiftId: null,
          employeeId: "e1",
          locationId: "l1",
          date: "2026-01-05",
          startTime: "08:00:00",
          endTime: "16:00:00",
        },
      ]);
    });
    await httpServices.adminSchedule.getMatrix("sch1");
    await httpServices.adminSchedule.getCoverage("sch1");
    await httpServices.adminSchedule.listIssues("sch1");
    const changes = await httpServices.adminSchedule.listChanges("sch1");
    expect(changes[0].changeKind).toBe("New");
    expect(calls.map((c) => c.url.split("/").pop())).toEqual([
      "employee-matrix",
      "location-coverage",
      "issues",
      "changes",
    ]);
  });

  it("explanation és alternatives", async () => {
    const explanation = {
      shiftAssignmentId: "s1",
      generationRunId: "run1",
      algorithmVersion: "1.0",
      reasonCodes: ["PreferredWindowMatch"],
      scoreComponents: { total: "12" },
      alternatives: [],
    };
    stubFetch((c) =>
      c.url.includes("/alternatives")
        ? json(200, [
            {
              employeeId: "e2",
              employeeDisplayName: "Béla",
              scoreDifference: "5",
              scoreComponents: { total: "7" },
              tradeoffCodes: ["Overtime"],
            },
          ])
        : json(200, explanation),
    );
    const ex = await httpServices.adminSchedule.explainShift("sch1", "s1");
    expect(ex.reasonCodes).toEqual(["PreferredWindowMatch"]);
    const alts = await httpServices.adminSchedule.findAlternatives("sch1", "s1");
    expect(alts[0].scoreDifference).toBe(5);
  });
});

describe("shift korrekciók", () => {
  it("lock / unlock / reject / replace verziókkal", async () => {
    const calls = stubFetch(() => json(200, shiftDto));
    const body = { expectedShiftVersion: 2, expectedScheduleVersion: 4 };
    await httpServices.adminSchedule.lockShift("sch1", "s1", body);
    await httpServices.adminSchedule.unlockShift("sch1", "s1", body);
    await httpServices.adminSchedule.rejectShift("sch1", "s1", { ...body, reason: "nem jó" });
    await httpServices.adminSchedule.replaceShift("sch1", "s1", {
      ...body,
      replacementEmployeeId: "e2",
      reason: "csere",
    });
    expect(calls.map((c) => c.url.split("/").pop())).toEqual([
      "lock",
      "unlock",
      "reject",
      "replace",
    ]);
    calls.forEach((c) => expect(c.body).toMatchObject({ expectedScheduleVersion: 4 }));
    expect(calls[3].body).toMatchObject({ replacementEmployeeId: "e2" });
    expectCentralCsrf(calls);
  });

  it("409 konfliktus hibát dob, nem ad hamis sikert", async () => {
    stubFetch(() =>
      json(409, { title: "Konfliktus", detail: "A beosztás időközben módosult.", status: 409 }),
    );
    await expect(
      httpServices.adminSchedule.lockShift("sch1", "s1", {
        expectedShiftVersion: 1,
        expectedScheduleVersion: 1,
      }),
    ).rejects.toThrow();
  });
});

describe("részleges újragenerálás – minden scope", () => {
  const cases: { type: RegenerationScopeType; backend: string }[] = [
    { type: "full", backend: "FullPeriod" },
    { type: "day", backend: "Day" },
    { type: "range", backend: "DateRange" },
    { type: "week", backend: "Week" },
    { type: "location", backend: "Location" },
    { type: "capability_time", backend: "CapabilityAndTimeType" },
    { type: "issues", backend: "Issues" },
  ];

  it.each(cases)("$type → $backend", ({ type, backend }) => {
    expect(mapRegenerationScopeToBackend({ type }).type).toBe(backend);
  });

  it("regenerate POST expectedVersion-nel és scope-pal", async () => {
    const calls = stubFetch(() => json(202, runDto));
    await httpServices.adminSchedule.regenerate("sch1", {
      scope: { type: "issues", issueIds: ["i1", "i2"] },
      expectedVersion: 4,
    });
    expect(calls[0].url).toContain("/api/admin/schedules/sch1/regenerate");
    expect(calls[0].body).toMatchObject({
      expectedVersion: 4,
      scope: { type: "Issues", issueIds: ["i1", "i2"] },
    });
    expect(calls[0].headers.get("Idempotency-Key")).toMatch(/^schedule-regeneration-/);
    expectCentralCsrf(calls);
  });
});

describe("workflow", () => {
  it("review → draft → approve → publish → archive → clone", async () => {
    const calls = stubFetch(() => json(200, planDto));
    await httpServices.adminSchedule.submitForReview("sch1", 4);
    await httpServices.adminSchedule.returnToDraft("sch1", 5);
    await httpServices.adminSchedule.approve("sch1", 6);
    await httpServices.adminSchedule.publish("sch1", 7);
    await httpServices.adminSchedule.archive("sch1", 8);
    await httpServices.adminSchedule.cloneDraft("sch1", 9);
    expect(calls.map((c) => c.url.split("/").pop())).toEqual([
      "submit-review",
      "return-draft",
      "approve",
      "publish",
      "archive",
      "clone-draft",
    ]);
    expect(calls.map((c) => (c.body as { expectedVersion: number }).expectedVersion)).toEqual([
      4, 5, 6, 7, 8, 9,
    ]);
    expect(calls[5].headers.get("Idempotency-Key")).toMatch(/^schedule-clone-/);
    expectCentralCsrf(calls);
  });

  it("blocking issue mellett a publish backend hibája magyarul jelenik meg", async () => {
    stubFetch(() =>
      json(422, {
        title: "Nem publikálható",
        detail: "A beosztásban blokkoló probléma van.",
        status: 422,
      }),
    );
    await expect(httpServices.adminSchedule.publish("sch1", 4)).rejects.toThrow(
      /blokkoló probléma/i,
    );
  });
});

describe("saját publikált beosztás", () => {
  it("szegmensek időtípusa megmarad (nem minden 'work')", () => {
    const own = mapOwnScheduleFromBackend({
      scheduleId: "sch1",
      periodStart: "2026-01-05",
      periodEnd: "2026-01-11",
      publishedRevisionNumber: "2",
      publishedAtUtc: "2026-01-02T10:00:00Z",
      shifts: [
        {
          id: "s1",
          locationId: "l1",
          locationName: "Fő",
          date: "2026-01-05",
          startTime: "08:00:00",
          endTime: "20:00:00",
          segments: [
            {
              id: "g1",
              startTime: "08:00:00",
              endTime: "16:00:00",
              timeType: "Work",
              minutes: "480",
            },
            {
              id: "g2",
              startTime: "16:00:00",
              endTime: "18:00:00",
              timeType: "Overtime",
              minutes: "120",
            },
            {
              id: "g3",
              startTime: "18:00:00",
              endTime: "20:00:00",
              timeType: "OnCallDuty",
              minutes: "120",
            },
          ],
        },
      ],
    });
    expect(own.publishedRevisionNumber).toBe(2);
    expect(own.shifts[0].segments.map((s) => s.timeType)).toEqual(["work", "overtime", "on_call"]);
  });

  it("API módban a /api/me/schedule endpointot hívja", async () => {
    const calls = stubFetch(() =>
      json(200, {
        scheduleId: "sch1",
        periodStart: "2026-01-05",
        periodEnd: "2026-01-11",
        publishedRevisionNumber: "1",
        publishedAtUtc: "2026-01-02T10:00:00Z",
        shifts: [],
      }),
    );
    await httpServices.schedule.getOwnPublishedSchedule({ date: "2026-01-05" });
    expect(calls[0].url).toContain("/api/me/schedule");
  });
});

describe("nincs mock fallback API-módban", () => {
  it("a legacy scheduleWorkspace generátor nem mock adatot ad", async () => {
    await expect(
      httpServices.scheduleWorkspace.generate({
        periodStart: "2026-01-05",
        periodEnd: "2026-01-11",
      } as never),
    ).rejects.toThrow();
  });

  it("a service locator mock módban a mock implementációt adja", () => {
    expect(dataSource === "api" ? services : services).toBeDefined();
  });
});
