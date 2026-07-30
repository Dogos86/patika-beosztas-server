// Exhaustív enum mapperek. Ismeretlen backend érték esetén kontrollált
// hibát dobunk — nem csendes fallback.

import type { BackendLocationType, BackendProfessionalRole } from "../dto/enums";
import type { LeaveType, Location, ProfessionalRole } from "@/services/types";

const PROF_ROLE_FROM: Record<BackendProfessionalRole, ProfessionalRole> = {
  PharmacyManager: "pharmacy_manager",
  Pharmacist: "pharmacist",
  SpecialistAssistant: "specialist_assistant",
  Assistant: "assistant",
  PharmacistTrainee: "pharmacist_trainee",
  AssistantTrainee: "assistant_trainee",
  Cleaner: "cleaner",
  FinanceHelper: "finance_helper",
  Other: "other",
};
const PROF_ROLE_TO = Object.fromEntries(
  Object.entries(PROF_ROLE_FROM).map(([k, v]) => [v, k]),
) as Record<ProfessionalRole, BackendProfessionalRole>;

export function mapProfessionalRoleFromBackend(v: string): ProfessionalRole {
  const out = PROF_ROLE_FROM[v as BackendProfessionalRole];
  if (!out) throw new Error(`Ismeretlen ProfessionalRole a backendtől: ${v}`);
  return out;
}
export function mapProfessionalRoleToBackend(v: ProfessionalRole): BackendProfessionalRole {
  return PROF_ROLE_TO[v];
}

export function mapLocationKindFromBackend(v: string): Location["kind"] {
  if (v === "Central") return "headquarters";
  if (v === "Branch") return "branch";
  throw new Error(`Ismeretlen LocationType: ${v}`);
}
export function mapLocationKindToBackend(v: Location["kind"]): BackendLocationType {
  return v === "headquarters" ? "Central" : "Branch";
}

/** TimeType a backend átfogó típusa — a UI-ban leaveType-ra szűkítjük. */
export function mapTimeTypeToLeaveType(v: string): LeaveType | null {
  switch (v) {
    case "AnnualLeave":
      return "annual_leave";
    case "SickLeave":
      return "sick_leave";
    case "UnpaidLeave":
      return "unpaid_leave";
    case "ParentalLeave":
      return "parental_leave";
    case "Other":
      return "other";
    case "Work":
    case "Overtime":
    case "OnCallDuty":
    case "Standby":
      return null; // nem leave típus
    default:
      throw new Error(`Ismeretlen TimeType: ${v}`);
  }
}
