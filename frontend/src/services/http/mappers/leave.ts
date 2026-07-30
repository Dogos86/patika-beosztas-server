// Leave request mapperek — a `LeaveRequestResponseDto` és a UI `LeaveRequest`
// típus között fordít. A `statusHistory` bejegyzések nem tartalmaznak
// szereplő azonosítót a wire-on, ezért `actorUserId`-t üresen hagyjuk; a UI
// ezt „ismeretlen"-ként kezeli.

import type {
  BackendLeaveStatus,
  BackendLeaveType,
  CreateLeaveRequestDto,
  LeaveRequestResponseDto,
  LeaveStatusHistoryResponseDto,
  UpdateLeaveRequestDto,
} from "../dto/leave";
import type { LeaveHistoryEntry, LeaveRequest, LeaveStatus, LeaveType } from "@/services/types";
import type { MyLeaveRequestInput } from "@/services/interfaces";

const TYPE_FROM: Record<BackendLeaveType, LeaveType> = {
  AnnualLeave: "annual_leave",
  SickLeave: "sick_leave",
  UnpaidLeave: "unpaid_leave",
  ParentalLeave: "parental_leave",
  Other: "other",
};
const TYPE_TO: Record<LeaveType, BackendLeaveType> = {
  annual_leave: "AnnualLeave",
  sick_leave: "SickLeave",
  unpaid_leave: "UnpaidLeave",
  parental_leave: "ParentalLeave",
  other: "Other",
};

const STATUS_FROM: Record<BackendLeaveStatus, LeaveStatus> = {
  Draft: "draft",
  Pending: "pending",
  Approved: "approved",
  Rejected: "rejected",
  Withdrawn: "withdrawn",
  Cancelled: "cancelled",
  Reported: "reported",
  Recorded: "recorded",
  Closed: "closed",
};

const HISTORY_ACTION: Record<BackendLeaveStatus, LeaveHistoryEntry["action"] | null> = {
  Draft: "created",
  Pending: "created",
  Approved: "approved",
  Rejected: "rejected",
  Withdrawn: "withdrawn",
  Cancelled: "cancelled",
  Reported: "reported",
  Recorded: "reported",
  Closed: "cancelled",
};

function hhmm(t: string | null): string | undefined {
  if (!t) return undefined;
  return t.length >= 5 ? t.slice(0, 5) : t;
}

export function mapLeaveTypeFromBackend(v: BackendLeaveType): LeaveType {
  return TYPE_FROM[v];
}
export function mapLeaveTypeToBackend(v: LeaveType): BackendLeaveType {
  return TYPE_TO[v];
}

export function mapLeaveStatusFromBackend(v: BackendLeaveStatus): LeaveStatus {
  return STATUS_FROM[v];
}

function mapHistory(dto: LeaveStatusHistoryResponseDto): LeaveHistoryEntry {
  const action = HISTORY_ACTION[dto.toStatus] ?? "created";
  return {
    at: dto.occurredAtUtc,
    actorUserId: "",
    action,
    note: dto.reason ?? undefined,
  };
}

export function mapLeaveFromBackend(dto: LeaveRequestResponseDto): LeaveRequest {
  return {
    id: dto.id,
    employeeId: dto.employeeId,
    type: mapLeaveTypeFromBackend(dto.type),
    fullDay: dto.isFullDay,
    startDate: dto.dateFrom,
    endDate: dto.dateTo ?? dto.dateFrom,
    startTime: hhmm(dto.startTime),
    endTime: hhmm(dto.endTime),
    note: dto.employeeNote ?? undefined,
    status: mapLeaveStatusFromBackend(dto.status),
    createdAt: dto.createdAtUtc,
    createdByUserId: "",
    decisionNote: dto.decisionReason ?? undefined,
    history: (dto.statusHistory ?? []).map(mapHistory),
    version: Number(dto.version),
  };
}

/** UI "HH:mm" → backend "HH:mm:ss" a `time` formátumhoz. */
function toBackendTime(t: string | undefined): string | null {
  if (!t) return null;
  return t.length === 5 ? `${t}:00` : t;
}

export function mapCreateLeaveRequest(input: MyLeaveRequestInput): CreateLeaveRequestDto {
  return {
    type: mapLeaveTypeToBackend(input.type),
    dateFrom: input.startDate,
    dateTo: input.endDate ?? input.startDate,
    isFullDay: input.fullDay,
    startTime: input.fullDay ? null : toBackendTime(input.startTime),
    endTime: input.fullDay ? null : toBackendTime(input.endTime),
    employeeNote: input.note ?? null,
  };
}

export function mapUpdateLeaveRequest(
  input: MyLeaveRequestInput,
  expectedVersion: number,
): UpdateLeaveRequestDto {
  return {
    dateFrom: input.startDate,
    dateTo: input.endDate ?? input.startDate,
    isFullDay: input.fullDay,
    startTime: input.fullDay ? null : toBackendTime(input.startTime),
    endTime: input.fullDay ? null : toBackendTime(input.endTime),
    employeeNote: input.note ?? null,
    expectedVersion,
  };
}
