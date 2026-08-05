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
  // A force refresh-eknek is ugyanazt a folyamatban levő kérést kell
  // megosztaniuk. Különben két párhuzamos INVALID_CSRF_TOKEN válasz két
  // cookie/token párt kérhet, és az utolsó Set-Cookie érvényteleníti a másik
  // kéréshez tartozó request tokent.
  if (inflight) return inflight;
  if (!force && cached) return cached;

  if (force) cached = null;
  const request = (async () => {
    const res = await fetch(`${apiBaseUrl()}/api/auth/csrf`, {
      method: "GET",
      credentials: "include",
      headers: { Accept: "application/json" },
      cache: "no-store",
    });
    if (!res.ok) throw new Error(`CSRF token nem szerezhető meg (${res.status}).`);
    const body = (await res.json()) as { requestToken?: string };
    if (!body.requestToken) throw new Error("Hibás CSRF válasz.");
    cached = body.requestToken;
    return cached;
  })();
  inflight = request;
  try {
    return await request;
  } finally {
    if (inflight === request) inflight = null;
  }
}

/**
 * INVALID_CSRF_TOKEN után csak akkor indít új hálózati frissítést, ha közben
 * egy másik kérés még nem cserélte le az elutasított tokent.
 */
export function refreshCsrfToken(rejectedToken: string): Promise<string> {
  if (cached && cached !== rejectedToken) return Promise.resolve(cached);
  return ensureCsrfToken(true);
}

export function clearCsrfToken() {
  cached = null;
}
