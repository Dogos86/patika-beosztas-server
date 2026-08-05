import type {
  AdminLeaveRequestService,
  AiAssistantService,
  AuthService,
  CoverageService,
  CreateUserInput,
  EmployeeService,
  LeaveRequestService,
  LocationService,
  MyLeaveRequestInput,
  NotificationService,
  ScheduleService,
  Services,
  UserService,
} from "../interfaces";
import type {
  AdminUserSummary,
  AiCommandPreview,
  CreateShiftQuotaRuleInput,
  CoverageRule,
  EmployeeCapabilitiesData,
  EmployeeShiftQuotaRule,
  EmployeeWorkProfile,
  LeaveRequest,
  Location,
  Shift,
  ShiftTemplate,
  StaffingCapability,
  UpdateShiftQuotaRuleInput,
  User,
} from "../types";
import { employeeCapabilities } from "@/lib/capability-map";
import { toLocalDateISO } from "@/lib/format";
import { defaultOpeningHours } from "@/lib/opening-hours";
import * as seed from "./seed";
import {
  mockGenerate,
  mockGetCurrentRun,
  mockRegenerateScope,
  mockLockShift,
  mockRejectShift,
  mockFindAlternatives,
  mockApprove,
  mockPublish,
} from "./scheduleWorkspace";
import type { ScheduleWorkspaceService } from "../interfaces";
import { bindPayrollMockContext, makePayrollService } from "./payroll";

const SESSION_KEY = "patika_session_user_id";

// In-memory store (initialized from seed)
const store = {
  users: [...seed.users],
  employees: [...seed.employees],
  locations: [...seed.locations],
  shifts: [...seed.shifts],
  leaveRequests: [...seed.leaveRequests],
  coverageRules: [...seed.coverageRules],
  notifications: [...seed.notifications],
};

const delay = (ms = 150) => new Promise((r) => setTimeout(r, ms));
const uid = () => Math.random().toString(36).slice(2, 10);

function currentUserSync(): User | null {
  if (typeof window === "undefined") return null;
  const id = window.localStorage.getItem(SESSION_KEY);
  return store.users.find((u) => u.id === id) ?? null;
}

function requireSession(): User {
  const u = currentUserSync();
  if (!u) throw new Error("Nincs bejelentkezett felhasználó.");
  return u;
}

function requireMyEmployeeId(): { user: User; employeeId: string } {
  const user = requireSession();
  if (!user.linkedEmployee) throw new Error("A felhasználódhoz nem tartozik dolgozói profil.");
  return { user, employeeId: user.linkedEmployee.id };
}

const authService: AuthService = {
  async getSession() {
    await delay(30);
    return currentUserSync();
  },
  async getCurrentUser() {
    await delay(30);
    return currentUserSync();
  },
  async login(email, password) {
    await delay();
    const user = store.users.find((u) => u.email.toLowerCase() === email.toLowerCase());
    if (!user || password !== "demo") {
      throw new Error("Hibás email vagy jelszó.");
    }
    if (typeof window !== "undefined") {
      window.localStorage.setItem(SESSION_KEY, user.id);
    }
    return user;
  },
  async logout() {
    await delay(50);
    if (typeof window !== "undefined") {
      window.localStorage.removeItem(SESSION_KEY);
    }
  },
  async requestPasswordReset() {
    await delay();
  },
};

const scheduleService: ScheduleService = {
  async getMySchedule({ from, to }) {
    await delay();
    const { employeeId } = requireMyEmployeeId();
    return store.shifts.filter(
      (s) => s.employeeId === employeeId && s.date >= from && s.date <= to,
    );
  },
  async getOwnPublishedSchedule() {
    // Mock módban a Phase 3B saját beosztás endpoint nem elérhető — a UI
    // ilyenkor a régi `getMySchedule` alapú heti nézetet mutatja.
    return null;
  },
  async listShifts(from, to, filters) {
    await delay();
    return store.shifts.filter(
      (s) =>
        s.date >= from &&
        s.date <= to &&
        (!filters?.employeeId || s.employeeId === filters.employeeId) &&
        (!filters?.locationId || s.locationId === filters.locationId),
    );
  },
  async upsertShift(shift) {
    await delay();
    if (shift.id) {
      const idx = store.shifts.findIndex((s) => s.id === shift.id);
      if (idx >= 0) {
        store.shifts[idx] = shift as Shift;
        return store.shifts[idx];
      }
    }
    const created: Shift = { ...(shift as Shift), id: uid() };
    store.shifts.push(created);
    return created;
  },
  async deleteShift(id) {
    await delay();
    store.shifts = store.shifts.filter((s) => s.id !== id);
  },
  async autoFill(weekStart, locationId) {
    await delay(400);
    // Demó algoritmus: aktív + beosztható + autofillbe bevont + adott telephely
    // (a végleges kitöltő motor a backendben készül).
    const created: Shift[] = [];
    const candidates = store.employees.filter(
      (e) => e.active && e.schedulable && e.includeInAutoFill && e.locationIds.includes(locationId),
    );
    candidates.forEach((emp, idx) => {
      const d = new Date(weekStart);
      d.setDate(d.getDate() + (idx % 5));
      const s: Shift = {
        id: uid(),
        employeeId: emp.id,
        locationId,
        date: toLocalDateISO(d),
        start: "08:00",
        end: "16:00",
        type: "work",
        status: "draft",
      };
      store.shifts.push(s);
      created.push(s);
    });
    return created;
  },
};

