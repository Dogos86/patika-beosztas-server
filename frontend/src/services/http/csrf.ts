// CSRF token szerzés és cache. A backend `GET /api/auth/csrf`
// válaszban ad egy `requestToken`-t, amit minden mutációnál az
// `X-CSRF-TOKEN` headerben vissza kell küldeni.

let cached: string | null = null;
let inflight: Promise<string> | null = null;

export function apiBaseUrl(): string {
  const url = import.meta.env.VITE_API_URL ?? "";
  return url.replace(/\/$/, "");
}

/** Lekéri és cache-eli a CSRF tokent. `force=true` esetén friss tokent kér. */
export async function ensureCsrfToken(force = false): Promise<string> {
  if (!force && cached) return cached;
  if (!force && inflight) return inflight;
  inflight = (async () => {
    const res = await fetch(`${apiBaseUrl()}/api/auth/csrf`, {
      method: "GET",
      credentials: "include",
      headers: { Accept: "application/json" },
    });
    if (!res.ok) throw new Error(`CSRF token nem szerezhető meg (${res.status}).`);
    const body = (await res.json()) as { requestToken?: string };
    if (!body.requestToken) throw new Error("Hibás CSRF válasz.");
    cached = body.requestToken;
    return cached;
  })();
  try {
    return await inflight;
  } finally {
    inflight = null;
  }
}

export function clearCsrfToken() {
  cached = null;
}
