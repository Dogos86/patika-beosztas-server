import type {
  AiActionPreview,
  AiCommandPreview,
  AdminUserSummary,
  AppPermission,
  CoverageRule,
  CreateShiftQuotaRuleInput,
  EmployeeCapabilitiesData,
  EmployeeShiftQuotaRule,
  EmployeeWorkProfile,
  Employee,
  GenerateRunInput,
  LeaveRequest,
  LeaveStatus,
  LeaveType,
  Location,
  LocationOpeningHours,
  LocationWeeklyOpening,
  ShiftTemplate,
  ShiftTemplateInput,
  Notification,
  PagedResponse,
  RegenerateScope,
  ScheduleRun,
  ShiftAlternative,
  Shift,
  StaffingCapability,
  UpdateShiftQuotaRuleInput,
  User,
  EmployeePayrollProfile,
  PayrollOnboardingSummary,
  TaxAllowanceSurvey,
  TaxAllowanceSurveyAnswers,
  TaxDeclarationRequirement,
  DeclarationRequirementStatus,
  PayrollProfileStatus,
  ScheduleGenerationRun,
  ShiftAssignment,
  ScheduleListItem,
  SchedulePlan,
  EmployeeScheduleMatrix,
  LocationCoverage,
  ScheduleIssueRow,
  ScheduleChange,
  ShiftAssignmentExplanation,
  ScheduleAlternative,
  RegenerationScopeInput,
  PendingLeaveHandling,
} from "./types";
import type { OwnScheduleView } from "./http/mappers/schedule";
import type { ScheduleGenerationWeightsRequestDto } from "./http/dto/schedule";

export interface AuthService {
  /** A jelenlegi bejelentkezett session (frontend cache; a valódi kép a backendben). */
  getSession(): Promise<User | null>;
  /** @deprecated `getSession` alias — átmeneti kompatibilitás. */
  getCurrentUser(): Promise<User | null>;
  login(email: string, password: string): Promise<User>;
  logout(): Promise<void>;
  requestPasswordReset(email: string): Promise<void>;
}

export interface ScheduleService {
  /** Saját beosztás (session-alapú). Kliens nem adhat meg employeeId-t. */
  getMySchedule(query: { from: string; to: string }): Promise<Shift[]>;
  /** Phase 3B — publikált saját beosztás (aktuális vagy megadott dátumra). */
  getOwnPublishedSchedule(query?: { date?: string }): Promise<OwnScheduleView | null>;
  /** Admin: bármely dolgozó/telephely beosztása. */
  listShifts(
    from: string,
    to: string,
    filters?: { employeeId?: string; locationId?: string },
  ): Promise<Shift[]>;
  upsertShift(shift: Omit<Shift, "id"> & { id?: string }): Promise<Shift>;
  deleteShift(id: string): Promise<void>;
  /** „Automatikus kitöltés" — demó algoritmus, végleges motor a backendben. */
  autoFill(weekStart: string, locationId: string): Promise<Shift[]>;
}

/** A generálás-központú munkatér mögötti szerviz. */
export interface ScheduleWorkspaceService {
  /** Új futás indítása — visszaadja a kész draft-ot műszakokkal és problémákkal. */
  generate(input: GenerateRunInput): Promise<ScheduleRun>;
  /** Aktuális (memóriában tárolt) legutóbbi futás. */
  getCurrentRun(): Promise<ScheduleRun | null>;
  /** Részleges újragenerálás egy scope-on belül (nap / hét / telephely / szerepkör / kijelölt problémák). */
  regenerateScope(runId: string, scope: RegenerateScope): Promise<ScheduleRun>;
  lockShift(runId: string, shiftId: string): Promise<ScheduleRun>;
  unlockShift(runId: string, shiftId: string): Promise<ScheduleRun>;
  rejectShift(runId: string, shiftId: string): Promise<ScheduleRun>;
  findAlternatives(runId: string, shiftId: string): Promise<ShiftAlternative[]>;
  approve(runId: string): Promise<ScheduleRun>;
  publish(runId: string): Promise<ScheduleRun>;
}

export interface MyLeaveRequestInput {
  type: LeaveType;
  fullDay: boolean;
  startDate: string;
  endDate?: string;
  startTime?: string;
  endTime?: string;
  note?: string;
}

