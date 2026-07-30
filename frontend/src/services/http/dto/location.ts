// Telephely törzsadat DTO-k (Phase 2E.5) — openapi.phase2d.json szerint.
// A numerikus mezők a wire-en `integer | string` alakúak lehetnek.

import type { BackendDayOfWeek, BackendStaffingCapability } from "./enums";

export type BackendOpeningDayMode = "Closed" | "Open24Hours" | "CustomIntervals";
export type BackendShiftTemplateCategory = "Morning" | "Afternoon" | "Long" | "Custom";

export interface OpeningIntervalResponseDto {
  id: string;
  startTime: string;
  endTime: string | null;
}

export interface OpeningIntervalRequestDto {
  startTime: string;
  endTime: string | null;
}

export interface OpeningDayResponseDto {
  dayOfWeek: BackendDayOfWeek;
  mode: BackendOpeningDayMode;
  intervals: OpeningIntervalResponseDto[];
}

export interface OpeningDayRequestDto {
  dayOfWeek: BackendDayOfWeek;
  mode: BackendOpeningDayMode;
  intervals: OpeningIntervalRequestDto[];
}

export interface LocationWeeklyOpeningResponseDto {
  id: string;
  locationId: string;
  locationName: string;
  locationIsActive: boolean;
  days: OpeningDayResponseDto[];
  warnings: string[];
  version: number | string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface UpdateLocationWeeklyOpeningRequestDto {
  days: OpeningDayRequestDto[];
  expectedVersion: number | null;
}

export interface LocationShiftTemplateResponseDto {
  id: string;
  locationId: string;
  locationName: string;
  category: BackendShiftTemplateCategory;
  name: string;
  weekdays: BackendDayOfWeek[];
  startTime: string;
  endTime: string;
  isActive: boolean;
  requiredCapability: BackendStaffingCapability | null;
  version: number | string;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface CreateLocationShiftTemplateRequestDto {
  name: string;
  category: BackendShiftTemplateCategory;
  weekdays: BackendDayOfWeek[];
  startTime: string;
  endTime: string;
  isActive: boolean;
  requiredCapability: BackendStaffingCapability | null;
}

export interface UpdateLocationShiftTemplateRequestDto extends CreateLocationShiftTemplateRequestDto {
  expectedVersion: number;
}

export interface DeactivateLocationShiftTemplateRequestDto {
  expectedVersion: number;
}
