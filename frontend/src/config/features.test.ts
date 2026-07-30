import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { resolveFrontendFeatures } from "./features";

describe("pilot frontend configuration", () => {
  it("only accepts real API with demo, AI and notifications disabled", () => {
    expect(
      resolveFrontendFeatures({
        VITE_APP_ENV: "pilot",
        VITE_DATA_SOURCE: "api",
        VITE_ENABLE_DEMO_LOGIN: "false",
        VITE_ENABLE_AI: "false",
        VITE_ENABLE_NOTIFICATIONS: "false",
      }),
    ).toEqual({
      isPilot: true,
      demoLoginEnabled: false,
      aiEnabled: false,
      notificationsEnabled: false,
    });
  });

  it.each([
    ["mock", "false", "false", "false"],
    ["api", "true", "false", "false"],
    ["api", "false", "true", "false"],
    ["api", "false", "false", "true"],
  ])("rejects unsafe pilot feature combination", (source, demo, ai, notifications) => {
    expect(() =>
      resolveFrontendFeatures({
        VITE_APP_ENV: "pilot",
        VITE_DATA_SOURCE: source,
        VITE_ENABLE_DEMO_LOGIN: demo,
        VITE_ENABLE_AI: ai,
        VITE_ENABLE_NOTIFICATIONS: notifications,
      }),
    ).toThrow(/Hibás pilot konfiguráció/);
  });

  it("contains no demo credential or demo login action in the login route", () => {
    const source = readFileSync(resolve(process.cwd(), "src/routes/login.tsx"), "utf8");
    expect(source).not.toMatch(/dolgozo@patika\.hu|admin@patika\.hu|loginDemo|jelszó: demo/);
  });

  it("does not import mock services into the pilot service locator", () => {
    const source = readFileSync(resolve(process.cwd(), "src/services/index.ts"), "utf8");
    expect(source).not.toMatch(/from\s+["']\.\/mock["']/);
    expect(source).toMatch(/export const services: Services = httpServices/);
  });
});
