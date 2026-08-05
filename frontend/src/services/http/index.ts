// Phase 1 HTTP service-ek. Ez a fájl a Services locator alternatívája
// a `VITE_DATA_SOURCE=phase1-http` módhoz. A még el nem készült backend
// modulok (schedule, leaveRequest, coverage, ai, notification) hibát
// dobnak — a UI ilyenkor „a szerver következő fázisában érkezik" jelzést
// mutat.

import type { Services } from "../interfaces";
import { buildUrl, httpClient } from "./client";
import { clearCsrfToken } from "./csrf";
import { ApiError } from "./errors";
import type {
  EmployeeResponseDto,
  LocationResponseDto,
  PagedResponseDto,
  SessionResponseDto,
  UserResponseDto,
} from "./dto";
import type {
  EmployeeCapabilitiesResponseDto,
  EmployeeShiftQuotaRuleResponseDto,
  EmployeeWorkProfileResponseDto,
} from "./dto/employee-planning";
import {
  mapCapabilitiesFromBackend,
  mapCapabilitiesUpdateRequest,
  mapQuotaCreateRequest,
  mapQuotaDeactivateRequest,
  mapQuotaRuleFromBackend,
  mapQuotaUpdateRequest,
  mapWorkProfileFromBackend,
  mapWorkProfileUpdateRequest,
} from "./mappers/employee-planning";
import type {
  CoverageRequirementResponseDto,
  DeactivateCoverageRequirementRequestDto,
} from "./dto/coverage";
import { mapSessionFromBackend } from "./mappers/session";
import {
  mapCapabilityToBackend,
  mapCoverageFromBackend,
  mapCoverageToCreateRequest,
  mapCoverageToUpdateRequest,
} from "./mappers/coverage";
import {
  mapEmployeeFromBackend,
  mapEmployeeToCreateRequest,
  mapEmployeeToUpdateRequest,
} from "./mappers/employee";
import {
  mapLocationFromBackend,
  mapShiftTemplateFromBackend,
  mapShiftTemplateToCreateRequest,
  mapShiftTemplateToUpdateRequest,
  mapWeeklyOpeningFromBackend,
  mapWeeklyOpeningToUpdateRequest,
} from "./mappers/location";
import type {
  LocationShiftTemplateResponseDto,
  LocationWeeklyOpeningResponseDto,
} from "./dto/location";
import { mapLocationKindToBackend } from "./mappers/enums";
import type { CreateLocationInput } from "../interfaces";
import {
  mapCreateUserRequest,
  mapUpdateEmployeeLinkRequest,
  mapUpdatePermissionsRequest,
  mapUpdateStatusRequest,
  mapUserFromBackend,
} from "./mappers/user";
import { collectAllPages, mapPagedResponse } from "@/lib/pagination";
import type {
  EmployeePayrollProfileResponseDto,
  PayrollOnboardingSummaryResponseDto,
  TaxAllowanceSurveyResponseDto,
  TaxDeclarationRequirementResponseDto,
  TaxSurveyVersionRequestDto,
  ReviewTaxAllowanceSurveyRequestDto,
} from "./dto/payroll";
import type {
  CancelLeaveRequestDto,
  CloseSickLeaveRequestDto,
  LeaveDecisionRequestDto,
  LeaveRequestResponseDto,
  LeaveVersionRequestDto,
} from "./dto/leave";
import { mapCreateLeaveRequest, mapLeaveFromBackend } from "./mappers/leave";
import {
  mapAdminSurveyUpdateRequest,
  mapDeclarationOverrideRequest,
  mapDeclarationStatusRequest,
  mapOwnSurveyCreateRequest,
  mapOwnSurveyUpdateRequest,
  mapProfileUpdateRequest,
} from "./mappers/payroll";
import type {
  AdminScheduleService,
  PayrollService,
  RegenerateScheduleInput,
  ScheduleGenerationService,
  StartScheduleGenerationInput,
} from "../interfaces";
import type { CoverageRule, LeaveRequest } from "@/services/types";
import type {
  CancelScheduleGenerationRequestDto,
  CloneScheduleDraftRequestDto,
  CreateScheduleGenerationRequestDto,
  EmployeeScheduleMatrixResponseDto,
  LocationCoverageResponseDto,
  OwnScheduleResponseDto,
  RegenerateScheduleRequestDto,
  RejectGeneratedSuggestionRequestDto,
  ReplaceShiftRequestDto,
  ScheduleChangeResponseDto,
  ScheduleGenerationRunResponseDto,
  ScheduleIssueResponseDto,
  ScheduleListItemResponseDto,
  SchedulePlanResponseDto,
  ScheduleVersionRequestDto,
  ShiftAssignmentResponseDto,
  ShiftExplanationResponseDto,
  ShiftVersionRequestDto,
  ScheduleAlternativeResponseDto,
} from "./dto/schedule";
import {
  mapCoverageProjectionFromBackend,
  mapGenerationRunFromBackend,
  mapIssueFromBackend,
  mapMatrixFromBackend,
  mapOwnScheduleFromBackend,
  mapRegenerationScopeToBackend,
  mapScheduleChangeFromBackend,
  mapScheduleListItemFromBackend,
  mapSchedulePlanFromBackend,
  mapShiftAssignmentFromBackend,
  mapShiftExplanationFromBackend,
} from "./mappers/schedule";
import type { ScheduleAlternative } from "@/services/types";
import type { WorkPreferenceResponseDto } from "./dto/work-preference";
import {
  mapWorkPreferenceDeactivateRequest,
  mapWorkPreferenceFromBackend,
  mapWorkPreferenceToCreateRequest,
  mapWorkPreferenceToUpdateRequest,
} from "./mappers/work-preference";