function buildRequest(
  employeeId: string,
  userId: string,
  input: MyLeaveRequestInput,
): LeaveRequest {
  const now = new Date().toISOString();
  const isSick = input.type === "sick_leave";
  return {
    id: uid(),
    employeeId,
    type: input.type,
    fullDay: input.fullDay,
    startDate: input.startDate,
    endDate: input.endDate ?? input.startDate,
    startTime: input.fullDay ? undefined : input.startTime,
    endTime: input.fullDay ? undefined : input.endTime,
    note: input.note,
    status: isSick ? "reported" : "pending",
    createdAt: now,
    createdByUserId: userId,
    history: [{ at: now, actorUserId: userId, action: isSick ? "reported" : "created" }],
    version: 1,
  };
}

function bumpVersion(req: LeaveRequest) {
  req.version = (req.version ?? 1) + 1;
}

const leaveRequestService: LeaveRequestService = {
  async listMyRequests(query) {
    await delay();
    const { employeeId } = requireMyEmployeeId();
    return store.leaveRequests.filter(
      (r) => r.employeeId === employeeId && (!query?.status || r.status === query.status),
    );
  },
  async createMyRequest(input) {
    await delay();
    const { user, employeeId } = requireMyEmployeeId();
    const req = buildRequest(employeeId, user.id, input);
    store.leaveRequests.push(req);
    return req;
  },
  async withdrawMyRequest(requestId, _expectedVersion) {
    await delay();
    const { user, employeeId } = requireMyEmployeeId();
    const req = store.leaveRequests.find((r) => r.id === requestId);
    if (!req) throw new Error("Nem található kérelem.");
    if (req.employeeId !== employeeId) throw new Error("Csak a saját kérelmed vonhatod vissza.");
    if (req.status !== "pending" && req.status !== "draft")
      throw new Error("Csak függő vagy piszkozat kérelem vonható vissza.");
    req.status = "withdrawn";
    req.history.push({ at: new Date().toISOString(), actorUserId: user.id, action: "withdrawn" });
    bumpVersion(req);
    return req;
  },
};

const adminLeaveRequestService: AdminLeaveRequestService = {
  async listRequests(query) {
    await delay();
    const status = query?.status ?? "pending";
    return store.leaveRequests.filter((r) => r.status === status);
  },
  async createForEmployee(employeeId, input) {
    await delay();
    const user = requireSession();
    const req = buildRequest(employeeId, user.id, input);
    store.leaveRequests.push(req);
    return req;
  },
  async decide(requestId, decision) {
    await delay();
    const user = requireSession();
    const req = store.leaveRequests.find((r) => r.id === requestId);
    if (!req) throw new Error("Nem található kérelem.");
    req.status = decision.action === "approve" ? "approved" : "rejected";
    req.decisionNote = decision.note;
    req.history.push({
      at: new Date().toISOString(),
      actorUserId: user.id,
      action: decision.action === "approve" ? "approved" : "rejected",
      note: decision.note,
    });
    bumpVersion(req);
    return req;
  },
  async cancel(requestId, note) {
    await delay();
    const user = requireSession();
    const req = store.leaveRequests.find((r) => r.id === requestId);
    if (!req) throw new Error("Nem található kérelem.");
    req.status = "cancelled";
    req.history.push({
      at: new Date().toISOString(),
      actorUserId: user.id,
      action: "cancelled",
      note,
    });
    bumpVersion(req);
    return req;
  },
  async submit(requestId) {
    await delay();
    const user = requireSession();
    const req = store.leaveRequests.find((r) => r.id === requestId);
    if (!req) throw new Error("Nem található kérelem.");
    if (req.status !== "draft") throw new Error("Csak piszkozat adható be.");
    req.status = "pending";
    req.history.push({ at: new Date().toISOString(), actorUserId: user.id, action: "created" });
    bumpVersion(req);
    return req;
  },
  async record(requestId) {
    await delay();
    const user = requireSession();
    const req = store.leaveRequests.find((r) => r.id === requestId);
    if (!req) throw new Error("Nem található kérelem.");
    if (req.status !== "reported") throw new Error("Csak bejelentett táppénz rögzíthető.");
    req.status = "recorded";
    req.history.push({ at: new Date().toISOString(), actorUserId: user.id, action: "reported" });
    bumpVersion(req);
    return req;
  },
  async close(requestId, dateTo) {
    await delay();
    const user = requireSession();
    const req = store.leaveRequests.find((r) => r.id === requestId);
    if (!req) throw new Error("Nem található kérelem.");
    if (req.status !== "recorded") throw new Error("Csak rögzített táppénz zárható.");
    req.status = "closed";
    req.endDate = dateTo;
    req.history.push({ at: new Date().toISOString(), actorUserId: user.id, action: "cancelled" });
    bumpVersion(req);
    return req;
  },
};