/** Önkiszolgáló műveletek — a session dönt az identitásról, nem a kliens. */
export interface LeaveRequestService {
  listMyRequests(query?: { status?: LeaveStatus }): Promise<LeaveRequest[]>;
  createMyRequest(input: MyLeaveRequestInput): Promise<LeaveRequest>;
  /** Piszkozat visszavonása / függő kérelem visszavonása. Az `expectedVersion`
   *  a szerver által legutóbb visszaadott érték; HTTP módban kötelező. */
  withdrawMyRequest(requestId: string, expectedVersion?: number): Promise<LeaveRequest>;
}

export interface AdminLeaveRequestService {
  listRequests(query?: { status?: LeaveStatus }): Promise<LeaveRequest[]>;
  createForEmployee(employeeId: string, input: MyLeaveRequestInput): Promise<LeaveRequest>;
  decide(
    requestId: string,
    decision: { action: "approve" | "reject"; note?: string; expectedVersion?: number },
  ): Promise<LeaveRequest>;
  cancel(requestId: string, note: string, expectedVersion?: number): Promise<LeaveRequest>;
  /** Draft → Pending admin oldalról (dolgozó nevében beadás). */
  submit(requestId: string, expectedVersion?: number): Promise<LeaveRequest>;
  /** SickLeave: Reported → Recorded (HR rögzítette). */
  record(requestId: string, expectedVersion?: number): Promise<LeaveRequest>;
  /** SickLeave: Recorded → Closed a záró dátum megadásával. */
  close(requestId: string, dateTo: string, expectedVersion?: number): Promise<LeaveRequest>;
}

export interface EmployeeService {
  /** @deprecated Kompatibilitási wrapper — a `listAll()` kontrollált lapozást használ. */
  list(): Promise<Employee[]>;
  /** Dropdownokhoz: végiglapozott, felső korláttal védett teljes lista. */
  listAll(options?: ListAllOptions): Promise<Employee[]>;
  listPaged(query?: EmployeeListQuery): Promise<PagedResponse<Employee>>;
  get(id: string): Promise<Employee | null>;
  create(input: Omit<Employee, "id">): Promise<Employee>;
  update(id: string, input: Employee, expectedVersion: number): Promise<Employee>;

  // Phase 2E — külön endpointok a planning területhez.
  getCapabilities(employeeId: string): Promise<EmployeeCapabilitiesData>;
  updateCapabilities(
    employeeId: string,
    capabilities: StaffingCapability[],
    expectedEmployeeVersion: number,
  ): Promise<EmployeeCapabilitiesData>;

  /** 404 → null, még nincs perzisztált profil (első mentéskor jön létre). */
  getWorkProfile(employeeId: string): Promise<EmployeeWorkProfile | null>;
  updateWorkProfile(employeeId: string, input: EmployeeWorkProfile): Promise<EmployeeWorkProfile>;

  listQuotas(employeeId: string): Promise<EmployeeShiftQuotaRule[]>;
  createQuota(
    employeeId: string,
    input: CreateShiftQuotaRuleInput,
  ): Promise<EmployeeShiftQuotaRule>;
  updateQuota(id: string, input: UpdateShiftQuotaRuleInput): Promise<EmployeeShiftQuotaRule>;
  deactivateQuota(id: string, expectedVersion: number): Promise<EmployeeShiftQuotaRule>;
}

export interface EmployeeListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  includeInactive?: boolean;
}

export interface LocationListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  includeInactive?: boolean;
}

export interface ListAllOptions {
  includeInactive?: boolean;
  /** Lapméret a végiglapozáshoz (alap: 100). */
  pageSize?: number;
  search?: string;
  maxItems?: number;
  signal?: AbortSignal;
}

export interface CreateLocationInput {
  name: string;
  kind: Location["kind"];
  address?: string | null;
  active: boolean;
}

