import type { QueryClient } from "@tanstack/react-query";
import type { EmployeeWorkProfile } from "@/services/types";
import { ApiError } from "@/services/http/errors";

export type WorkProfileField =
  | "contractedMonthlyMinutes"
  | "contractedWeeklyMinutes"
  | "standardShiftMinutes"
  | "minimumShiftMinutes"
  | "maximumRegularShiftMinutes"
  | "maximumDailyMinutes"
  | "maximumLongShiftMinutes"
  | "maximumOvertimeMinutesPerMonth"
  | "maximumOnCallAssignmentsPerMonth"
  | "maximumStandbyAssignmentsPerMonth"
  | "maximumSaturdaysPerMonth"
  | "maximumSundaysPerMonth"
  | "includeInAutoFill";

export class WorkProfileValidationError extends Error {
  constructor(
    public readonly field: WorkProfileField,
    public readonly validationCode: string,
    message: string,
  ) {
    super(message);
    this.name = "WorkProfileValidationError";
  }
}

const FIELD_MESSAGES: Partial<Record<WorkProfileField, Record<string, string>>> = {
  maximumLongShiftMinutes: {
    LONG_SHIFT_LIMIT_REQUIRED: "Hosszú műszak engedélyezésekor adj meg pozitív maximumot.",
    LONG_SHIFT_LIMIT_MUST_BE_EMPTY:
      "A hosszú műszak maximuma csak engedélyezett hosszú műszaknál adható meg.",
    LONG_SHIFT_MAXIMUM_TOO_SMALL:
      "A hosszú műszak maximuma nem lehet kisebb a normál műszak maximumánál.",
  },
  maximumOvertimeMinutesPerMonth: {
    OVERTIME_LIMIT_REQUIRED: "Túlóra engedélyezésekor adj meg pozitív havi maximumot.",
    OVERTIME_LIMIT_MUST_BE_EMPTY: "A havi túlóra maximuma csak engedélyezett túlóránál adható meg.",
  },
};

export function normalizeConditionalPositiveLimit(
  allowed: boolean,
  value: number | null,
  field: WorkProfileField,
  requiredCode: string,
): number | null {
  if (!allowed) return null;
  if (value === null || !Number.isInteger(value) || value <= 0) {
    throw new WorkProfileValidationError(
      field,
      requiredCode,
      FIELD_MESSAGES[field]?.[requiredCode] ?? "Adj meg pozitív értéket.",
    );
  }
  return value;
}

export function setLongShiftAllowed(
  profile: EmployeeWorkProfile,
  allowed: boolean,
): EmployeeWorkProfile {
  return {
    ...profile,
    allowsLongShift: allowed,
    maximumLongShiftMinutes: allowed
      ? profile.maximumLongShiftMinutes && profile.maximumLongShiftMinutes > 0
        ? profile.maximumLongShiftMinutes
        : profile.maximumDailyMinutes
      : null,
  };
}

export function getWorkProfileFieldErrors(
  error: unknown,
): Partial<Record<WorkProfileField, string>> {
  if (error instanceof WorkProfileValidationError) {
    return { [error.field]: error.message };
  }
  if (!(error instanceof ApiError)) return {};

  const result: Partial<Record<WorkProfileField, string>> = {};
  for (const [field, messages] of Object.entries(error.fieldErrors ?? {})) {
    const workProfileField = field as WorkProfileField;
    const code = error.fieldErrorCodes?.[field]?.[0];
    result[workProfileField] =
      (code && FIELD_MESSAGES[workProfileField]?.[code]) || messages[0] || "A mező értéke hibás.";
  }
  return result;
}

export async function refetchEmployeeWorkProfile(
  queryClient: QueryClient,
  employeeId: string,
  queryFn: () => Promise<EmployeeWorkProfile | null>,
): Promise<EmployeeWorkProfile | null> {
  const queryKey = ["employee-work-profile", employeeId] as const;
  await queryClient.invalidateQueries({ queryKey, refetchType: "none" });
  return queryClient.fetchQuery({ queryKey, queryFn, staleTime: 0 });
}