const employeeService: EmployeeService = {
  async list() {
    await delay();
    return [...store.employees];
  },
  async listAll(options) {
    await delay();
    const all = store.employees.filter((e) =>
      (options?.includeInactive ?? true) ? true : e.active,
    );
    return all.slice(0, options?.maxItems ?? 2000);
  },
  async listPaged(query) {
    await delay();
    const page = query?.page ?? 1;
    const pageSize = query?.pageSize ?? 20;
    const search = (query?.search ?? "").trim().toLowerCase();
    const includeInactive = query?.includeInactive ?? false;
    const withLinked = store.employees.map((e) => ({
      ...e,
      linkedUser: (() => {
        const u = store.users.find((x) => x.linkedEmployee?.id === e.id);
        return u
          ? { userId: u.id, email: u.email, displayName: u.displayName, active: u.active ?? true }
          : null;
      })(),
    }));
    const filtered = withLinked
      .filter((e) => (includeInactive ? true : e.active))
      .filter(
        (e) =>
          !search ||
          e.fullName.toLowerCase().includes(search) ||
          e.displayName.toLowerCase().includes(search),
      );
    const start = (page - 1) * pageSize;
    return {
      items: filtered.slice(start, start + pageSize),
      total: filtered.length,
      page,
      pageSize,
    };
  },
  async get(id) {
    await delay();
    return store.employees.find((e) => e.id === id) ?? null;
  },
  async create(input) {
    await delay();
    const created = { ...input, id: `emp-${uid()}` } as (typeof store.employees)[number];
    store.employees.push(created);
    return created;
  },
  async update(id, input, _expectedVersion) {
    await delay();
    const idx = store.employees.findIndex((e) => e.id === id);
    if (idx < 0) throw new Error("Nem található dolgozó.");
    store.employees[idx] = { ...input, id };
    return store.employees[idx];
  },
  async getCapabilities(employeeId) {
    await delay();
    const e = store.employees.find((x) => x.id === employeeId);
    if (!e) throw new Error("Nem található dolgozó.");
    const assigned = e.capabilities ?? [];
    const effective =
      e.capabilities && e.capabilities.length > 0 ? e.capabilities : employeeCapabilities(e);
    return {
      employeeId,
      assignedCapabilities: assigned,
      effectiveCapabilities: effective,
      countsAsPharmacistCompatibility: e.countsAsPharmacist,
      employeeVersion: 1,
    };
  },
  async updateCapabilities(employeeId, capabilities, _expectedEmployeeVersion) {
    await delay();
    const idx = store.employees.findIndex((x) => x.id === employeeId);
    if (idx < 0) throw new Error("Nem található dolgozó.");
    store.employees[idx] = { ...store.employees[idx], capabilities: [...capabilities] };
    return {
      employeeId,
      assignedCapabilities: [...capabilities],
      effectiveCapabilities: [...capabilities],
      countsAsPharmacistCompatibility: store.employees[idx].countsAsPharmacist,
      employeeVersion: 1,
    };
  },
  async getWorkProfile(employeeId) {
    await delay();
    return mockWorkProfiles.get(employeeId) ?? null;
  },
  async updateWorkProfile(employeeId, input) {
    await delay();
    const stored: EmployeeWorkProfile = {
      ...input,
      id: input.id ?? `wp-${employeeId}`,
      version: (input.version ?? 0) + 1,
    };
    mockWorkProfiles.set(employeeId, stored);
    return stored;
  },
  async listQuotas(employeeId) {
    await delay();
    return (mockQuotas.get(employeeId) ?? []).filter((q) => q.isActive);
  },
  async createQuota(employeeId, input) {
    await delay();
    const created: EmployeeShiftQuotaRule = {
      id: `q-${uid()}`,
      employeeId,
      ...input,
      version: 1,
    };
    const arr = mockQuotas.get(employeeId) ?? [];
    arr.push(created);
    mockQuotas.set(employeeId, arr);
    return created;
  },
  async updateQuota(id, input) {
    await delay();
    for (const [k, arr] of mockQuotas.entries()) {
      const idx = arr.findIndex((q) => q.id === id);
      if (idx >= 0) {
        arr[idx] = { ...arr[idx], ...input, version: arr[idx].version + 1 };
        mockQuotas.set(k, arr);
        return arr[idx];
      }
    }
    throw new Error("Nem található kvóta.");
  },
  async deactivateQuota(id, _expectedVersion) {
    await delay();
    for (const [k, arr] of mockQuotas.entries()) {
      const idx = arr.findIndex((q) => q.id === id);
      if (idx >= 0) {
        arr[idx] = { ...arr[idx], isActive: false, version: arr[idx].version + 1 };
        mockQuotas.set(k, arr);
        return arr[idx];
      }
    }
    throw new Error("Nem található kvóta.");
  },
};

