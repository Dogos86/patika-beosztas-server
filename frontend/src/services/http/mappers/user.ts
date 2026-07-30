import type {
  UpdateUserEmployeeLinkRequestDto,
  UpdateUserPermissionsRequestDto,
  UpdateUserStatusRequestDto,
  UserCreateRequestDto,
  UserResponseDto,
} from "../dto";
import type { AdminUserSummary, AppPermission } from "@/services/types";
import { mapLinkedEmployeeFromBackend } from "./session";

export function mapUserFromBackend(dto: UserResponseDto): AdminUserSummary {
  return {
    id: dto.id,
    organizationId: dto.organizationId ?? undefined,
    email: dto.email,
    displayName: dto.displayName,
    active: dto.isActive,
    permissions: [...dto.permissions],
    linkedEmployee: dto.linkedEmployee ? mapLinkedEmployeeFromBackend(dto.linkedEmployee) : null,
    createdAt: dto.createdAtUtc,
    version: dto.version,
  };
}

export function mapCreateUserRequest(input: {
  email: string;
  displayName: string;
  initialPassword: string;
  permissions: AppPermission[];
  linkedEmployeeId?: string | null;
}): UserCreateRequestDto {
  return {
    email: input.email,
    displayName: input.displayName,
    initialPassword: input.initialPassword,
    permissions: [...input.permissions],
    employeeId: input.linkedEmployeeId ?? null,
  };
}

export function mapUpdatePermissionsRequest(input: {
  permissions: AppPermission[];
  expectedVersion: number;
}): UpdateUserPermissionsRequestDto {
  return { permissions: [...input.permissions], expectedVersion: input.expectedVersion };
}

export function mapUpdateEmployeeLinkRequest(input: {
  linkedEmployeeId: string | null;
  expectedVersion: number;
}): UpdateUserEmployeeLinkRequestDto {
  return { employeeId: input.linkedEmployeeId, expectedVersion: input.expectedVersion };
}

export function mapUpdateStatusRequest(input: {
  active: boolean;
  expectedVersion: number;
}): UpdateUserStatusRequestDto {
  return { isActive: input.active, expectedVersion: input.expectedVersion };
}