export interface LocationService {
  /** @deprecated Kompatibilitási wrapper a régi hívókhoz — lapozott választ bont ki. */
  list(): Promise<Location[]>;
  listPaged(query?: LocationListQuery): Promise<PagedResponse<Location>>;
  listAll(options?: ListAllOptions): Promise<Location[]>;
  get(id: string): Promise<Location | null>;
  create(input: CreateLocationInput): Promise<Location>;
  update(id: string, input: CreateLocationInput, expectedVersion: number): Promise<Location>;

  getWeeklyOpening(locationId: string): Promise<LocationWeeklyOpening | null>;
  updateWeeklyOpening(
    locationId: string,
    hours: LocationOpeningHours,
    expectedVersion: number | null,
  ): Promise<LocationWeeklyOpening>;

  listShiftTemplates(locationId: string, includeInactive?: boolean): Promise<ShiftTemplate[]>;
  createShiftTemplate(locationId: string, input: ShiftTemplateInput): Promise<ShiftTemplate>;
  updateShiftTemplate(
    id: string,
    input: ShiftTemplateInput,
    expectedVersion: number,
  ): Promise<ShiftTemplate>;
  deactivateShiftTemplate(id: string, expectedVersion: number): Promise<ShiftTemplate>;
}

export interface CoverageListQuery {
  locationId?: string;
  capability?: import("./types").StaffingCapability;
  weekday?: number;
  includeInactive?: boolean;
}

export interface CoverageService {
  list(query?: CoverageListQuery): Promise<CoverageRule[]>;
  /** Új szabály — a rule.id-t figyelmen kívül hagyjuk. */
  create(rule: CoverageRule): Promise<CoverageRule>;
  /** Meglévő szabály módosítása; kötelező `expectedVersion`. */
  update(id: string, rule: CoverageRule, expectedVersion: number): Promise<CoverageRule>;
  /** Deaktiválás (soft-delete a backend szerint). */
  deactivate(id: string, expectedVersion: number): Promise<CoverageRule>;
  /** Backward-compat wrapper — új esetén create, egyébként update (`__version` metaadattal). */
  save(rule: CoverageRule & { __version?: number }): Promise<CoverageRule>;
  /** Backward-compat wrapper — a `save` mellé; a Phase 2E-ben deactivate ajánlott. */
  delete(id: string, expectedVersion?: number): Promise<void>;
}

export interface UserListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  includeInactive?: boolean;
}

export interface CreateUserInput {
  email: string;
  displayName: string;
  initialPassword: string;
  permissions: AppPermission[];
  linkedEmployeeId?: string | null;
}

export interface UpdateUserPermissionsInput {
  permissions: AppPermission[];
  expectedVersion: number;
}

export interface UpdateUserEmployeeLinkInput {
  linkedEmployeeId: string | null;
  expectedVersion: number;
}

export interface UpdateUserStatusInput {
  active: boolean;
  expectedVersion: number;
}

export interface UserService {
  list(query?: UserListQuery): Promise<PagedResponse<AdminUserSummary>>;
  create(input: CreateUserInput): Promise<AdminUserSummary>;
  updatePermissions(id: string, input: UpdateUserPermissionsInput): Promise<AdminUserSummary>;
  updateEmployeeLink(id: string, input: UpdateUserEmployeeLinkInput): Promise<AdminUserSummary>;
  setStatus(id: string, input: UpdateUserStatusInput): Promise<AdminUserSummary>;
}

export interface NotificationService {
  listForUser(userId: string): Promise<Notification[]>;
  markRead(id: string): Promise<void>;
}

/** Saját munkavégzési kérések — a session dönt az identitásról (nincs employeeId). */
export interface WorkPreferenceService {
  listMine(includeInactive?: boolean): Promise<import("./types").WorkPreference[]>;
  createMine(
    input: import("./types").WorkPreferenceInput,
  ): Promise<import("./types").WorkPreference>;
  updateMine(
    id: string,
    input: import("./types").WorkPreferenceInput,
    expectedVersion: number,
  ): Promise<import("./types").WorkPreference>;
  deactivateMine(id: string, expectedVersion: number): Promise<import("./types").WorkPreference>;
}