// Mock in-memory tárolók a planning területekhez.
const mockWorkProfiles = new Map<string, EmployeeWorkProfile>();
const mockWorkPreferences = new Map<string, import("../types").WorkPreference[]>();

function makeMockPreference(
  employeeId: string,
  employeeDisplayName: string,
  input: import("../types").WorkPreferenceInput,
): import("../types").WorkPreference {
  return {
    id: `wpref-${uid()}`,
    employeeId,
    employeeDisplayName,
    ...input,
    startTime: input.isFullDay ? null : input.startTime,
    endTime: input.isFullDay ? null : input.endTime,
    locationName: null,
    isActive: true,
    version: 1,
  };
}

function findMockPreference(id: string) {
  for (const [key, arr] of mockWorkPreferences.entries()) {
    const idx = arr.findIndex((p) => p.id === id);
    if (idx >= 0) return { key, arr, idx };
  }
  throw new Error("Nem található munkavégzési kérés.");
}

const adminWorkPreferenceService: import("../interfaces").AdminWorkPreferenceService = {
  async listForEmployee(employeeId, includeInactive) {
    await delay();
    const all = mockWorkPreferences.get(employeeId) ?? [];
    return includeInactive ? [...all] : all.filter((p) => p.isActive);
  },
  async createForEmployee(employeeId, input) {
    await delay();
    const name = store.employees.find((e) => e.id === employeeId)?.displayName ?? employeeId;
    const created = makeMockPreference(employeeId, name, input);
    mockWorkPreferences.set(employeeId, [...(mockWorkPreferences.get(employeeId) ?? []), created]);
    return created;
  },
  async update(id, input, expectedVersion) {
    await delay();
    const { key, arr, idx } = findMockPreference(id);
    if (arr[idx].version !== expectedVersion) {
      throw new Error("Konkurens módosítás történt. Töltsd újra az adatokat.");
    }
    arr[idx] = {
      ...arr[idx],
      ...input,
      startTime: input.isFullDay ? null : input.startTime,
      endTime: input.isFullDay ? null : input.endTime,
      version: arr[idx].version + 1,
    };
    mockWorkPreferences.set(key, arr);
    return arr[idx];
  },
  async deactivate(id, expectedVersion) {
    await delay();
    const { key, arr, idx } = findMockPreference(id);
    if (arr[idx].version !== expectedVersion) {
      throw new Error("Konkurens módosítás történt. Töltsd újra az adatokat.");
    }
    arr[idx] = { ...arr[idx], isActive: false, version: arr[idx].version + 1 };
    mockWorkPreferences.set(key, arr);
    return arr[idx];
  },
};

/** Saját kérések — a mock „session" a bejelentkezett user kapcsolt dolgozója. */
const workPreferenceService: import("../interfaces").WorkPreferenceService = {
  async listMine(includeInactive) {
    return adminWorkPreferenceService.listForEmployee(requireMockEmployeeId(), includeInactive);
  },
  async createMine(input) {
    return adminWorkPreferenceService.createForEmployee(requireMockEmployeeId(), input);
  },
  async updateMine(id, input, expectedVersion) {
    return adminWorkPreferenceService.update(id, input, expectedVersion);
  },
  async deactivateMine(id, expectedVersion) {
    return adminWorkPreferenceService.deactivate(id, expectedVersion);
  },
};

function requireMockEmployeeId(): string {
  const id = currentUserSync()?.linkedEmployee?.id;
  if (!id) throw new Error("Nincs kapcsolt dolgozói profil.");
  return id;
}
const mockQuotas = new Map<string, EmployeeShiftQuotaRule[]>();
// Referenciák, hogy ne legyen unused import a fenti típusokra.
export type _MockPlanningTypes =
  | StaffingCapability
  | EmployeeCapabilitiesData
  | CreateShiftQuotaRuleInput
  | UpdateShiftQuotaRuleInput;

const mockOpeningVersions = new Map<string, number>();

function mockTemplates(locationId: string): ShiftTemplate[] {
  const loc = store.locations.find((l) => l.id === locationId);
  if (!loc) return [];
  loc.templates = loc.templates ?? [];
  return loc.templates;
}