function requireVersion(v: number | undefined, ctx: string): number {
  if (typeof v !== "number") {
    throw new Error(`Hiányzó verziószám (${ctx}) — töltsd újra a listát.`);
  }
  return v;
}

/** UI Employee-hez opcionálisan tapadó szerver version (mapper eredménye). */
type WithVersion<T> = T & { version?: number };

function notReady(name: string): never {
  throw new Error(`A(z) ${name} funkció még nem érhető el a szerver ezen fázisában.`);
}

// AUTH ─────────────────────────────────────────────────────────────
const authService: Services["auth"] = {
  async getSession() {
    try {
      const dto = await httpClient.get<SessionResponseDto>("/api/auth/session");
      return mapSessionFromBackend(dto);
    } catch (err) {
      // Csak 401 esetén tekintjük „nincs session"-nek; egyéb hibát (5xx,
      // hálózat, CSRF) továbbdobunk, hogy a UI látható hibát mutasson.
      if (err instanceof ApiError && err.code === "UNAUTHENTICATED") return null;
      throw err;
    }
  },
  async getCurrentUser() {
    return this.getSession();
  },
  async login(email, password) {
    const dto = await httpClient.post<SessionResponseDto>("/api/auth/login", { email, password });
    // A login megváltoztatja az antiforgery tokenhez kötött identitást.
    clearCsrfToken();
    return mapSessionFromBackend(dto);
  },
  async logout() {
    try {
      await httpClient.post("/api/auth/logout");
    } finally {
      clearCsrfToken();
    }
  },
  async requestPasswordReset(_email) {
    // A backend jelenleg nem szállítja a password-reset endpointot; a UI
    // ezt „később érhető el" jelzéssel kezeli, hívást nem indítunk.
    return;
  },
};

// EMPLOYEES ────────────────────────────────────────────────────────
const employeeService: Services["employee"] = {
  async list() {
    return employeeService.listAll();
  },
  async listAll(options) {
    return collectAllPages(
      (page, pageSize) =>
        employeeService.listPaged({
          page,
          pageSize,
          search: options?.search,
          includeInactive: options?.includeInactive ?? true,
        }),
      { maxItems: options?.maxItems, pageSize: options?.pageSize, signal: options?.signal },
    );
  },
  async listPaged(query) {
    const raw = await httpClient.get<PagedResponseDto<EmployeeResponseDto>>(
      "/api/admin/employees",
      query as Record<string, string | number | boolean | undefined | null>,
    );
    return mapPagedResponse(raw, mapEmployeeFromBackend);
  },
  async get(id) {
    const dto = await httpClient.get<EmployeeResponseDto | null>(`/api/admin/employees/${id}`);
    return dto ? mapEmployeeFromBackend(dto) : null;
  },
  async create(input) {
    const dto = await httpClient.post<EmployeeResponseDto>(
      "/api/admin/employees",
      mapEmployeeToCreateRequest(input as never),
    );
    return mapEmployeeFromBackend(dto);
  },
  async update(id, input, expectedVersion) {
    const dto = await httpClient.put<EmployeeResponseDto>(
      `/api/admin/employees/${id}`,
      mapEmployeeToUpdateRequest(input, expectedVersion),
    );
    return mapEmployeeFromBackend(dto);
  },
  async getCapabilities(employeeId) {
    const dto = await httpClient.get<EmployeeCapabilitiesResponseDto>(
      `/api/admin/employees/${employeeId}/capabilities`,
    );
    return mapCapabilitiesFromBackend(dto);
  },
  async updateCapabilities(employeeId, capabilities, expectedEmployeeVersion) {
    const dto = await httpClient.put<EmployeeCapabilitiesResponseDto>(
      `/api/admin/employees/${employeeId}/capabilities`,
      mapCapabilitiesUpdateRequest(capabilities, expectedEmployeeVersion),
    );
    return mapCapabilitiesFromBackend(dto);
  },
  async getWorkProfile(employeeId) {
    try {
      const dto = await httpClient.get<EmployeeWorkProfileResponseDto>(
        `/api/admin/employees/${employeeId}/work-profile`,
      );
      return mapWorkProfileFromBackend(dto);
    } catch (err) {
      if (err instanceof ApiError && err.code === "NOT_FOUND") return null;
      throw err;
    }
  },
  async updateWorkProfile(employeeId, input) {
    const dto = await httpClient.put<EmployeeWorkProfileResponseDto>(
      `/api/admin/employees/${employeeId}/work-profile`,
      mapWorkProfileUpdateRequest(input),
    );
    return mapWorkProfileFromBackend(dto);
  },
  async listQuotas(employeeId) {
    const dtos = await httpClient.get<EmployeeShiftQuotaRuleResponseDto[]>(
      `/api/admin/employees/${employeeId}/shift-quota-rules`,
    );
    return dtos.map(mapQuotaRuleFromBackend);
  },
  async createQuota(employeeId, input) {
    const dto = await httpClient.post<EmployeeShiftQuotaRuleResponseDto>(
      `/api/admin/employees/${employeeId}/shift-quota-rules`,
      mapQuotaCreateRequest(input),
    );
    return mapQuotaRuleFromBackend(dto);
  },
  async updateQuota(id, input) {
    const dto = await httpClient.put<EmployeeShiftQuotaRuleResponseDto>(
      `/api/admin/employee-shift-quota-rules/${id}`,
      mapQuotaUpdateRequest(input),
    );
    return mapQuotaRuleFromBackend(dto);
  },
  async deactivateQuota(id, expectedVersion) {
    const dto = await httpClient.post<EmployeeShiftQuotaRuleResponseDto>(
      `/api/admin/employee-shift-quota-rules/${id}/deactivate`,
      mapQuotaDeactivateRequest(expectedVersion),
    );
    return mapQuotaRuleFromBackend(dto);
  },
};

