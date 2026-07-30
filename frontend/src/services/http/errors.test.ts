import { describe, it, expect } from "vitest";
import { mapErrorResponse } from "./errors";

describe("mapErrorResponse", () => {
  it("401 → UNAUTHENTICATED", () => {
    const err = mapErrorResponse(401, { title: "no" });
    expect(err.code).toBe("UNAUTHENTICATED");
    expect(err.status).toBe(401);
  });

  it("400 + INVALID_CSRF_TOKEN → INVALID_CSRF_TOKEN", () => {
    const err = mapErrorResponse(400, { code: "INVALID_CSRF_TOKEN" });
    expect(err.code).toBe("INVALID_CSRF_TOKEN");
  });

  it("403 + INVALID_CSRF_TOKEN → INVALID_CSRF_TOKEN", () => {
    const err = mapErrorResponse(403, { code: "INVALID_CSRF_TOKEN" });
    expect(err.code).toBe("INVALID_CSRF_TOKEN");
  });

  it("422 kiolvassa a fieldErrors-t", () => {
    const err = mapErrorResponse(422, {
      detail: "Hibás",
      errors: { name: ["kötelező"] },
    });
    expect(err.code).toBe("VALIDATION");
    expect(err.fieldErrors?.name).toEqual(["kötelező"]);
  });

  it("409 → CONFLICT (expectedVersion ütközés)", () => {
    const err = mapErrorResponse(409, { code: "CONCURRENCY_CONFLICT" });
    expect(err.code).toBe("CONFLICT");
    expect(err.serverCode).toBe("CONCURRENCY_CONFLICT");
  });

  it("422 array-alakú errors-t normalizál (key/messages)", () => {
    const err = mapErrorResponse(422, {
      errors: [
        { key: "email", messages: ["kötelező"] },
        { field: "email", message: "formátum" },
      ],
    });
    expect(err.fieldErrors?.email).toEqual(["kötelező", "formátum"]);
  });
});