const locationService: LocationService = {
  async list() {
    await delay();
    return [...store.locations];
  },
  async listPaged(query) {
    await delay();
    const page = query?.page ?? 1;
    const pageSize = query?.pageSize ?? 25;
    const search = (query?.search ?? "").trim().toLowerCase();
    const includeInactive = query?.includeInactive ?? false;
    const filtered = store.locations
      .filter((l) => (includeInactive ? true : l.active))
      .filter((l) => (search ? l.name.toLowerCase().includes(search) : true));
    return {
      items: filtered.slice((page - 1) * pageSize, page * pageSize),
      total: filtered.length,
      page,
      pageSize,
    };
  },
  async listAll(options) {
    await delay();
    const all = store.locations.filter((l) =>
      (options?.includeInactive ?? true) ? true : l.active,
    );
    return all.slice(0, options?.maxItems ?? 2000);
  },
  async get(id) {
    await delay();
    return store.locations.find((l) => l.id === id) ?? null;
  },
  async create(input) {
    await delay();
    const loc: Location = {
      id: `loc-${Math.random().toString(36).slice(2, 8)}`,
      name: input.name,
      kind: input.kind,
      address: input.address ?? null,
      active: input.active,
      version: 1,
    };
    store.locations.push(loc);
    return loc;
  },
  async update(id, input, expectedVersion) {
    await delay();
    const loc = store.locations.find((l) => l.id === id);
    if (!loc) throw new Error("A telephely nem található.");
    if (loc.version !== undefined && loc.version !== expectedVersion) {
      throw new Error("Konkurens módosítás történt. Töltsd újra az adatokat.");
    }
    loc.name = input.name;
    loc.kind = input.kind;
    loc.address = input.address ?? null;
    loc.active = input.active;
    loc.version = (loc.version ?? 1) + 1;
    return loc;
  },
  async getWeeklyOpening(locationId) {
    await delay();
    const loc = store.locations.find((l) => l.id === locationId);
    if (!loc) return null;
    return {
      locationId,
      hours: loc.openingHours ?? defaultOpeningHours(),
      warnings: [],
      version: mockOpeningVersions.get(locationId) ?? 1,
    };
  },
  async updateWeeklyOpening(locationId, hours, expectedVersion) {
    await delay();
    const loc = store.locations.find((l) => l.id === locationId);
    if (!loc) throw new Error("A telephely nem található.");
    const current = mockOpeningVersions.get(locationId) ?? 1;
    if (expectedVersion !== null && expectedVersion !== current) {
      throw new Error("Konkurens módosítás történt. Töltsd újra az adatokat.");
    }
    loc.openingHours = hours;
    const next = current + 1;
    mockOpeningVersions.set(locationId, next);
    return { locationId, hours, warnings: [], version: next };
  },
  async listShiftTemplates(locationId, includeInactive) {
    await delay();
    const items = mockTemplates(locationId);
    return (includeInactive ?? true) ? [...items] : items.filter((t) => t.active);
  },
  async createShiftTemplate(locationId, input) {
    await delay();
    const items = mockTemplates(locationId);
    const created: ShiftTemplate = {
      ...input,
      id: `tpl-${Math.random().toString(36).slice(2, 8)}`,
      locationId,
      version: 1,
    };
    items.push(created);
    return created;
  },
  async updateShiftTemplate(id, input, expectedVersion) {
    await delay();
    for (const loc of store.locations) {
      const idx = (loc.templates ?? []).findIndex((t) => t.id === id);
      if (idx >= 0) {
        const current = loc.templates![idx];
        if (current.version !== undefined && current.version !== expectedVersion) {
          throw new Error("Konkurens módosítás történt. Töltsd újra az adatokat.");
        }
        const next: ShiftTemplate = {
          ...current,
          ...input,
          version: (current.version ?? 1) + 1,
        };
        loc.templates![idx] = next;
        return next;
      }
    }
    throw new Error("A sablon nem található.");
  },
  async deactivateShiftTemplate(id, expectedVersion) {
    await delay();
    for (const loc of store.locations) {
      const idx = (loc.templates ?? []).findIndex((t) => t.id === id);
      if (idx >= 0) {
        const current = loc.templates![idx];
        if (current.version !== undefined && current.version !== expectedVersion) {
          throw new Error("Konkurens módosítás történt. Töltsd újra az adatokat.");
        }
        const next: ShiftTemplate = {
          ...current,
          active: false,
          version: (current.version ?? 1) + 1,
        };
        loc.templates![idx] = next;
        return next;
      }
    }
    throw new Error("A sablon nem található.");
  },
};