// LOCATIONS ────────────────────────────────────────────────────────
const locationService: Services["location"] = {
  async list() {
    return locationService.listAll();
  },
  async listPaged(query) {
    const raw = await httpClient.get<PagedResponseDto<LocationResponseDto>>(
      "/api/admin/locations",
      query as Record<string, string | number | boolean | undefined | null>,
    );
    return mapPagedResponse(raw, mapLocationFromBackend);
  },
  async listAll(options) {
    return collectAllPages(
      (page, pageSize) =>
        locationService.listPaged({
          page,
          pageSize,
          search: options?.search,
          includeInactive: options?.includeInactive ?? true,
        }),
      { maxItems: options?.maxItems, pageSize: options?.pageSize, signal: options?.signal },
    );
  },
  async get(id) {
    const dto = await httpClient.get<LocationResponseDto | null>(`/api/admin/locations/${id}`);
    return dto ? mapLocationFromBackend(dto) : null;
  },
  async create(input) {
    const dto = await httpClient.post<LocationResponseDto>("/api/admin/locations", {
      name: input.name,
      type: mapLocationKindToBackend(input.kind),
      address: input.address ?? null,
      isActive: input.active,
    });
    return mapLocationFromBackend(dto);
  },
  async update(id, input: CreateLocationInput, expectedVersion) {
    const dto = await httpClient.put<LocationResponseDto>(`/api/admin/locations/${id}`, {
      name: input.name,
      type: mapLocationKindToBackend(input.kind),
      address: input.address ?? null,
      isActive: input.active,
      expectedVersion,
    });
    return mapLocationFromBackend(dto);
  },

  async getWeeklyOpening(locationId) {
    try {
      const dto = await httpClient.get<LocationWeeklyOpeningResponseDto>(
        `/api/admin/locations/${locationId}/weekly-opening`,
      );
      return mapWeeklyOpeningFromBackend(dto);
    } catch (e) {
      if (e instanceof ApiError && e.code === "NOT_FOUND") return null;
      throw e;
    }
  },
  async updateWeeklyOpening(locationId, hours, expectedVersion) {
    const dto = await httpClient.put<LocationWeeklyOpeningResponseDto>(
      `/api/admin/locations/${locationId}/weekly-opening`,
      mapWeeklyOpeningToUpdateRequest(hours, expectedVersion),
    );
    return mapWeeklyOpeningFromBackend(dto);
  },

  async listShiftTemplates(locationId, includeInactive) {
    const dtos = await httpClient.get<LocationShiftTemplateResponseDto[]>(
      `/api/admin/locations/${locationId}/shift-templates`,
      { includeInactive: includeInactive ?? true },
    );
    return dtos.map(mapShiftTemplateFromBackend);
  },
  async createShiftTemplate(locationId, input) {
    const dto = await httpClient.post<LocationShiftTemplateResponseDto>(
      `/api/admin/locations/${locationId}/shift-templates`,
      mapShiftTemplateToCreateRequest(input),
    );
    return mapShiftTemplateFromBackend(dto);
  },
  async updateShiftTemplate(id, input, expectedVersion) {
    const dto = await httpClient.put<LocationShiftTemplateResponseDto>(
      `/api/admin/location-shift-templates/${id}`,
      mapShiftTemplateToUpdateRequest(input, expectedVersion),
    );
    return mapShiftTemplateFromBackend(dto);
  },
  async deactivateShiftTemplate(id, expectedVersion) {
    const dto = await httpClient.post<LocationShiftTemplateResponseDto>(
      `/api/admin/location-shift-templates/${id}/deactivate`,
      { expectedVersion },
    );
    return mapShiftTemplateFromBackend(dto);
  },
};

// USERS ────────────────────────────────────────────────────────────
const userService: Services["user"] = {
  async list(query) {
    const raw = await httpClient.get<PagedResponseDto<UserResponseDto>>(
      "/api/admin/users",
      query as Record<string, string | number | boolean | undefined | null>,
    );
    return mapPagedResponse(raw, mapUserFromBackend);
  },
  async create(input) {
    const dto = await httpClient.post<UserResponseDto>(
      "/api/admin/users",
      mapCreateUserRequest(input),
    );
    return mapUserFromBackend(dto);
  },
  async updatePermissions(id, input) {
    const dto = await httpClient.put<UserResponseDto>(
      `/api/admin/users/${id}/permissions`,
      mapUpdatePermissionsRequest(input),
    );
    return mapUserFromBackend(dto);
  },
  async updateEmployeeLink(id, input) {
    const dto = await httpClient.put<UserResponseDto>(
      `/api/admin/users/${id}/employee-link`,
      mapUpdateEmployeeLinkRequest(input),
    );
    return mapUserFromBackend(dto);
  },
  async setStatus(id, input) {
    const dto = await httpClient.put<UserResponseDto>(
      `/api/admin/users/${id}/status`,
      mapUpdateStatusRequest(input),
    );
    return mapUserFromBackend(dto);
  },
};