/** Admin oldali munkavégzési kérés kezelés (Permission:ManageWorkPreferences). */
export interface AdminWorkPreferenceService {
  listForEmployee(
    employeeId: string,
    includeInactive?: boolean,
  ): Promise<import("./types").WorkPreference[]>;
  createForEmployee(
    employeeId: string,
    input: import("./types").WorkPreferenceInput,
  ): Promise<import("./types").WorkPreference>;
  update(
    id: string,
    input: import("./types").WorkPreferenceInput,
    expectedVersion: number,
  ): Promise<import("./types").WorkPreference>;
  deactivate(id: string, expectedVersion: number): Promise<import("./types").WorkPreference>;
}

export interface AiAssistantService {
  /** @deprecated régi mock — használd az `interpretCommand` metódust. */
  interpret(text: string): Promise<AiActionPreview[]>;
  interpretCommand(input: { text: string }): Promise<AiCommandPreview>;
  answerClarification(
    previewId: string,
    clarificationId: string,
    answer: string,
  ): Promise<AiCommandPreview>;
  executeCommand(previewId: string, confirmationToken: string): Promise<{ auditId: string }>;
}

export interface Services {
  auth: AuthService;
  schedule: ScheduleService;
  scheduleWorkspace: ScheduleWorkspaceService;
  scheduleGeneration: ScheduleGenerationService;
  adminSchedule: AdminScheduleService;
  leaveRequest: LeaveRequestService;
  adminLeaveRequest: AdminLeaveRequestService;
  workPreference: WorkPreferenceService;
  adminWorkPreference: AdminWorkPreferenceService;
  employee: EmployeeService;
  location: LocationService;
  coverage: CoverageService;
  notification: NotificationService;
  ai: AiAssistantService;
  user: UserService;
  payroll: PayrollService;
}

// ─── Phase 3B — Schedule generation & admin ────────────────────────

export interface StartScheduleGenerationInput {
  periodStart: string;
  periodEnd: string;
  deterministicSeed?: number | null;
  maxSolveSeconds?: number | null;
  workerCount?: number | null;
  pendingLeaveHandling?: PendingLeaveHandling;
  weights?: ScheduleGenerationWeightsRequestDto | null;
}

export interface ScheduleGenerationService {
  start(input: StartScheduleGenerationInput): Promise<ScheduleGenerationRun>;
  get(runId: string): Promise<ScheduleGenerationRun>;
  cancel(runId: string, expectedVersion: number): Promise<ScheduleGenerationRun>;
}

export interface RegenerateScheduleInput {
  scope: RegenerationScopeInput;
  expectedVersion: number;
  deterministicSeed?: number | null;
  maxSolveSeconds?: number | null;
  workerCount?: number | null;
  pendingLeaveHandling?: PendingLeaveHandling;
  weights?: ScheduleGenerationWeightsRequestDto | null;
}

export interface AdminScheduleService {
  list(query?: { status?: string }): Promise<ScheduleListItem[]>;
  get(scheduleId: string): Promise<SchedulePlan>;
  getMatrix(scheduleId: string): Promise<EmployeeScheduleMatrix>;
  getCoverage(scheduleId: string): Promise<LocationCoverage>;
  listIssues(scheduleId: string): Promise<ScheduleIssueRow[]>;
  listChanges(scheduleId: string): Promise<ScheduleChange[]>;
  explainShift(scheduleId: string, shiftId: string): Promise<ShiftAssignmentExplanation>;
  findAlternatives(scheduleId: string, shiftId: string): Promise<ScheduleAlternative[]>;
  lockShift(
    scheduleId: string,
    shiftId: string,
    body: { expectedShiftVersion: number; expectedScheduleVersion: number; reason?: string },
  ): Promise<ShiftAssignment>;
  unlockShift(
    scheduleId: string,
    shiftId: string,
    body: { expectedShiftVersion: number; expectedScheduleVersion: number; reason?: string },
  ): Promise<ShiftAssignment>;
  rejectShift(
    scheduleId: string,
    shiftId: string,
    body: {
      expectedShiftVersion: number;
      expectedScheduleVersion: number;
      reason: string;
      exclusionScope?: "Run" | "Schedule" | "Period";
    },
  ): Promise<ShiftAssignment>;
  replaceShift(
    scheduleId: string,
    shiftId: string,
    body: {
      replacementEmployeeId: string;
      expectedShiftVersion: number;
      expectedScheduleVersion: number;
      reason: string;
    },
  ): Promise<ShiftAssignment>;
  regenerate(scheduleId: string, input: RegenerateScheduleInput): Promise<ScheduleGenerationRun>;
  submitForReview(scheduleId: string, expectedVersion: number): Promise<SchedulePlan>;
  returnToDraft(scheduleId: string, expectedVersion: number): Promise<SchedulePlan>;
  approve(scheduleId: string, expectedVersion: number): Promise<SchedulePlan>;
  publish(scheduleId: string, expectedVersion: number): Promise<SchedulePlan>;
  archive(scheduleId: string, expectedVersion: number): Promise<SchedulePlan>;
  cloneDraft(scheduleId: string, expectedVersion: number): Promise<SchedulePlan>;
}

