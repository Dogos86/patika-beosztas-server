import { describe, it, expect } from "vitest";
import { withExpectedVersion } from "./expected-version";

describe("withExpectedVersion", () => {
  it("hozzáadja a version mezőt", () => {
    expect(withExpectedVersion({ name: "A" }, 3)).toEqual({ name: "A", expectedVersion: 3 });
  });
  it("undefined esetén nem ad hozzá mezőt", () => {
    expect(withExpectedVersion({ name: "A" }, undefined)).toEqual({ name: "A" });
  });
});