// COVERAGE ─────────────────────────────────────────────────────────
const DOW_INDEX_TO_BACKEND = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
] as const;

const coverageService: Services["coverage"] = {
  async list(query) {
    const params: Record<string, string | number | boolean | undefined | null> = {};
    if (query?.locationId) params.locationId = query.locationId;
    if (query?.capability) params.capability = mapCapabilityToBackend(query.capability);
    if (typeof query?.weekday === "number") {
      params.dayOfWeek = DOW_INDEX_TO_BACKEND[query.weekday];
    }
    if (typeof query?.includeInactive === "boolean") {
      params.includeInactive = query.includeInactive;
    }
    const dtos = await httpClient.get<CoverageRequirementResponseDto[]>(
      "/api/admin/coverage-requirements",
      params,
    );
    return dtos.map(mapCoverageFromBackend);
  },
  async create(rule) {
    const dto = await httpClient.post<CoverageRequirementResponseDto>(
      "/api/admin/coverage-requirements",
      mapCoverageToCreateRequest(rule),
    );
    return mapCoverageFromBackend(dto);
  },
  async update(id, rule, expectedVersion) {
    const dto = await httpClient.put<CoverageRequirementResponseDto>(
      `/api/admin/coverage-requirements/${id}`,
      mapCoverageToUpdateRequest(rule, expectedVersion),
    );
    return mapCoverageFromBackend(dto);
  },
  async deactivate(id, expectedVersion) {
    const body: DeactivateCoverageRequirementRequestDto = { expectedVersion };
    const dto = await httpClient.post<CoverageRequirementResponseDto>(
      `/api/admin/coverage-requirements/${id}/deactivate`,
      body,
    );
    return mapCoverageFromBackend(dto);
  },
  async save(rule) {
    const meta = rule as CoverageRule & { version?: number; __version?: number };
    const version = meta.__version ?? meta.version;
    const isNew = !rule.id || rule.id.startsWith("c-") || rule.id.startsWith("new-");
    if (isNew || typeof version !== "number") {
      return coverageService.create(rule);
    }
    return coverageService.update(rule.id, rule, version);
  },
  async delete(id, expectedVersion) {
    if (typeof expectedVersion !== "number") {
      throw new Error("Deaktiváláshoz szükséges a verziószám (töltsd újra a lefedettségi listát).");
    }
    await coverageService.deactivate(id, expectedVersion);
  },
};

// WORK PREFERENCES ─────────────────────────────────────────────────
const workPreferenceService: Services["workPreference"] = {
  async listMine(includeInactive) {
    const dtos = await httpClient.get<WorkPreferenceResponseDto[]>("/api/me/work-preferences", {
      includeInactive: includeInactive ? "true" : undefined,
    });
    return dtos.map(mapWorkPreferenceFromBackend);
  },
  async createMine(input) {
    const dto = await httpClient.post<WorkPreferenceResponseDto>(
      "/api/me/work-preferences",
      mapWorkPreferenceToCreateRequest(input),
    );
    return mapWorkPreferenceFromBackend(dto);
  },
  async updateMine(id, input, expectedVersion) {
    const dto = await httpClient.put<WorkPreferenceResponseDto>(
      `/api/me/work-preferences/${id}`,
      mapWorkPreferenceToUpdateRequest(
        input,
        requireVersion(expectedVersion, "munkavégzési kérés"),
      ),
    );
    return mapWorkPreferenceFromBackend(dto);
  },
  async deactivateMine(id, expectedVersion) {
    const dto = await httpClient.post<WorkPreferenceResponseDto>(
      `/api/me/work-preferences/${id}/deactivate`,
      mapWorkPreferenceDeactivateRequest(requireVersion(expectedVersion, "munkavégzési kérés")),
    );
    return mapWorkPreferenceFromBackend(dto);
  },
};

const adminWorkPreferenceService: Services["adminWorkPreference"] = {
  async listForEmployee(employeeId, includeInactive) {
    const dtos = await httpClient.get<WorkPreferenceResponseDto[]>(
      `/api/admin/employees/${employeeId}/work-preferences`,
      { includeInactive: includeInactive ? "true" : undefined },
    );
    return dtos.map(mapWorkPreferenceFromBackend);
  },
  async createForEmployee(employeeId, input) {
    const dto = await httpClient.post<WorkPreferenceResponseDto>(
      `/api/admin/employees/${employeeId}/work-preferences`,
      mapWorkPreferenceToCreateRequest(input),
    );
    return mapWorkPreferenceFromBackend(dto);
  },
  async update(id, input, expectedVersion) {
    const dto = await httpClient.put<WorkPreferenceResponseDto>(
      `/api/admin/work-preferences/${id}`,
      mapWorkPreferenceToUpdateRequest(
        input,
        requireVersion(expectedVersion, "munkavégzési kérés"),
      ),
    );
    return mapWorkPreferenceFromBackend(dto);
  },
  async deactivate(id, expectedVersion) {
    const dto = await httpClient.post<WorkPreferenceResponseDto>(
      `/api/admin/work-preferences/${id}/deactivate`,
      mapWorkPreferenceDeactivateRequest(requireVersion(expectedVersion, "munkavégzési kérés")),
    );
    return mapWorkPreferenceFromBackend(dto);
  },
};

