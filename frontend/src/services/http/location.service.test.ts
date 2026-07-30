import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { httpServices } from "./index";
import { clearCsrfToken } from "./csrf";
import { ApiError } from "./errors";
import { defaultOpeningHours, twentyFourDay } from "@/lib/opening-hours";

const originalEnv = import.meta.env.VITE_API_URL;

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

const locDto = {
  id: "l1",
  name: "Fiók",
  type: "Branch",
  address: null,
  isActive: true,
  version: 2,
};

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

describe("http location service", () => {
  beforeEach(() => {
    (import.meta.env as Record<string, string>).VITE_API_URL = "http://api.test";
    clearCsrfToken();
  });
  afterEach(() => {
    (import.meta.env as Record<string, string>).VITE_API_URL = originalEnv ?? "";
    vi.restoreAllMocks();
  });

  it("listPaged a lapozott választ normalizálja", async () => {
    const calls = stubFetch(() =>
      json(200, { items: [locDto], totalCount: 42, page: 2, pageSize: 20 }),
    );
    const paged = await httpServices.location.listPaged({ page: 2, pageSize: 20, search: "fi" });
    expect(paged.total).toBe(42);
    expect(paged.items[0].name).toBe("Fiók");
    expect(calls[0].url).toContain("page=2");
    expect(calls[0].url).toContain("search=fi");
  });

  it("listAll több oldalon lapoz és nem esik vissza mockra", async () => {
    let page = 0;
    const calls = stubFetch(() => {
      page++;
      return page === 1
        ? json(200, {
            items: [locDto, { ...locDto, id: "l2" }],
            totalCount: 3,
            page: 1,
            pageSize: 2,
          })
        : json(200, { items: [{ ...locDto, id: "l3" }], totalCount: 3, page: 2, pageSize: 2 });
    });
    const all = await httpServices.location.listAll({ maxItems: 4, pageSize: 2 });
    expect(all.map((l) => l.id)).toEqual(["l1", "l2", "l3"]);
    expect(calls.length).toBe(2);
  });

  it("create POST-ol, nem ID-prefix alapján dönt", async () => {
    const calls = stubFetch(() => json(200, locDto));
    await httpServices.location.create({
      name: "Fiók",
      kind: "branch",
      address: null,
      active: true,
    });
    expect(calls[0].method).toBe("POST");
    expect(calls[0].body).toEqual({ name: "Fiók", type: "Branch", address: null, isActive: true });
  });

  it("update PUT-ol expectedVersion-nel", async () => {
    const calls = stubFetch(() => json(200, { ...locDto, version: 3 }));
    const loc = await httpServices.location.update(
      "l1",
      { name: "Fiók 2", kind: "branch", address: "X", active: false },
      2,
    );
    expect(calls[0].method).toBe("PUT");
    expect(calls[0].url).toContain("/api/admin/locations/l1");
    expect(calls[0].body).toMatchObject({ expectedVersion: 2, isActive: false, address: "X" });
    expect(loc.version).toBe(3);
  });

  it("409 esetén CONFLICT ApiError jön, nincs hamis siker", async () => {
    stubFetch(() => json(409, { title: "Conflict" }));
    await expect(
      httpServices.location.update("l1", { name: "A", kind: "branch", active: true }, 1),
    ).rejects.toMatchObject({ code: "CONFLICT" });
  });

  it("weekly opening GET és PUT", async () => {
    const calls = stubFetch((c) =>
      json(200, {
        id: "o1",
        locationId: "l1",
        locationName: "Fiók",
        locationIsActive: true,
        warnings: [],
        version: c.method === "GET" ? 1 : 2,
        days: [{ dayOfWeek: "Monday", mode: "Open24Hours", intervals: [] }],
      }),
    );
    const current = await httpServices.location.getWeeklyOpening("l1");
    expect(current?.version).toBe(1);
    expect(current?.hours.mon.mode).toBe("twentyFour");

    const hours = { ...defaultOpeningHours(), tue: twentyFourDay() };
    const saved = await httpServices.location.updateWeeklyOpening("l1", hours, 1);
    expect(saved.version).toBe(2);
    expect(calls[1].method).toBe("PUT");
    expect(calls[1].url).toContain("/api/admin/locations/l1/weekly-opening");
    expect(calls[1].body).toMatchObject({ expectedVersion: 1 });
  });

  it("weekly opening 404 → null", async () => {
    stubFetch(() => json(404, { title: "Not found" }));
    await expect(httpServices.location.getWeeklyOpening("l1")).resolves.toBeNull();
  });

  it("shift template list / create / update / deactivate", async () => {
    const tpl = {
      id: "t1",
      locationId: "l1",
      locationName: "Fiók",
      category: "Morning",
      name: "Délelőtt",
      weekdays: ["Monday"],
      startTime: "08:00:00",
      endTime: "14:00:00",
      isActive: true,
      requiredCapability: null,
      version: 1,
    };
    const calls = stubFetch((c) => (c.method === "GET" ? json(200, [tpl]) : json(200, tpl)));

    const list = await httpServices.location.listShiftTemplates("l1", true);
    expect(list[0].category).toBe("AM");
    expect(calls[0].url).toContain("/api/admin/locations/l1/shift-templates");

    const input = {
      name: "Délelőtt",
      category: "AM" as const,
      days: ["mon" as const],
      startMin: 480,
      endMin: 840,
      active: true,
    };
    await httpServices.location.createShiftTemplate("l1", input);
    expect(calls[1].method).toBe("POST");

    await httpServices.location.updateShiftTemplate("t1", input, 1);
    expect(calls[2].method).toBe("PUT");
    expect(calls[2].url).toContain("/api/admin/location-shift-templates/t1");
    expect(calls[2].body).toMatchObject({ expectedVersion: 1 });

    await httpServices.location.deactivateShiftTemplate("t1", 1);
    expect(calls[3].method).toBe("POST");
    expect(calls[3].url).toContain("/deactivate");
    expect(calls[3].body).toEqual({ expectedVersion: 1 });
  });

  it("sablon 409 konfliktus ApiError-t dob", async () => {
    stubFetch(() => json(409, { title: "Conflict" }));
    await expect(httpServices.location.deactivateShiftTemplate("t1", 1)).rejects.toBeInstanceOf(
      ApiError,
    );
  });
});
