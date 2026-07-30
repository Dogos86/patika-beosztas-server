// Phase 2E.7 – regressziós tesztcsomag.
// Célja bizonyítani, hogy API-módban: (1) a request/response mapperek helyesek,
// (2) a verzió (`expectedVersion`) továbbmegy, (3) nincs csendes mock fallback,
// (4) nincs hamis sikeres mentés hiba esetén.

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { httpServices } from "./index";
import { clearCsrfToken } from "./csrf";

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
    };
    calls.push(call);
    return handler(call);
  });
  vi.stubGlobal("fetch", fetchMock);
  return calls;
}

const leaveDto = {
  id: "lr1",
  employeeId: "e1",
  employeeDisplayName: "Teszt Elek",
  type: "AnnualLeave" as const,
  dateFrom: "2026-08-03",
  dateTo: "2026-08-07",
  isFullDay: true,
  startTime: null,
  endTime: null,
  status: "Draft" as const,
  employeeNote: null,
  decisionReason: null,
  statusHistory: [
    {
      fromStatus: null,
      toStatus: "Draft" as const,
      occurredAtUtc: "2026-07-01T08:00:00Z",
      reason: null,
    },
  ],
  version: 1,
  createdAtUtc: "2026-07-01T08:00:00Z",
  updatedAtUtc: "2026-07-01T08:00:00Z",
};

const surveyDto = {
  id: "s1",
  employeeId: "e1",
  taxYear: 2026,
  status: "Draft",
  version: 1,
};

beforeEach(() => {
  (import.meta.env as Record<string, string>).VITE_API_URL = "http://api.test";
  clearCsrfToken();
});
afterEach(() => {
  (import.meta.env as Record<string, string>).VITE_API_URL = originalEnv ?? "";
  vi.restoreAllMocks();
});

describe("regresszió – távolléti folyamat", () => {
  it("saját éves szabadság create után automatikus submit fut", async () => {
    const calls = stubFetch((c) =>
      c.url.includes("/submit")
        ? json(200, { ...leaveDto, status: "Pending", version: 2 })
        : json(201, leaveDto),
    );
    const created = await httpServices.leaveRequest.createMyRequest({
      type: "annual_leave",
      fullDay: true,
      startDate: "2026-08-03",
      endDate: "2026-08-07",
    });
    expect(calls[0].url).toContain("/api/me/leave-requests");
    expect(calls[0].body).toMatchObject({ type: "AnnualLeave", isFullDay: true });
    expect(calls[1].body).toEqual({ expectedVersion: 1 });
    expect(created.status).toBe("pending");
  });

  it("admin jóváhagyás expectedVersion-nel megy és leképezi a történetet", async () => {
    const calls = stubFetch(() =>
      json(200, {
        ...leaveDto,
        status: "Approved",
        version: 3,
        statusHistory: [
          ...leaveDto.statusHistory,
          {
            fromStatus: "Pending",
            toStatus: "Approved",
            occurredAtUtc: "2026-07-02T08:00:00Z",
            reason: "rendben",
          },
        ],
      }),
    );
    const decided = await httpServices.adminLeaveRequest.decide("lr1", {
      action: "approve",
      note: "rendben",
      expectedVersion: 2,
    });
    expect(calls[0].url).toContain("/api/admin/leave-requests/lr1/decision");
    expect(calls[0].body).toMatchObject({ decision: "Approve", expectedVersion: 2 });
    expect(decided.status).toBe("approved");
    expect(decided.history?.at(-1)?.action).toBe("approved");
  });

  it("táppénz Reported → Recorded → Closed, nyitott végdátummal", async () => {
    const calls = stubFetch((c) =>
      json(
        200,
        c.url.includes("/close")
          ? { ...leaveDto, type: "SickLeave", status: "Closed", dateTo: "2026-08-09", version: 4 }
          : { ...leaveDto, type: "SickLeave", status: "Recorded", dateTo: null, version: 3 },
      ),
    );
    const recorded = await httpServices.adminLeaveRequest.record("lr1", 2);
    expect(recorded.status).toBe("recorded");
    const closed = await httpServices.adminLeaveRequest.close("lr1", "2026-08-09", 3);
    expect(closed.status).toBe("closed");
    expect(calls[0].body).toEqual({ expectedVersion: 2 });
    expect(calls[1].body).toEqual({ dateTo: "2026-08-09", expectedVersion: 3 });
  });

  it("hibás mentés nem ad hamis sikert", async () => {
    stubFetch(() => json(409, { title: "Conflict" }));
    await expect(httpServices.adminLeaveRequest.record("lr1", 1)).rejects.toMatchObject({
      code: "CONFLICT",
    });
  });

  it("hiányzó verzió esetén nincs csendes 0 fallback", async () => {
    stubFetch(() => json(200, leaveDto));
    await expect(httpServices.adminLeaveRequest.close("lr1", "2026-08-09")).rejects.toThrow(
      /verziószám/i,
    );
  });
});

describe("regresszió – payroll folyamat", () => {
  it("survey draft → submit → review → complete végigviszi a verziót", async () => {
    const calls = stubFetch((c) =>
      json(200, { ...surveyDto, status: c.url.includes("complete") ? "Completed" : "Submitted" }),
    );
    await httpServices.payroll.submitMySurvey("s1", 1);
    await httpServices.payroll.adminReviewSurvey("s1", { hrPayrollNote: "ok", expectedVersion: 2 });
    await httpServices.payroll.adminCompleteSurvey("s1", 3);
    expect(calls[0].url).toContain("/api/me/tax-allowance-surveys/s1/submit");
    expect(calls[0].body).toEqual({ expectedVersion: 1 });
    expect(calls[1].body).toEqual({ hrPayrollNote: "ok", expectedVersion: 2 });
    expect(calls[2].body).toEqual({ expectedVersion: 3 });
  });

  it("export URL explicit VITE_API_URL mellett is abszolút", async () => {
    const calls = stubFetch(() => new Response("x", { status: 200 }));
    await httpServices.payroll.exportOnboarding("e1", "csv");
    expect(calls[0].url).toBe(
      "http://api.test/api/admin/employees/e1/payroll-onboarding/export?format=csv",
    );
  });

  it("üres VITE_API_URL esetén az export relatív, azonos origines útvonalat használ", async () => {
    (import.meta.env as Record<string, string>).VITE_API_URL = "";
    const calls = stubFetch(() => new Response("x", { status: 200 }));
    await httpServices.payroll.exportOnboarding("e1", "json");
    expect(calls[0].url).toBe("/api/admin/employees/e1/payroll-onboarding/export?format=json");
  });
});

describe("regresszió – nincs csendes mock fallback", () => {
  it("a tudatosan későbbi modulok API-módban explicit hibát dobnak", async () => {
    stubFetch(() => json(200, []));
    await expect(httpServices.notification.listForUser("u1")).rejects.toThrow(/nem érhető el/i);
    await expect(httpServices.scheduleWorkspace.generate({} as never)).rejects.toThrow(
      /nem érhető el/i,
    );
  });
});