export const httpServices: Services = {
  auth: authService,
  employee: employeeService,
  location: locationService,
  user: userService,
  payroll: makeHttpPayrollService(),
  coverage: coverageService,
  workPreference: workPreferenceService,
  adminWorkPreference: adminWorkPreferenceService,
  // A régi interface még nem támogatott műveletei explicit hibát adnak;
  // API módban nincs mock fallback.
  schedule: {
    getMySchedule: async () => notReady("Beosztás"),
    getOwnPublishedSchedule: async (query) => {
      const params: Record<string, string | undefined> = {};
      if (query?.date) params.date = query.date;
      try {
        const dto = await httpClient.get<OwnScheduleResponseDto | null>("/api/me/schedule", params);
        return dto ? mapOwnScheduleFromBackend(dto) : null;
      } catch (err) {
        if (err instanceof ApiError && err.code === "NOT_FOUND") return null;
        throw err;
      }
    },
    listShifts: async () => notReady("Beosztás"),
    upsertShift: async () => notReady("Beosztás"),
    deleteShift: async () => notReady("Beosztás"),
    autoFill: async () => notReady("Automatikus kitöltés"),
  },
  scheduleWorkspace: {
    generate: async () => notReady("Beosztás-generátor"),
    getCurrentRun: async () => null,
    regenerateScope: async () => notReady("Beosztás-generátor"),
    lockShift: async () => notReady("Beosztás-generátor"),
    unlockShift: async () => notReady("Beosztás-generátor"),
    rejectShift: async () => notReady("Beosztás-generátor"),
    findAlternatives: async () => [],
    approve: async () => notReady("Beosztás-generátor"),
    publish: async () => notReady("Beosztás-generátor"),
  },
  scheduleGeneration: makeHttpScheduleGenerationService(),
  adminSchedule: makeHttpAdminScheduleService(),
  leaveRequest: makeHttpLeaveRequestService(),
  adminLeaveRequest: makeHttpAdminLeaveRequestService(),
  notification: {
    listForUser: async () => notReady("Értesítések"),
    markRead: async () => notReady("Értesítések"),
  },
  ai: {
    interpret: async () => notReady("AI asszisztens"),
    interpretCommand: async () => notReady("AI asszisztens"),
    answerClarification: async () => notReady("AI asszisztens"),
    executeCommand: async () => notReady("AI asszisztens"),
  },
};

function makeHttpScheduleGenerationService(): ScheduleGenerationService {
  return {
    async start(input: StartScheduleGenerationInput) {
      const body: CreateScheduleGenerationRequestDto = {
        periodStart: input.periodStart,
        periodEnd: input.periodEnd,
        deterministicSeed: input.deterministicSeed ?? null,
        maxSolveSeconds: input.maxSolveSeconds ?? null,
        workerCount: input.workerCount ?? null,
        pendingLeaveHandling: input.pendingLeaveHandling,
        weights: input.weights ?? null,
      };
      const dto = await httpClient.post<ScheduleGenerationRunResponseDto>(
        "/api/admin/schedule-generations",
        body,
        { "Idempotency-Key": `schedule-generation-${crypto.randomUUID()}` },
      );
      return mapGenerationRunFromBackend(dto);
    },
    async get(runId: string) {
      const dto = await httpClient.get<ScheduleGenerationRunResponseDto>(
        `/api/admin/schedule-generations/${runId}`,
      );
      return mapGenerationRunFromBackend(dto);
    },
    async cancel(runId: string, expectedVersion: number) {
      const body: CancelScheduleGenerationRequestDto = { expectedVersion };
      const dto = await httpClient.post<ScheduleGenerationRunResponseDto>(
        `/api/admin/schedule-generations/${runId}/cancel`,
        body,
      );
      return mapGenerationRunFromBackend(dto);
    },
  };
}