const coverageService: CoverageService = {
  async list(query) {
    await delay();
    let rows = [...store.coverageRules];
    if (query?.locationId) rows = rows.filter((r) => r.locationId === query.locationId);
    if (query?.capability) rows = rows.filter((r) => r.capability === query.capability);
    if (typeof query?.weekday === "number") {
      rows = rows.filter((r) => r.weekday === query.weekday);
    }
    if (!query?.includeInactive) rows = rows.filter((r) => r.active);
    return rows;
  },
  async create(rule) {
    await delay();
    const created: CoverageRule = { ...rule, id: `c-${uid()}`, active: rule.active ?? true };
    store.coverageRules.push(created);
    return created;
  },
  async update(id, rule, _expectedVersion) {
    await delay();
    const idx = store.coverageRules.findIndex((r) => r.id === id);
    if (idx < 0) throw new Error("Nem található lefedettségi szabály.");
    store.coverageRules[idx] = { ...rule, id };
    return store.coverageRules[idx];
  },
  async deactivate(id, _expectedVersion) {
    await delay();
    const idx = store.coverageRules.findIndex((r) => r.id === id);
    if (idx < 0) throw new Error("Nem található lefedettségi szabály.");
    store.coverageRules[idx] = { ...store.coverageRules[idx], active: false };
    return store.coverageRules[idx];
  },
  async save(rule) {
    await delay();
    const idx = store.coverageRules.findIndex((r) => r.id === rule.id);
    if (idx >= 0) store.coverageRules[idx] = rule;
    else store.coverageRules.push(rule);
    return rule;
  },
  async delete(id) {
    await delay();
    store.coverageRules = store.coverageRules.filter((r) => r.id !== id);
  },
};

const notificationService: NotificationService = {
  async listForUser(userId) {
    await delay();
    return store.notifications.filter((n) => n.targetUserId === userId);
  },
  async markRead(id) {
    await delay();
    const n = store.notifications.find((x) => x.id === id);
    if (n) n.read = true;
  },
};

const aiService: AiAssistantService = {
  async interpret(text) {
    await delay(600);
    const lower = text.toLowerCase();
    const items = [];
    if (lower.includes("szabad") || lower.includes("nyaral")) {
      items.push({
        id: uid(),
        kind: "leave_request" as const,
        summary: "Szabadságigény rögzítése",
        details: ["Típus: szabadság", "Időszak: következő hét hétfőtől szerdáig", "Egész napos"],
        warnings: ["Erre az időszakra már van 1 függő kérelem."],
      });
    }
    if (lower.includes("beteg")) {
      items.push({
        id: uid(),
        kind: "leave_request" as const,
        summary: "Betegállomány rögzítése",
        details: ["Típus: betegállomány", "Mai naptól 2 napig"],
        warnings: [],
      });
    }
    if (lower.includes("csere") || lower.includes("cserél")) {
      items.push({
        id: uid(),
        kind: "shift_swap" as const,
        summary: "Műszakcsere javaslat",
        details: ["Csütörtök 08–16 → péntek 12–20", "Csere: Szabó Eszter"],
        warnings: [
          "A lefedettségi szabály figyelmeztet: 1 gyógyszerész hiányzik 08:00–12:00 között.",
        ],
      });
    }
    if (items.length === 0) {
      items.push({
        id: uid(),
        kind: "leave_request" as const,
        summary: "Nem sikerült egyértelműen értelmezni",
        details: [
          "Kérlek pontosítsd a kérésed, például: „Szeretnék szabadságot jövő héten csütörtök-péntek.",
        ],
        warnings: [],
      });
    }
    return items;
  },
  async interpretCommand({ text }) {
    await delay(500);
    const t = text.toLowerCase();
    const isSick = t.includes("beteg");
    const isLeave = t.includes("szabad") || t.includes("nyaral");
    const isSwap = t.includes("csere") || t.includes("cserél");
    const previewId = uid();
    const expiresAt = new Date(Date.now() + 10 * 60_000).toISOString();
    if (!isSick && !isLeave && !isSwap) {
      return {
        previewId,
        summary: "Nem sikerült egyértelműen értelmezni.",
        transcript: text,
        resolvedActions: [],
        warnings: [],
        canExecute: false,
        clarifications: [
          {
            id: uid(),
            question:
              "Mit szeretnél tenni: szabadságot igényelni, betegállományt bejelenteni vagy műszakot cserélni?",
            answered: false,
          },
        ],
        expiresAt,
        confirmationToken: uid(),
      } satisfies AiCommandPreview;
    }
    const summary = isSwap
      ? "Műszakcsere javaslat"
      : isSick
        ? "Betegállomány bejelentés"
        : "Szabadságigény rögzítése";
    return {
      previewId,
      summary,
      transcript: text,
      resolvedActions: [
        {
          kind: isSwap ? "shift_swap" : "leave_request",
          summary,
          details: isSick
            ? ["Típus: betegállomány", "Mai naptól 2 napig"]
            : isSwap
              ? ["Csütörtök 08–16 → péntek 12–20"]
              : ["Típus: szabadság", "Következő hét hétfő–szerda", "Egész napos"],
        },
      ],
      clarifications: [],
      warnings: isSwap ? ["Lefedettségi figyelmeztetés a csere időszakban."] : [],
      canExecute: true,
      expiresAt,
      confirmationToken: uid(),
    } satisfies AiCommandPreview;
  },
  async answerClarification(previewId, _clarificationId, _answer) {
    await delay(200);
    return {
      previewId,
      summary: "Szabadságigény rögzítése (pontosítva)",
      transcript: "",
      resolvedActions: [
        {
          kind: "leave_request",
          summary: "Szabadság",
          details: ["A pontosítás alapján összeállítva."],
        },
      ],
      clarifications: [],
      warnings: [],
      canExecute: true,
      expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
      confirmationToken: uid(),
    } satisfies AiCommandPreview;
  },
  async executeCommand(_previewId, _token) {
    await delay(400);
    return { auditId: `audit-${uid()}` };
  },
};

