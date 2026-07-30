import type { Employee, ProfessionalRole, StaffingCapability } from "@/services/types";

const ROLE_CAPS: Record<ProfessionalRole, StaffingCapability[]> = {
  pharmacy_manager: ["pharmacist"],
  pharmacist: ["pharmacist"],
  specialist_assistant: ["senior_assistant", "assistant"],
  assistant: ["assistant"],
  pharmacist_trainee: ["assistant"],
  assistant_trainee: ["assistant"],
  cleaner: ["cleaner"],
  finance_helper: ["finance"],
  other: ["other"],
};

export function employeeCapabilities(e: Employee): StaffingCapability[] {
  if (e.capabilities && e.capabilities.length > 0) return e.capabilities;
  const base = new Set<StaffingCapability>(ROLE_CAPS[e.professionalRole] ?? ["other"]);
  if (e.countsAsPharmacist) base.add("pharmacist");
  return [...base];
}

export function hasCapability(e: Employee, need: StaffingCapability): boolean {
  return employeeCapabilities(e).includes(need);
}

export function migrateEmployeeCapabilities(e: Employee): Employee {
  return { ...e, capabilities: employeeCapabilities(e) };
}

export const CAPABILITIES: StaffingCapability[] = [
  "pharmacist",
  "specialist_pharmacist",
  "senior_assistant",
  "assistant",
  "cleaner",
  "finance",
  "other",
];
