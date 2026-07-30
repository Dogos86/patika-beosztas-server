import { describe, it, expect } from "vitest";
import { mapUpdateStatusRequest, mapUpdatePermissionsRequest, mapUserFromBackend } from "./user";

describe("user mapper", () => {
  it("userId → id, isActive → active, permissions listát átveszi", () => {
    const u = mapUserFromBackend({
      id: "u1",
      email: "a@b.hu",
      displayName: "A",
      isActive: false,
      permissions: ["ManageUsers", "ManageEmployees"],
      linkedEmployee: null,
      version: 4,
    });
    expect(u.id).toBe("u1");
    expect(u.active).toBe(false);
    expect(u.permissions).toContain("ManageUsers");
    expect(u.version).toBe(4);
  });
  it("status request isActive kulcsot használ, expectedVersion továbbadva", () => {
    const req = mapUpdateStatusRequest({ active: true, expectedVersion: 9 });
    expect(req).toEqual({ isActive: true, expectedVersion: 9 });
  });
  it("permissions request kötelezően továbbadja az expectedVersion értékét", () => {
    const req = mapUpdatePermissionsRequest({ permissions: ["ManageUsers"], expectedVersion: 3 });
    expect(req.expectedVersion).toBe(3);
    expect(req.permissions).toEqual(["ManageUsers"]);
  });
});