function makeHttpAdminScheduleService(): AdminScheduleService {
  const ver = (n: number): ScheduleVersionRequestDto => ({ expectedVersion: n });
  const shiftVer = (
    expectedShiftVersion: number,
    expectedScheduleVersion: number,
    reason?: string,
  ): ShiftVersionRequestDto => ({
    expectedShiftVersion,
    expectedScheduleVersion,
    reason: reason ?? null,
  });

  return {
    async list(query) {
      const params: Record<string, string | undefined> = {};
      if (query?.status) params.status = query.status;
      const dtos = await httpClient.get<ScheduleListItemResponseDto[]>(
        "/api/admin/schedules",
        params,
      );
      return (dtos ?? []).map(mapScheduleListItemFromBackend);
    },
    async get(id) {
      const dto = await httpClient.get<SchedulePlanResponseDto>(`/api/admin/schedules/${id}`);
      return mapSchedulePlanFromBackend(dto);
    },
    async getMatrix(id) {
      const dto = await httpClient.get<EmployeeScheduleMatrixResponseDto>(
        `/api/admin/schedules/${id}/employee-matrix`,
      );
      return mapMatrixFromBackend(dto);
    },
    async getCoverage(id) {
      const dto = await httpClient.get<LocationCoverageResponseDto>(
        `/api/admin/schedules/${id}/location-coverage`,
      );
      return mapCoverageProjectionFromBackend(dto);
    },
    async listIssues(id) {
      const dtos = await httpClient.get<ScheduleIssueResponseDto[]>(
        `/api/admin/schedules/${id}/issues`,
      );
      return (dtos ?? []).map(mapIssueFromBackend);
    },
    async listChanges(id) {
      const dtos = await httpClient.get<ScheduleChangeResponseDto[]>(
        `/api/admin/schedules/${id}/changes`,
      );
      return (dtos ?? []).map(mapScheduleChangeFromBackend);
    },
    async explainShift(scheduleId, shiftId) {
      const dto = await httpClient.get<ShiftExplanationResponseDto>(
        `/api/admin/schedules/${scheduleId}/shifts/${shiftId}/explanation`,
      );
      return mapShiftExplanationFromBackend(dto);
    },
    async findAlternatives(scheduleId, shiftId) {
      const dtos = await httpClient.get<ScheduleAlternativeResponseDto[]>(
        `/api/admin/schedules/${scheduleId}/shifts/${shiftId}/alternatives`,
      );
      const explain = await httpClient
        .get<ShiftExplanationResponseDto>(
          `/api/admin/schedules/${scheduleId}/shifts/${shiftId}/explanation`,
        )
        .catch(() => null);
      // Preferred pattern: az alternatives endpointot használjuk, de ha nincs,
      // az explanation.alternatives-t adjuk vissza.
      if (Array.isArray(dtos) && dtos.length > 0) {
        return dtos.map((a) => ({
          employeeId: a.employeeId,
          employeeDisplayName: a.employeeDisplayName,
          scoreDifference: Number(a.scoreDifference),
          scoreComponents: Object.fromEntries(
            Object.entries(a.scoreComponents ?? {}).map(([k, v]) => [k, Number(v)]),
          ) as Record<string, number>,
          tradeoffCodes: a.tradeoffCodes ?? [],
        })) satisfies ScheduleAlternative[];
      }
      return explain ? mapShiftExplanationFromBackend(explain).alternatives : [];
    },
    async lockShift(scheduleId, shiftId, body) {
      const dto = await httpClient.post<ShiftAssignmentResponseDto>(
        `/api/admin/schedules/${scheduleId}/shifts/${shiftId}/lock`,
        shiftVer(body.expectedShiftVersion, body.expectedScheduleVersion, body.reason),
      );
      return mapShiftAssignmentFromBackend(dto);
    },
    async unlockShift(scheduleId, shiftId, body) {
      const dto = await httpClient.post<ShiftAssignmentResponseDto>(
        `/api/admin/schedules/${scheduleId}/shifts/${shiftId}/unlock`,
        shiftVer(body.expectedShiftVersion, body.expectedScheduleVersion, body.reason),
      );
      return mapShiftAssignmentFromBackend(dto);
    },
    async rejectShift(scheduleId, shiftId, body) {
      const req: RejectGeneratedSuggestionRequestDto = {
        expectedShiftVersion: body.expectedShiftVersion,
        expectedScheduleVersion: body.expectedScheduleVersion,
        reason: body.reason,
        exclusionScope: body.exclusionScope,
      };
      const dto = await httpClient.post<ShiftAssignmentResponseDto>(
        `/api/admin/schedules/${scheduleId}/shifts/${shiftId}/reject`,
        req,
      );
      return mapShiftAssignmentFromBackend(dto);
    },
    async replaceShift(scheduleId, shiftId, body) {
      const req: ReplaceShiftRequestDto = {
        replacementEmployeeId: body.replacementEmployeeId,
        expectedShiftVersion: body.expectedShiftVersion,
        expectedScheduleVersion: body.expectedScheduleVersion,
        reason: body.reason,
      };
      const dto = await httpClient.post<ShiftAssignmentResponseDto>(
        `/api/admin/schedules/${scheduleId}/shifts/${shiftId}/replace`,
        req,
      );
      return mapShiftAssignmentFromBackend(dto);
    },
    async regenerate(scheduleId, input: RegenerateScheduleInput) {
      const req: RegenerateScheduleRequestDto = {
        scope: mapRegenerationScopeToBackend(input.scope),
        expectedVersion: input.expectedVersion,
        deterministicSeed: input.deterministicSeed ?? null,
        maxSolveSeconds: input.maxSolveSeconds ?? null,
        workerCount: input.workerCount ?? null,
        pendingLeaveHandling: input.pendingLeaveHandling,
        weights: input.weights ?? null,
      };
      const dto = await httpClient.post<ScheduleGenerationRunResponseDto>(
        `/api/admin/schedules/${scheduleId}/regenerate`,
        req,
        { "Idempotency-Key": `schedule-regeneration-${crypto.randomUUID()}` },
      );
      return mapGenerationRunFromBackend(dto);
    },
    async submitForReview(id, expectedVersion) {
      const dto = await httpClient.post<SchedulePlanResponseDto>(
        `/api/admin/schedules/${id}/submit-review`,
        ver(expectedVersion),
      );
      return mapSchedulePlanFromBackend(dto);
    },
    async returnToDraft(id, expectedVersion) {
      const dto = await httpClient.post<SchedulePlanResponseDto>(
        `/api/admin/schedules/${id}/return-draft`,
        ver(expectedVersion),
      );
      return mapSchedulePlanFromBackend(dto);
    },
    async approve(id, expectedVersion) {
      const dto = await httpClient.post<SchedulePlanResponseDto>(
        `/api/admin/schedules/${id}/approve`,
        ver(expectedVersion),
      );
      return mapSchedulePlanFromBackend(dto);
    },
    async publish(id, expectedVersion) {
      const dto = await httpClient.post<SchedulePlanResponseDto>(
        `/api/admin/schedules/${id}/publish`,
        ver(expectedVersion),
      );
      return mapSchedulePlanFromBackend(dto);
    },
    async archive(id, expectedVersion) {
      const dto = await httpClient.post<SchedulePlanResponseDto>(
        `/api/admin/schedules/${id}/archive`,
        ver(expectedVersion),
      );
      return mapSchedulePlanFromBackend(dto);
    },
    async cloneDraft(id, expectedVersion) {
      const body: CloneScheduleDraftRequestDto = { expectedVersion };
      const dto = await httpClient.post<SchedulePlanResponseDto>(
        `/api/admin/schedules/${id}/clone-draft`,
        body,
        { "Idempotency-Key": `schedule-clone-${crypto.randomUUID()}` },
      );
      return mapSchedulePlanFromBackend(dto);
    },
  };
}

