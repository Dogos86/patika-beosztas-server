import type { AppPermission } from "@/services/types";

export function hasAnyPermission(
  userPerms: readonly AppPermission[] | undefined,
  required: readonly AppPermission[],
): boolean {
  if (!userPerms || userPerms.length === 0) return false;
  if (required.length === 0) return true;
  return required.some((p) => userPerms.includes(p));
}

export function hasAllPermissions(
  userPerms: readonly AppPermission[] | undefined,
  required: readonly AppPermission[],
): boolean {
  if (!userPerms) return false;
  return required.every((p) => userPerms.includes(p));
}
