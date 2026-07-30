import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { apiFetch, buildUrl } from "./client";
import { clearCsrfToken } from "./csrf";

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
