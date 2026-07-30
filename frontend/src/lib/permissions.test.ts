import { describe, it, expect } from "vitest";
import { hasAnyPermission, hasAllPermissions } from "./permissions";

describe("permission guard", () => {
  it("hasAnyPermission igaz, ha van egyezés", () => {
    expect(hasAnyPermission(["ManageSchedules"], ["ManageSchedules", "ManageUsers"])).toBe(true);
  });
  it("hasAnyPermission hamis üres user perms esetén", () => {
    expect(hasAnyPermission([], ["ManageUsers"])).toBe(false);
    expect(hasAnyPermission(undefined, ["ManageUsers"])).toBe(false);
  });
  it("hasAllPermissions minden szükséges perm kell", () => {
    expect(hasAllPermissions(["ManageEmployees"], ["ManageEmployees", "ManageUsers"])).toBe(false);
    expect(
      hasAllPermissions(["ManageEmployees", "ManageUsers"], ["ManageEmployees", "ManageUsers"]),
    ).toBe(true);
  });
});