export interface UpdatePayrollProfileInput {
  employeeNumber: string;
  taxIdentificationNumber: string | null;
  employmentStartDate: string;
  payrollExternalId: string | null;
  status: PayrollProfileStatus;
  expectedVersion: number | null;
}

export interface AdminUpdateSurveyInput {
  effectiveFrom: string;
  answers: TaxAllowanceSurveyAnswers;
  hrPayrollNote: string | null;
  expectedVersion: number | null;
}

export interface OwnCreateSurveyInput {
  taxYear: number;
  effectiveFrom: string;
  answers: TaxAllowanceSurveyAnswers;
}

export interface OwnUpdateSurveyInput {
  effectiveFrom: string;
  answers: TaxAllowanceSurveyAnswers;
  expectedVersion: number;
}

export interface UpdateDeclarationStatusInput {
  status: DeclarationRequirementStatus;
  effectiveTo: string | null;
  notes: string | null;
  expectedVersion: number;
}

export interface OverrideDeclarationInput {
  requiredDecision: boolean;
  status: DeclarationRequirementStatus;
  reason: string;
  effectiveTo: string | null;
  expectedVersion: number;
}

export interface PayrollService {
  // Admin
  getSummary(employeeId: string): Promise<PayrollOnboardingSummary>;
  getProfile(employeeId: string): Promise<EmployeePayrollProfile | null>;
  updateProfile(
    employeeId: string,
    input: UpdatePayrollProfileInput,
  ): Promise<EmployeePayrollProfile>;
  completeOnboarding(
    employeeId: string,
    expectedProfileVersion: number,
  ): Promise<PayrollOnboardingSummary>;
  exportOnboarding(employeeId: string, format: "json" | "csv"): Promise<Blob>;

  getAdminSurvey(employeeId: string, taxYear: number): Promise<TaxAllowanceSurvey | null>;
  listDeclarationRequirements(employeeId: string): Promise<TaxDeclarationRequirement[]>;
  adminUpdateSurvey(
    employeeId: string,
    taxYear: number,
    input: AdminUpdateSurveyInput,
  ): Promise<TaxAllowanceSurvey>;
  adminSubmitSurvey(id: string, expectedVersion: number): Promise<TaxAllowanceSurvey>;
  adminReopenSurvey(id: string, expectedVersion: number): Promise<TaxAllowanceSurvey>;
  adminReviewSurvey(
    id: string,
    input: { hrPayrollNote: string | null; expectedVersion: number },
  ): Promise<TaxAllowanceSurvey>;
  adminCompleteSurvey(id: string, expectedVersion: number): Promise<TaxAllowanceSurvey>;
  updateDeclarationStatus(
    id: string,
    input: UpdateDeclarationStatusInput,
  ): Promise<TaxDeclarationRequirement>;
  overrideDeclaration(
    id: string,
    input: OverrideDeclarationInput,
  ): Promise<TaxDeclarationRequirement>;

  // Self-service
  getMyOnboarding(): Promise<PayrollOnboardingSummary>;
  getMySurvey(taxYear: number): Promise<TaxAllowanceSurvey | null>;
  createMySurvey(input: OwnCreateSurveyInput): Promise<TaxAllowanceSurvey>;
  updateMySurvey(id: string, input: OwnUpdateSurveyInput): Promise<TaxAllowanceSurvey>;
  submitMySurvey(id: string, expectedVersion: number): Promise<TaxAllowanceSurvey>;
}
