// A Phase 1 backend HTTP hibáinak típusos leképezése.

export type ApiErrorCode =
  | "UNAUTHENTICATED"
  | "FORBIDDEN"
  | "VALIDATION"
  | "CONFLICT"
  | "INVALID_CSRF_TOKEN"
  | "NOT_FOUND"
  | "SERVER_ERROR"
  | "NETWORK";

export class ApiError extends Error {
  code: ApiErrorCode;
  status: number;
  fieldErrors?: Record<string, string[]>;
  fieldErrorCodes?: Record<string, string[]>;
  serverCode?: string;
  constructor(
    code: ApiErrorCode,
    message: string,
    status: number,
    extra?: {
      fieldErrors?: Record<string, string[]>;
      fieldErrorCodes?: Record<string, string[]>;
      serverCode?: string;
    },
  ) {
    super(message);
    this.name = "ApiError";
    this.code = code;
    this.status = status;
    this.fieldErrors = extra?.fieldErrors;
    this.fieldErrorCodes = extra?.fieldErrorCodes;
    this.serverCode = extra?.serverCode;
  }
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  /** RFC7807: `errors` lehet objektum (mező → üzenetek) VAGY tömb
   *  (`{ key, messages }` alakú), így a normalizáció mindkettőt kezeli. */
  errors?:
    | Record<string, string[]>
    | Array<{
        key?: string;
        field?: string;
        code?: string;
        messages?: string[];
        message?: string;
      }>;
}

/** ProblemDetails.errors → normalizált `Record<string, string[]>`. */
export function normalizeFieldErrors(
  errors: ProblemDetails["errors"],
): Record<string, string[]> | undefined {
  if (!errors) return undefined;
  if (Array.isArray(errors)) {
    const out: Record<string, string[]> = {};
    for (const item of errors) {
      const key = item.key ?? item.field ?? "_";
      const msgs = item.messages ?? (item.message ? [item.message] : []);
      if (msgs.length === 0) continue;
      out[key] = [...(out[key] ?? []), ...msgs];
    }
    return Object.keys(out).length ? out : undefined;
  }
  return errors;
}

export function normalizeFieldErrorCodes(
  errors: ProblemDetails["errors"],
): Record<string, string[]> | undefined {
  if (!Array.isArray(errors)) return undefined;
  const out: Record<string, string[]> = {};
  for (const item of errors) {
    if (!item.code) continue;
    const key = item.key ?? item.field ?? "_";
    out[key] = [...(out[key] ?? []), item.code];
  }
  return Object.keys(out).length ? out : undefined;
}

/** HTTP státusz → ApiError (Problem Details minta szerint). */
export function mapErrorResponse(status: number, body: ProblemDetails | undefined): ApiError {
  const message = body?.detail ?? body?.title ?? "Ismeretlen hiba";
  const serverCode = body?.code;
  switch (status) {
    case 401:
      return new ApiError("UNAUTHENTICATED", "Nincs bejelentkezve.", 401, { serverCode });
    case 400:
      if (serverCode === "INVALID_CSRF_TOKEN") {
        return new ApiError(
          "INVALID_CSRF_TOKEN",
          "A biztonsági munkamenet lejárt. Frissítsd az oldalt, majd próbáld újra.",
          400,
          { serverCode },
        );
      }
      return new ApiError("VALIDATION", message, 400, {
        fieldErrors: normalizeFieldErrors(body?.errors),
        fieldErrorCodes: normalizeFieldErrorCodes(body?.errors),
        serverCode,
      });
    case 403:
      if (serverCode === "INVALID_CSRF_TOKEN") {
        return new ApiError(
          "INVALID_CSRF_TOKEN",
          "A biztonsági munkamenet lejárt. Frissítsd az oldalt, majd próbáld újra.",
          403,
          { serverCode },
        );
      }
      return new ApiError("FORBIDDEN", "Nincs jogosultságod ehhez a művelethez.", 403, {
        serverCode,
      });
    case 404:
      return new ApiError("NOT_FOUND", message, 404, { serverCode });
    case 409:
      return new ApiError(
        "CONFLICT",
        "Konkurens módosítás történt. Töltsd újra az adatokat.",
        409,
        { serverCode },
      );
    case 422:
      return new ApiError("VALIDATION", message, 422, {
        fieldErrors: normalizeFieldErrors(body?.errors),
        fieldErrorCodes: normalizeFieldErrorCodes(body?.errors),
        serverCode,
      });
    default:
      return new ApiError("SERVER_ERROR", message, status, { serverCode });
  }
}
