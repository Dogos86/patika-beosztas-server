import type { LinkedEmployeeDto, SessionResponseDto } from "../dto";
import type { LinkedEmployeeInfo, User } from "@/services/types";
import { mapProfessionalRoleFromBackend } from "./enums";

export function mapLinkedEmployeeFromBackend(dto: LinkedEmployeeDto): LinkedEmployeeInfo {
  return {
    id: dto.id,
    displayName: dto.displayName,
    professionalRole: mapProfessionalRoleFromBackend(dto.professionalRole),
    active: dto.isActive,
    schedulable: dto.isSchedulable,
  };
}

export function mapSessionFromBackend(dto: SessionResponseDto): User {
  return {
    id: dto.userId,
    organizationId: dto.organizationId ?? undefined,
    email: dto.email,
    displayName: dto.displayName,
    active: dto.isActive,
    permissions: [...dto.permissions],
    linkedEmployee: dto.linkedEmployee ? mapLinkedEmployeeFromBackend(dto.linkedEmployee) : null,
  };
}
