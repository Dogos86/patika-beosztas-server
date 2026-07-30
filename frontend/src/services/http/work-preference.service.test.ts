import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { httpServices } from "./index";
import { clearCsrfToken } from "./csrf";
import { ApiError } from "./errors";
import type { WorkPreferenceInput } from "@/services/types";

const originalEnv = import.meta.env.VITE_API_URL;

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

const wpDto = {
  id: "wp1",
  employeeId: "e1",
  employeeDisplayName: "Teszt Elek",
  type: "Preferred",
  dateFrom: "2026-01-01",
  dateTo: "2026-01-31",
  dayOfWeek: "Monday",
  isFullDay: true,
  startTime: null,
  endTime: null,
  locationId: null,
  locationName: null,
  note: null,
  isActive: true,
  version: 1,
};

const input: WorkPreferenceInput = {
  type: "Preferred",
  dateFrom: "2026-01-01",
  dateTo: "2026-01-31",
  weekday: "mon",
  isFullDay: true,
  startTime: null,
  endTime: null,
  locationId: null,
  note: null,
};

interface Call {
  url: string;
  method: string;
  body: unknown;
}

function stubFetch(handler: (call: Call) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (i: RequestInfo | URL, init?: RequestInit) => {
    const url = String(i);
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

describe("http work preference service", () => {
  beforeEach(() => {
    (import.meta.env as Record<string, string>).VITE_API_URL = "http://api.test";
    clearCsrfToken();
  });
  afterEach(() => {
    (import.meta.env as Record<string, string>).VITE_API_URL = originalEnv ?? "";
    vi.restoreAllMocks();
  });

  it("saját lista a /api/me végpontot hívja", async () => {
    const calls = stubFetch(() => json(200, [wpDto]));
    const list = await httpServices.workPreference.listMine(true);
    expect(list).toHaveLength(1);
    expect(calls[0].url).toContain("/api/me/work-preferences");
    expect(calls[0].url).toContain("includeInactive=true");
  });

  it("saját create nem küld employeeId-t", async () => {
    const calls = stubFetch(() => json(201, wpDto));
    await httpServices.workPreference.createMine(input);
    expect(calls[0].method).toBe("POST");
    expect(calls[0].url).toContain("/api/me/work-preferences");
    expect(calls[0].body).not.toHaveProperty("employeeId");
  });

  it("saját update expectedVersion-nel megy", async () => {
    const calls = stubFetch(() => json(200, { ...wpDto, version: 2 }));
    await httpServices.workPreference.updateMine("wp1", input, 1);
    expect(calls[0].method).toBe("PUT");
    expect(calls[0].url).toContain("/api/me/work-preferences/wp1");
    expect(calls[0].body).toMatchObject({ expectedVersion: 1 });
  });

  it("saját deactivate a dedikált végpontot hívja", async () => {
    const calls = stubFetch(() => json(200, { ...wpDto, isActive: false, version: 2 }));
    const res = await httpServices.workPreference.deactivateMine("wp1", 1);
    expect(res.isActive).toBe(false);
    expect(calls[0].url).toContain("/api/me/work-preferences/wp1/deactivate");
    expect(calls[0].body).toEqual({ expectedVersion: 1 });
  });

  it("admin lista és create employee-scoped", async () => {
    const calls = stubFetch(() => json(200, [wpDto]));
    await httpServices.adminWorkPreference.listForEmployee("e1");
    expect(calls[0].url).toContain("/api/admin/employees/e1/work-preferences");

    stubFetch(() => json(201, wpDto));
    await httpServices.adminWorkPreference.createForEmployee("e1", input);
  });

  it("admin update és deactivate ID-scoped, expectedVersion kötelező", async () => {
    const calls = stubFetch(() => json(200, wpDto));
    await httpServices.adminWorkPreference.update("wp1", input, 5);
    await httpServices.adminWorkPreference.deactivate("wp1", 5);
    expect(calls[0].url).toContain("/api/admin/work-preferences/wp1");
    expect(calls[0].body).toMatchObject({ expectedVersion: 5 });
    expect(calls[1].url).toContain("/api/admin/work-preferences/wp1/deactivate");
    await expect(
      httpServices.adminWorkPreference.update("wp1", input, null as unknown as number),
    ).rejects.toThrow();
  });

  it("409 CONFLICT hibát ad", async () => {
    stubFetch(() => json(409, { title: "Conflict" }));
    await expect(httpServices.workPreference.updateMine("wp1", input, 1)).rejects.toMatchObject({
      code: "CONFLICT",
    });
  });

  it("422 validációs mezőhibákat normalizál", async () => {
    stubFetch(() =>
      json(422, { title: "Invalid", errors: { dateTo: ["A záró dátum korábbi a kezdőnél."] } }),
    );
    const err = await httpServices.workPreference.createMine(input).catch((e: ApiError) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).code).toBe("VALIDATION");
    expect((err as ApiError).fieldErrors?.dateTo?.[0]).toContain("záró dátum");
  });

  it("más szervezet erőforrása 404-ként jelenik meg", async () => {
    stubFetch(() => json(404, { title: "Not found" }));
    await expect(httpServices.adminWorkPreference.update("wp9", input, 1)).rejects.toMatchObject({
      code: "NOT_FOUND",
    });
  });
});
