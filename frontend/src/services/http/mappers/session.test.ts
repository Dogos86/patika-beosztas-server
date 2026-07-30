import { describe, it, expect } from "vitest";
import { mapSessionFromBackend } from "./session";

describe("mapSessionFromBackend", () => {
  it("userId → id, isActive → active, snake linkedEmployee", () => {
    const u = mapSessionFromBackend({
      userId: "u1",
      email: "a@b.hu",
      displayName: "A",
      isActive: true,
      permissions: ["ManageUsers"],
      linkedEmployee: {
        id: "e1",
        displayName: "E",
        professionalRole: "Pharmacist",
        isActive: true,
        isSchedulable: false,
      },
    });
    expect(u.id).toBe("u1");
    expect(u.active).toBe(true);
    expect(u.linkedEmployee?.professionalRole).toBe("pharmacist");
    expect(u.linkedEmployee?.schedulable).toBe(false);
  });
  it("null linkedEmployee", () => {
    const u = mapSessionFromBackend({
      userId: "u2",
      email: "x@y.hu",
      displayName: "X",
      permissions: [],
      linkedEmployee: null,
    });
    expect(u.linkedEmployee).toBeNull();
  });
});
