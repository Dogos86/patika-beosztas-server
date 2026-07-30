// Leave request DTO-k a Phase 2E OpenAPI szerződéshez.
// Numerikus verzió mező a wire-en `integer | string` lehet — a mapperek
// `Number(...)`-t hívnak. Az időmezők `time` formátuma "HH:mm:ss" is lehet;
// a mapper "HH:mm"-re rövidít.

export type BackendLeaveType =
  "AnnualLeave" | "SickLeave" | "UnpaidLeave" | "ParentalLeave" | "Other";

export type BackendLeaveStatus =
  | "Draft"
  | "Pending"
  | "Approved"
  | "Rejected"
  | "Withdrawn"
  | "Cancelled"
  | "Reported"
  | "Recorded"
  | "Closed";

export type BackendLeaveDecision = "Approve" | "Reject";

export interface LeaveStatusHistoryResponseDto {
  fromStatus: BackendLeaveStatus | null;
  toStatus: BackendLeaveStatus;
  occurredAtUtc: string;
  reason: string | null;
}

export interface LeaveRequestResponseDto {
  id: string;
  employeeId: string;
  employeeDisplayName: string;
  type: BackendLeaveType;
  dateFrom: string;
  dateTo: string | null;
  isFullDay: boolean;
  startTime: string | null;
  endTime: string | null;
  status: BackendLeaveStatus;
  employeeNote: string | null;
  decisionReason: string | null;
  statusHistory: LeaveStatusHistoryResponseDto[];
  version: number | string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateLeaveRequestDto {
  type: BackendLeaveType;
  dateFrom: string;
  dateTo: string | null;
  isFullDay: boolean;
  startTime: string | null;
  endTime: string | null;
  employeeNote: string | null;
}

export interface UpdateLeaveRequestDto {
  dateFrom: string;
  dateTo: string | null;
  isFullDay: boolean;
  startTime: string | null;
  endTime: string | null;
  employeeNote: string | null;
  expectedVersion: number;
}

export interface LeaveVersionRequestDto {
  expectedVersion: number;
}

export interface LeaveDecisionRequestDto {
  decision: BackendLeaveDecision;
  reason: string | null;
  expectedVersion: number;
}

export interface CancelLeaveRequestDto {
  reason: string;
  expectedVersion: number;
}

export interface CloseSickLeaveRequestDto {
  dateTo: string;
  expectedVersion: number;
}
