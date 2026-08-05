import { describe, expect, it } from "vitest";
import { buildDownstreamHeaders, buildUpstreamHeaders } from "./gateway-headers.mjs";

describe("Railway gateway headers", () => {
  it("változtatás nélkül továbbítja a Cookie fejlécet", () => {
    const headers = buildUpstreamHeaders(
      {
        host: "web.example.test",
        cookie: "__Host-PatikaSession=session; __Host-PatikaCsrf=csrf",
      },
      new URL("http://api.railway.internal:8080"),
      true,
      "127.0.0.1",
    );

    expect(headers.cookie).toBe("__Host-PatikaSession=session; __Host-PatikaCsrf=csrf");
    expect(headers.host).toBe("api.railway.internal:8080");
  });

  it("külön Set-Cookie fejléceket őriz meg és a CSRF választ nem cache-elteti", () => {
    const setCookies = [
      "__Host-PatikaSession=session; Path=/; Secure; HttpOnly",
      "__Host-PatikaCsrf=csrf; Path=/; Secure; HttpOnly",
    ];
    const headers = buildDownstreamHeaders(
      { "set-cookie": setCookies, "cache-control": "public, max-age=300" },
      "/api/auth/csrf",
    );

    expect(headers["set-cookie"]).toBe(setCookies);
    expect(headers["cache-control"]).toBe("no-store");
    expect(headers.pragma).toBe("no-cache");
    expect(headers.expires).toBe("0");
  });
});