function makeHttpLeaveRequestService(): Services["leaveRequest"] {
  return {
    async listMyRequests(query) {
      const dtos = await httpClient.get<LeaveRequestResponseDto[]>("/api/me/leave-requests");
      const items = dtos.map(mapLeaveFromBackend);
      return query?.status ? items.filter((r) => r.status === query.status) : items;
    },
    async createMyRequest(input) {
      const created = await httpClient.post<LeaveRequestResponseDto>(
        "/api/me/leave-requests",
        mapCreateLeaveRequest(input),
      );
      // Nem-táppénz kérés: Draft-ból automatikusan submit → Pending, hogy a UI
      // szemantikailag ugyanaz maradjon, mint a mockban.
      if (created.type !== "SickLeave" && created.status === "Draft") {
        const body: LeaveVersionRequestDto = { expectedVersion: Number(created.version) };
        const submitted = await httpClient.post<LeaveRequestResponseDto>(
          `/api/me/leave-requests/${created.id}/submit`,
          body,
        );
        return mapLeaveFromBackend(submitted);
      }
      return mapLeaveFromBackend(created);
    },
    async withdrawMyRequest(requestId, expectedVersion) {
      const body: LeaveVersionRequestDto = {
        expectedVersion: requireVersion(expectedVersion, "kérelem visszavonás"),
      };
      const dto = await httpClient.post<LeaveRequestResponseDto>(
        `/api/me/leave-requests/${requestId}/withdraw`,
        body,
      );
      return mapLeaveFromBackend(dto);
    },
  };
}

function makeHttpAdminLeaveRequestService(): Services["adminLeaveRequest"] {
  return {
    async listRequests(query) {
      const params: Record<string, string | undefined> = {};
      if (query?.status) {
        // UI enum → backend PascalCase (első betű nagybetű, aláhúzás elmarad).
        params.status = query.status
          .split("_")
          .map((s) => s.charAt(0).toUpperCase() + s.slice(1))
          .join("");
      }
      const dtos = await httpClient.get<LeaveRequestResponseDto[]>(
        "/api/admin/leave-requests",
        params,
      );
      return dtos.map(mapLeaveFromBackend);
    },
    async createForEmployee(employeeId, input) {
      const dto = await httpClient.post<LeaveRequestResponseDto>(
        `/api/admin/employees/${employeeId}/leave-requests`,
        mapCreateLeaveRequest(input),
      );
      return mapLeaveFromBackend(dto);
    },
    async decide(requestId, decision) {
      const body: LeaveDecisionRequestDto = {
        decision: decision.action === "approve" ? "Approve" : "Reject",
        reason: decision.note ?? null,
        expectedVersion: requireVersion(decision.expectedVersion, "döntés"),
      };
      const dto = await httpClient.post<LeaveRequestResponseDto>(
        `/api/admin/leave-requests/${requestId}/decision`,
        body,
      );
      return mapLeaveFromBackend(dto);
    },
    async cancel(requestId, note, expectedVersion) {
      const body: CancelLeaveRequestDto = {
        reason: note,
        expectedVersion: requireVersion(expectedVersion, "kérelem törlés"),
      };
      const dto = await httpClient.post<LeaveRequestResponseDto>(
        `/api/admin/leave-requests/${requestId}/cancel`,
        body,
      );
      return mapLeaveFromBackend(dto);
    },
    async submit(requestId, expectedVersion) {
      const body: LeaveVersionRequestDto = {
        expectedVersion: requireVersion(expectedVersion, "beadás"),
      };
      const dto = await httpClient.post<LeaveRequestResponseDto>(
        `/api/admin/leave-requests/${requestId}/submit`,
        body,
      );
      return mapLeaveFromBackend(dto);
    },
    async record(requestId, expectedVersion) {
      const body: LeaveVersionRequestDto = {
        expectedVersion: requireVersion(expectedVersion, "táppénz rögzítés"),
      };
      const dto = await httpClient.post<LeaveRequestResponseDto>(
        `/api/admin/leave-requests/${requestId}/record`,
        body,
      );
      return mapLeaveFromBackend(dto);
    },
    async close(requestId, dateTo, expectedVersion) {
      const body: CloseSickLeaveRequestDto = {
        dateTo,
        expectedVersion: requireVersion(expectedVersion, "táppénz zárás"),
      };
      const dto = await httpClient.post<LeaveRequestResponseDto>(
        `/api/admin/leave-requests/${requestId}/close`,
        body,
      );
      return mapLeaveFromBackend(dto);
    },
  };
}