function toSummary(u: User): AdminUserSummary {
  return { ...u, createdAt: new Date().toISOString(), version: 1 };
}

const userService: UserService = {
  async list(query) {
    await delay();
    const page = query?.page ?? 1;
    const pageSize = query?.pageSize ?? 20;
    const search = (query?.search ?? "").trim().toLowerCase();
    const includeInactive = query?.includeInactive ?? false;
    const filtered = store.users
      .filter((u) => (includeInactive ? true : u.active !== false))
      .filter(
        (u) =>
          !search ||
          u.email.toLowerCase().includes(search) ||
          u.displayName.toLowerCase().includes(search),
      )
      .map(toSummary);
    const start = (page - 1) * pageSize;
    return {
      items: filtered.slice(start, start + pageSize),
      total: filtered.length,
      page,
      pageSize,
    };
  },
  async create(input) {
    await delay();
    if (store.users.some((u) => u.email.toLowerCase() === input.email.toLowerCase())) {
      throw new Error("Már létezik felhasználó ezzel az email címmel.");
    }
    const linkedEmployee = input.linkedEmployeeId
      ? (() => {
          const e = store.employees.find((x) => x.id === input.linkedEmployeeId);
          if (!e) throw new Error("A megadott dolgozó nem található.");
          return {
            id: e.id,
            displayName: e.displayName,
            professionalRole: e.professionalRole,
            active: e.active,
            schedulable: e.schedulable,
          };
        })()
      : null;
    const newUser: User = {
      id: `u-${uid()}`,
      organizationId: "org-demo",
      email: input.email,
      displayName: input.displayName,
      active: true,
      permissions: input.permissions,
      linkedEmployee,
    };
    store.users.push(newUser);
    // initialPassword ignored in mock — real backend hashelné.
    return toSummary(newUser);
  },
  async updatePermissions(id, input) {
    await delay();
    const u = store.users.find((x) => x.id === id);
    if (!u) throw new Error("Nem található felhasználó.");
    // „Utolsó ManageUsers admin" védelem — 422-t szimulálunk.
    const wouldRemoveManage = !input.permissions.includes("ManageUsers");
    const remainingAdmins = store.users.filter(
      (x) => x.id !== id && x.active !== false && x.permissions.includes("ManageUsers"),
    ).length;
    if (wouldRemoveManage && u.permissions.includes("ManageUsers") && remainingAdmins === 0) {
      const err = new Error(
        "Nem törölhető az utolsó felhasználó-adminisztrátor jogosultsága.",
      ) as Error & { code?: string; status?: number };
      err.code = "LAST_ADMIN_REMOVAL";
      err.status = 422;
      throw err;
    }
    u.permissions = input.permissions;
    return toSummary(u);
  },
  async updateEmployeeLink(id, input) {
    await delay();
    const u = store.users.find((x) => x.id === id);
    if (!u) throw new Error("Nem található felhasználó.");
    if (input.linkedEmployeeId) {
      const e = store.employees.find((x) => x.id === input.linkedEmployeeId);
      if (!e) throw new Error("A dolgozó nem található.");
      u.linkedEmployee = {
        id: e.id,
        displayName: e.displayName,
        professionalRole: e.professionalRole,
        active: e.active,
        schedulable: e.schedulable,
      };
    } else {
      u.linkedEmployee = null;
    }
    return toSummary(u);
  },
  async setStatus(id, input) {
    await delay();
    const u = store.users.find((x) => x.id === id);
    if (!u) throw new Error("Nem található felhasználó.");
    if (!input.active && u.permissions.includes("ManageUsers")) {
      const remainingAdmins = store.users.filter(
        (x) => x.id !== id && x.active !== false && x.permissions.includes("ManageUsers"),
      ).length;
      if (remainingAdmins === 0) {
        const err = new Error(
          "Nem deaktiválható az utolsó aktív felhasználó-adminisztrátor.",
        ) as Error & { code?: string; status?: number };
        err.code = "LAST_ADMIN_REMOVAL";
        err.status = 422;
        throw err;
      }
    }
    u.active = input.active;
    return toSummary(u);
  },
};

