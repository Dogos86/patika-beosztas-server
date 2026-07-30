// Munkavégzési kérések és visszatérő szabályok DTO-i.
// Backend szerződés: contracts/openapi.phase2d.json (Work preferences tag).

import type { BackendDayOfWeek } from "./enums";

export type BackendWorkPreferenceType =
  "Available" | "Preferred" | "Avoid" | "Unavailable" | "Fixed";

export interface WorkPreferenceResponseDto {
  id: string;
  employeeId: string;
  employeeDisplayName: string;
  type: BackendWorkPreferenceType;
  dateFrom: string;
  dateTo: string;
  dayOfWeek: BackendDayOfWeek | null;
  isFullDay: boolean;
  startTime: string | null;
  endTime: string | null;
  locationId: string | null;
  locationName: string | null;
  note: string | null;
  isActive: boolean;
  version: number | string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateWorkPreferenceRequestDto {
  type: BackendWorkPreferenceType;
  dateFrom: string;
  dateTo: string;
  dayOfWeek: BackendDayOfWeek | null;
  isFullDay: boolean;
  startTime: string | null;
  endTime: string | null;
  locationId: string | null;
  note: string | null;
}

export interface UpdateWorkPreferenceRequestDto extends CreateWorkPreferenceRequestDto {
  expectedVersion: number;
}

export interface DeactivateWorkPreferenceRequestDto {
  expectedVersion: number;
}
