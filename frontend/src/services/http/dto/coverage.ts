// Coverage requirement DTO-k a Phase 2E OpenAPI szerződéshez.
// Numeric mezők a wire-en `integer | string` alakúak lehetnek (uint32/int32),
// ezért a mapperek `Number(...)` normalizálást végeznek.

import type { BackendDayOfWeek, BackendStaffingCapability } from "./enums";

export type BackendCoverageSeverity = "Warning" | "Blocking";

export interface CoverageRequirementResponseDto {
  id: string;
  locationId: string;
  locationName: string;
  locationIsActive: boolean;
  dayOfWeek: BackendDayOfWeek;
  startTime: string;
  endTime: string;
  requiredCapability: BackendStaffingCapability;
  requiredCount: number | string;
  severity: BackendCoverageSeverity;
  isActive: boolean;
  warnings: string[];
  version: number | string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface CreateCoverageRequirementRequestDto {
  locationId: string;
  dayOfWeek: BackendDayOfWeek;
  startTime: string;
  endTime: string;
  requiredCapability: BackendStaffingCapability;
  requiredCount: number;
  severity: BackendCoverageSeverity;
  isActive: boolean;
}

export interface UpdateCoverageRequirementRequestDto extends CreateCoverageRequirementRequestDto {
  expectedVersion: number;
}

export interface DeactivateCoverageRequirementRequestDto {
  expectedVersion: number;
}