function ctx() {
  return {
    employees: store.employees,
    locations: store.locations,
    leaves: store.leaveRequests,
    previousShifts: store.shifts,
  };
}

const scheduleWorkspaceService: ScheduleWorkspaceService = {
  async generate(input) {
    await delay(600);
    return mockGenerate(input, ctx());
  },
  async getCurrentRun() {
    await delay(30);
    return mockGetCurrentRun();
  },
  async regenerateScope(runId, scope) {
    await delay(400);
    return mockRegenerateScope(runId, scope, ctx());
  },
  async lockShift(runId, shiftId) {
    await delay(100);
    return mockLockShift(runId, shiftId, true);
  },
  async unlockShift(runId, shiftId) {
    await delay(100);
    return mockLockShift(runId, shiftId, false);
  },
  async rejectShift(runId, shiftId) {
    await delay(150);
    return mockRejectShift(runId, shiftId);
  },
  async findAlternatives(runId, shiftId) {
    await delay(100);
    return mockFindAlternatives(runId, shiftId);
  },
  async approve(runId) {
    await delay(200);
    return mockApprove(runId);
  },
  async publish(runId) {
    await delay(300);
    const run = mockPublish(runId);
    // Publikálás után a "published" műszakok bekerülnek a fő store-ba is.
    store.shifts = store.shifts.filter((s) => !s.runId || s.runId !== runId);
    store.shifts.push(...run.shifts);
    return run;
  },
};

export const mockServices: Services = {
  auth: authService,
  schedule: scheduleService,
  scheduleWorkspace: scheduleWorkspaceService,
  scheduleGeneration: {
    async preflight() {
      throw new Error("A beosztás-generálás csak API módban érhető el.");
    },
    async start() {
      throw new Error("A beosztás-generálás csak API módban érhető el.");
    },
    async get() {
      throw new Error("A beosztás-generálás csak API módban érhető el.");
    },
    async cancel() {
      throw new Error("A beosztás-generálás csak API módban érhető el.");
    },
  },
  adminSchedule: {
    async list() {
      return [];
    },
    async get() {
      throw new Error("A beosztás részletei csak API módban érhetők el.");
    },
    async getMatrix() {
      throw new Error("A beosztás mátrix csak API módban érhető el.");
    },
    async getCoverage() {
      throw new Error("A beosztás lefedettség csak API módban érhető el.");
    },
    async listIssues() {
      return [];
    },
    async listChanges() {
      return [];
    },
    async explainShift() {
      throw new Error("A magyarázat csak API módban érhető el.");
    },
    async findAlternatives() {
      return [];
    },
    async lockShift() {
      throw new Error("A műszak lakatolása csak API módban érhető el.");
    },
    async unlockShift() {
      throw new Error("A műszak feloldása csak API módban érhető el.");
    },
    async rejectShift() {
      throw new Error("A műszak elutasítás csak API módban érhető el.");
    },
    async replaceShift() {
      throw new Error("A műszak csere csak API módban érhető el.");
    },
    async regenerate() {
      throw new Error("Az újragenerálás csak API módban érhető el.");
    },
    async submitForReview() {
      throw new Error("A beosztás átadás csak API módban érhető el.");
    },
    async returnToDraft() {
      throw new Error("A beosztás visszaküldés csak API módban érhető el.");
    },
    async approve() {
      throw new Error("A jóváhagyás csak API módban érhető el.");
    },
    async publish() {
      throw new Error("A publikálás csak API módban érhető el.");
    },
    async archive() {
      throw new Error("Az archiválás csak API módban érhető el.");
    },
    async archiveEmptyDraft() {
      throw new Error("Az üres draft archiválása csak API módban érhető el.");
    },
    async cloneDraft() {
      throw new Error("A draft klónozás csak API módban érhető el.");
    },
  },
  leaveRequest: leaveRequestService,
  adminLeaveRequest: adminLeaveRequestService,
  workPreference: workPreferenceService,
  adminWorkPreference: adminWorkPreferenceService,
  employee: employeeService,
  location: locationService,
  coverage: coverageService,
  notification: notificationService,
  ai: aiService,
  user: userService,
  payroll: (() => {
    bindPayrollMockContext({
      currentUser: () => currentUserSync(),
      findEmployee: (id) => store.employees.find((e) => e.id === id) ?? null,
    });
    return makePayrollService((id) => store.employees.find((e) => e.id === id)?.displayName ?? id);
  })(),
};
