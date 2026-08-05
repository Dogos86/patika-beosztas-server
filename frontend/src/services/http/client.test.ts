import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { apiFetch, buildUrl } from "./client";
import { clearCsrfToken } from "./csrf";
import { httpServices } from "./index";

const originalEnv = import.meta.env.VITE_API_URL;

function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json", ...headers },
  });
}

describe("apiFetch CSRF + 401", () => {
  beforeEach(() => {
    (import.meta.env as Record<string, string>).VITE_API_URL = "http://api.test";
    clearCsrfToken();
  });
  afterEach(() => {
    (import.meta.env as Record<string, string>).VITE_API_URL = originalEnv ?? "";
    vi.restoreAllMocks();
  });

  it("400 + INVALID_CSRF_TOKEN után egyszer frissít és újrapróbál", async () => {
    let call = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      call++;
      if (url.endsWith("/api/auth/csrf")) return jsonResponse(200, { requestToken: `t${call}` });
      if (call === 2) return jsonResponse(400, { code: "INVALID_CSRF_TOKEN" });
      return jsonResponse(200, { ok: true });
    });
    vi.stubGlobal("fetch", fetchMock);

    const res = await apiFetch<{ ok: boolean }>("/api/things", { method: "POST", body: {} });
    expect(res.ok).toBe(true);
    // CSRF-et kétszer szerezte meg (egyszer, majd frissítés retry-hoz)
    const csrfCalls = fetchMock.mock.calls.filter((c) => String(c[0]).endsWith("/api/auth/csrf"));
    expect(csrfCalls.length).toBe(2);
    const mutationCalls = fetchMock.mock.calls.filter(
      (c) => !String(c[0]).endsWith("/api/auth/csrf"),
    );
    expect(mutationCalls.length).toBe(2);
  });

  it("a sikertelen automatikus újrapróbálás után magyar munkamenet-hibát ad", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) =>
      String(input).endsWith("/api/auth/csrf")
        ? jsonResponse(200, { requestToken: "t" })
        : jsonResponse(400, { code: "INVALID_CSRF_TOKEN" }),
    );
    vi.stubGlobal("fetch", fetchMock);

    await expect(apiFetch("/api/things", { method: "POST", body: {} })).rejects.toThrow(
      "A biztonsági munkamenet lejárt. Frissítsd az oldalt, majd próbáld újra.",
    );
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });

  it("párhuzamos INVALID_CSRF_TOKEN válaszok egyetlen frissítési promise-t használnak", async () => {
    let csrfCalls = 0;
    let staleMutations = 0;
    let releaseStaleMutations!: () => void;
    const bothStaleMutationsStarted = new Promise<void>((resolve) => {
      releaseStaleMutations = resolve;
    });
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith("/api/auth/csrf")) {
        csrfCalls++;
        return jsonResponse(200, { requestToken: csrfCalls === 1 ? "stale" : "fresh" });
      }

      const token = new Headers(init?.headers).get("X-CSRF-TOKEN");
      if (token === "stale") {
        staleMutations++;
        if (staleMutations === 2) releaseStaleMutations();
        await bothStaleMutationsStarted;
        return jsonResponse(400, { code: "INVALID_CSRF_TOKEN" });
      }
      return jsonResponse(200, { ok: true });
    });
    vi.stubGlobal("fetch", fetchMock);

    const results = await Promise.all([
      apiFetch<{ ok: boolean }>("/api/one", { method: "POST", body: {} }),
      apiFetch<{ ok: boolean }>("/api/two", { method: "POST", body: {} }),
    ]);

    expect(results.every((result) => result.ok)).toBe(true);
    expect(csrfCalls).toBe(2);
    expect(staleMutations).toBe(2);
  });

  it("a később beérkező CSRF-hiba a közben már frissített tokent használja", async () => {
    let csrfCalls = 0;
    let staleMutations = 0;
    let releaseDelayedFailure!: () => void;
    const refreshed = new Promise<void>((resolve) => {
      releaseDelayedFailure = resolve;
    });
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith("/api/auth/csrf")) {
        csrfCalls++;
        if (csrfCalls === 2) releaseDelayedFailure();
        return jsonResponse(200, { requestToken: csrfCalls === 1 ? "stale" : "fresh" });
      }

      const token = new Headers(init?.headers).get("X-CSRF-TOKEN");
      if (token === "stale") {
        staleMutations++;
        if (staleMutations === 2) await refreshed;
        return jsonResponse(400, { code: "INVALID_CSRF_TOKEN" });
      }
      return jsonResponse(200, { ok: true });
    });
    vi.stubGlobal("fetch", fetchMock);

    const results = await Promise.all([
      apiFetch<{ ok: boolean }>("/api/one", { method: "POST", body: {} }),
      apiFetch<{ ok: boolean }>("/api/two", { method: "POST", body: {} }),
    ]);

    expect(results.every((result) => result.ok)).toBe(true);
    expect(csrfCalls).toBe(2);
  });

  it("login és logout után törli a memóriában tárolt tokent", async () => {
    let csrfCalls = 0;
    const mutationTokens: string[] = [];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith("/api/auth/csrf")) {
        csrfCalls++;
        return jsonResponse(200, { requestToken: `t${csrfCalls}` });
      }
      mutationTokens.push(new Headers(init?.headers).get("X-CSRF-TOKEN") ?? "");
      if (url.endsWith("/api/auth/logout")) return new Response(null, { status: 204 });
      return jsonResponse(200, {
        userId: "u1",
        organizationId: "o1",
        email: "admin@example.test",
        displayName: "Admin",
        isActive: true,
        permissions: [],
        linkedEmployee: null,
      });
    });
    vi.stubGlobal("fetch", fetchMock);

    await httpServices.auth.login("admin@example.test", "secret");
    await httpServices.auth.logout();
    await apiFetch("/api/after-logout", { method: "POST", body: {} });

    expect(csrfCalls).toBe(3);
    expect(mutationTokens).toEqual(["t1", "t2", "t3"]);
  });

  it("401 esetén redirect a /login-ra és ApiError dob", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).endsWith("/api/auth/csrf")) return jsonResponse(200, { requestToken: "t" });
      return jsonResponse(401, { detail: "nope" });
    });
    vi.stubGlobal("fetch", fetchMock);
    const redirect = vi.fn();
    await expect(
      apiFetch("/api/x", { method: "GET", onUnauthenticated: redirect }),
    ).rejects.toMatchObject({ code: "UNAUTHENTICATED", status: 401 });
    expect(redirect).toHaveBeenCalled();
  });
});

describe("relative production API path", () => {
  afterEach(() => {
    (import.meta.env as Record<string, string>).VITE_API_URL = originalEnv ?? "";
  });

  it("uses the same-origin /api path when VITE_API_URL is empty", () => {
    (import.meta.env as Record<string, string>).VITE_API_URL = "";

    expect(buildUrl("/api/me/schedule", { date: "2026-07-30" })).toBe(
      "/api/me/schedule?date=2026-07-30",
    );
  });
});
