// Cookie + CSRF alapú HTTP kliens a Phase 1 ASP.NET Core backendhez.
// Bearer tokent nem használunk. Auth és CSRF token SOHA nem kerülhet
// localStorage-be — a session cookie httpOnly, a CSRF a memóriában él.

import { apiBaseUrl, clearCsrfToken, ensureCsrfToken, refreshCsrfToken } from "./csrf";
import { ApiError, mapErrorResponse, type ProblemDetails } from "./errors";

type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

interface RequestOptions {
  method?: Method;
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined | null>;
  headers?: Record<string, string>;
  /** Ha true (mutációknál igaz alapból), előbb CSRF tokent szerez. */
  csrf?: boolean;
  /** Ha 401 érkezik, hova irányítsunk. Alap: /login. */
  onUnauthenticated?: () => void;
}

export function buildUrl(path: string, query?: RequestOptions["query"]): string {
  const absolute = /^https?:\/\//i.test(path);
  const combined = absolute ? path : apiBaseUrl() + path;
  const url = new URL(combined, "http://relative-api.invalid");
  if (query) {
    for (const [k, v] of Object.entries(query)) {
      if (v === undefined || v === null) continue;
      url.searchParams.set(k, String(v));
    }
  }
  return absolute || apiBaseUrl() ? url.toString() : `${url.pathname}${url.search}${url.hash}`;
}

async function parseBody(res: Response): Promise<unknown> {
  const ct = res.headers.get("content-type") ?? "";
  if (res.status === 204) return null;
  if (ct.includes("application/json")) return res.json();
  const text = await res.text();
  return text ? { detail: text } : null;
}

function defaultOnUnauthenticated() {
  if (typeof window === "undefined") return;
  if (window.location.pathname !== "/login") {
    window.location.replace("/login");
  }
}

/** Alacsony szintű fetch — hibaleképezéssel + egyszeri CSRF retry-jel. */
export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method: Method = options.method ?? "GET";
  const isMutation = method !== "GET";
  const wantsCsrf = options.csrf ?? isMutation;

  const doFetch = async (retriedCsrf: boolean, retryToken?: string): Promise<T> => {
    const headers: Record<string, string> = {
      Accept: "application/json",
      ...options.headers,
    };
    if (options.body !== undefined) headers["Content-Type"] = "application/json";
    const csrfToken = wantsCsrf ? (retryToken ?? (await ensureCsrfToken())) : undefined;
    if (csrfToken) headers["X-CSRF-TOKEN"] = csrfToken;

    let res: Response;
    try {
      res = await fetch(buildUrl(path, options.query), {
        method,
        credentials: "include",
        headers,
        body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      });
    } catch (e) {
      throw new ApiError("NETWORK", e instanceof Error ? e.message : "Hálózati hiba", 0);
    }

    if (res.ok) {
      return (await parseBody(res)) as T;
    }

    const body = (await parseBody(res).catch(() => undefined)) as ProblemDetails | undefined;
    const err = mapErrorResponse(res.status, body);

    // 401: session ürítés + login redirect
    if (err.code === "UNAUTHENTICATED") {
      clearCsrfToken();
      (options.onUnauthenticated ?? defaultOnUnauthenticated)();
      throw err;
    }

    // 403 INVALID_CSRF_TOKEN → egyszer új tokent kérünk és újrapróbáljuk.
    if (err.code === "INVALID_CSRF_TOKEN" && !retriedCsrf) {
      const refreshedToken = await refreshCsrfToken(csrfToken ?? "");
      return doFetch(true, refreshedToken);
    }

    if (err.code === "INVALID_CSRF_TOKEN") {
      throw new ApiError(
        "INVALID_CSRF_TOKEN",
        "A biztonsági munkamenet lejárt. Frissítsd az oldalt, majd próbáld újra.",
        err.status,
        { serverCode: err.serverCode },
      );
    }

    throw err;
  };

  return doFetch(false);
}

export const httpClient = {
  get: <T>(path: string, query?: RequestOptions["query"]) =>
    apiFetch<T>(path, { method: "GET", query }),
  post: <T>(path: string, body?: unknown, headers?: Record<string, string>) =>
    apiFetch<T>(path, { method: "POST", body, headers }),
  put: <T>(path: string, body?: unknown) => apiFetch<T>(path, { method: "PUT", body }),
  del: <T>(path: string) => apiFetch<T>(path, { method: "DELETE" }),
};