function makeHttpPayrollService(): PayrollService {
  return {
    async getSummary(employeeId) {
      return httpClient.get<PayrollOnboardingSummaryResponseDto>(
        `/api/admin/employees/${employeeId}/payroll-onboarding`,
      );
    },
    async getProfile(employeeId) {
      return httpClient.get<EmployeePayrollProfileResponseDto | null>(
        `/api/admin/employees/${employeeId}/payroll-profile`,
      );
    },
    async updateProfile(employeeId, input) {
      return httpClient.put<EmployeePayrollProfileResponseDto>(
        `/api/admin/employees/${employeeId}/payroll-profile`,
        mapProfileUpdateRequest(input),
      );
    },
    async completeOnboarding(employeeId, expectedProfileVersion) {
      return httpClient.post<PayrollOnboardingSummaryResponseDto>(
        `/api/admin/employees/${employeeId}/payroll-onboarding/complete`,
        { expectedProfileVersion },
      );
    },
    async exportOnboarding(employeeId, format) {
      // A base URL-t ugyanaz a resolver adja, mint a többi hívásnál: így a
      // relatív dev-proxy és az explicit VITE_API_URL is működik.
      const res = await fetch(
        buildUrl(`/api/admin/employees/${employeeId}/payroll-onboarding/export`, { format }),
        { credentials: "include" },
      );
      if (!res.ok) throw new Error(`Export sikertelen (${res.status}).`);
      return res.blob();
    },
    async getAdminSurvey(employeeId, taxYear) {
      return httpClient.get<TaxAllowanceSurveyResponseDto | null>(
        `/api/admin/employees/${employeeId}/tax-allowance-surveys/${taxYear}`,
      );
    },
    async listDeclarationRequirements(employeeId) {
      return httpClient.get<TaxDeclarationRequirementResponseDto[]>(
        `/api/admin/employees/${employeeId}/tax-declaration-requirements`,
      );
    },
    async adminUpdateSurvey(employeeId, taxYear, input) {
      return httpClient.put<TaxAllowanceSurveyResponseDto>(
        `/api/admin/employees/${employeeId}/tax-allowance-surveys/${taxYear}`,
        mapAdminSurveyUpdateRequest(input),
      );
    },
    async adminSubmitSurvey(id, expectedVersion) {
      const body: TaxSurveyVersionRequestDto = { expectedVersion };
      return httpClient.post<TaxAllowanceSurveyResponseDto>(
        `/api/admin/tax-allowance-surveys/${id}/submit`,
        body,
      );
    },
    async adminReopenSurvey(id, expectedVersion) {
      const body: TaxSurveyVersionRequestDto = { expectedVersion };
      return httpClient.post<TaxAllowanceSurveyResponseDto>(
        `/api/admin/tax-allowance-surveys/${id}/reopen`,
        body,
      );
    },
    async adminReviewSurvey(id, input) {
      const body: ReviewTaxAllowanceSurveyRequestDto = {
        hrPayrollNote: input.hrPayrollNote,
        expectedVersion: input.expectedVersion,
      };
      return httpClient.post<TaxAllowanceSurveyResponseDto>(
        `/api/admin/tax-allowance-surveys/${id}/review`,
        body,
      );
    },
    async adminCompleteSurvey(id, expectedVersion) {
      const body: TaxSurveyVersionRequestDto = { expectedVersion };
      return httpClient.post<TaxAllowanceSurveyResponseDto>(
        `/api/admin/tax-allowance-surveys/${id}/complete`,
        body,
      );
    },
    async updateDeclarationStatus(id, input) {
      return httpClient.put<TaxDeclarationRequirementResponseDto>(
        `/api/admin/tax-declaration-requirements/${id}/status`,
        mapDeclarationStatusRequest(input),
      );
    },
    async overrideDeclaration(id, input) {
      return httpClient.put<TaxDeclarationRequirementResponseDto>(
        `/api/admin/tax-declaration-requirements/${id}/override`,
        mapDeclarationOverrideRequest(input),
      );
    },
    async getMyOnboarding() {
      return httpClient.get<PayrollOnboardingSummaryResponseDto>("/api/me/payroll-onboarding");
    },
    async getMySurvey(taxYear) {
      return httpClient.get<TaxAllowanceSurveyResponseDto | null>(
        `/api/me/tax-allowance-surveys/${taxYear}`,
      );
    },
    async createMySurvey(input) {
      return httpClient.post<TaxAllowanceSurveyResponseDto>(
        "/api/me/tax-allowance-surveys",
        mapOwnSurveyCreateRequest(input),
      );
    },
    async updateMySurvey(id, input) {
      return httpClient.put<TaxAllowanceSurveyResponseDto>(
        `/api/me/tax-allowance-surveys/${id}`,
        mapOwnSurveyUpdateRequest(input),
      );
    },
    async submitMySurvey(id, expectedVersion) {
      const body: TaxSurveyVersionRequestDto = { expectedVersion };
      return httpClient.post<TaxAllowanceSurveyResponseDto>(
        `/api/me/tax-allowance-surveys/${id}/submit`,
        body,
      );
    },
  };
}
