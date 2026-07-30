// Explicit backend DTO alakok. A frontend típusok külön élnek (services/types.ts).
// A mapperek felelősek a fordításért — sose castoljunk közvetlenül UI típusra.

import type { BackendLocationType, BackendPermission, BackendProfessionalRole } from "./enums";

export interface PagedResponseDto<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface LinkedEmployeeDto {
  id: string;
  displayName: string;
  professionalRole: BackendProfessionalRole;
  isActive: boolean;
  isSchedulable: boolean;
}

/** OpenAPI: LinkedUserSummary — az EmployeeResponse.linkedUser mező alakja. */
export interface LinkedUserSummaryDto {
  userId: string;
  email: string;
  displayName: string;
  isActive: boolean;
}

export interface SessionResponseDto {
  userId: string;
  organizationId?: string | null;
  email: string;
  displayName: string;
  isActive?: boolean;
  permissions: BackendPermission[];
  linkedEmployee?: LinkedEmployeeDto | null;
}

export interface EmployeeLocationRefDto {
  locationId: string;
  locationName: string;
  enabled: boolean;
}

export interface EmployeeLocationRequestDto {
  locationId: string;
  enabled?: boolean;
}

export interface EmployeeResponseDto {
  id: string;
  fullName: string;
  displayName: string;
  professionalRole: BackendProfessionalRole;
  isActive: boolean;
  isSchedulable: boolean;
  includeInAutoFill: boolean;
  countsAsPharmacist: boolean;
  /** Havi munkaidőcél percben (backend kanonikus egység); a wire nullable. */
  monthlyMinutesLimit: number | null;
  maxDailyMinutes: number | null;
  birthDate: string | null;
  externalPayrollId: string | null;
  locations: EmployeeLocationRefDto[];
  timeWindows: BackendTimeWindowDto[];
  allowedTimeTypes: string[];
  linkedUser: LinkedUserSummaryDto | null;
  warnings?: string[];
  version: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface BackendTimeWindowDto {
  /** Backend: dayOfWeek (Every | Mon ... Sun). */
  dayOfWeek: string;
  /** "HH:mm" alak. */
  startTime: string;
  endTime: string;
  /** Preferred | Blocked. */
  type: "Preferred" | "Blocked";
}

export interface EmployeeCreateRequestDto {
  fullName: string;
  displayName: string;
  professionalRole: BackendProfessionalRole;
  isActive: boolean;
  isSchedulable: boolean;
  includeInAutoFill: boolean;
  countsAsPharmacist: boolean;
  monthlyMinutesLimit: number | null;
  maxDailyMinutes: number | null;
  birthDate: string | null;
  externalPayrollId: string | null;
  locations: EmployeeLocationRequestDto[];
  timeWindows: BackendTimeWindowDto[];
  allowedTimeTypes: string[];
}

export interface EmployeeUpdateRequestDto extends EmployeeCreateRequestDto {
  expectedVersion: number;
}

export interface LocationResponseDto {
  id: string;
  name: string;
  type: BackendLocationType;
  address?: string | null;
  isActive: boolean;
  version: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface LocationCreateRequestDto {
  name: string;
  type: BackendLocationType;
  isActive: boolean;
  address?: string | null;
}

export interface LocationUpdateRequestDto extends LocationCreateRequestDto {
  expectedVersion: number;
}

export interface UserResponseDto {
  /** OpenAPI: id (nem userId). */
  id: string;
  organizationId?: string | null;
  email: string;
  displayName: string;
  isActive: boolean;
  permissions: BackendPermission[];
  linkedEmployee?: LinkedEmployeeDto | null;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  version: number;
}

export interface UserCreateRequestDto {
  email: string;
  displayName: string;
  initialPassword: string;
  permissions: BackendPermission[];
  /** OpenAPI: employeeId (nem linkedEmployeeId). */
  employeeId?: string | null;
  isActive?: boolean;
}

export interface UpdateUserPermissionsRequestDto {
  permissions: BackendPermission[];
  /** OpenAPI szerint kötelező. */
  expectedVersion: number;
}

export interface UpdateUserEmployeeLinkRequestDto {
  employeeId: string | null;
  expectedVersion: number;
}

export interface UpdateUserStatusRequestDto {
  isActive: boolean;
  expectedVersion: number;
}
