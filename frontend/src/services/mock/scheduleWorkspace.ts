// Mock beosztás-generáló munkatér. Egy egyszerű, determinisztikus algoritmus
// építi fel a műszakokat és a problémákat, hogy a UI végig kattinthatóan
// demonstrálja a spec 3–10. pontjait. A valódi motor a backendben készül.

import type {
  CoverageCell,
  Employee,
  GenerateRunInput,
  IssueSeverity,
  LeaveRequest,
  Location,
  ProfessionalRole,
  RegenerateScope,
  ScheduleIssue,
  ScheduleRun,
  ScheduleRunSummary,
  Shift,
  ShiftAlternative,
  ShiftExplanation,
} from "../types";
import { addDaysISO, eachDayISO, professionalRoleLabel } from "@/lib/format";

const uid = () => Math.random().toString(36).slice(2, 10);

interface GeneratorCtx {
  employees: Employee[];
  locations: Location[];
  leaves: LeaveRequest[];
  previousShifts: Shift[];
}

const REQUIRED_ROLES: ProfessionalRole[] = ["pharmacist", "assistant"];

function empDaily(e: Employee, date: string, leaves: LeaveRequest[]): "leave" | "ok" | "pending" {
  const l = leaves.find((r) => r.employeeId === e.id && r.startDate <= date && r.endDate >= date);
  if (!l) return "ok";
  if (l.status === "approved" || l.status === "reported") return "leave";
  return "pending";
}

function pickWindow(role: ProfessionalRole, slot: "am" | "pm"): { start: string; end: string } {
  if (role === "pharmacy_manager")
    return slot === "am" ? { start: "08:00", end: "14:00" } : { start: "14:00", end: "20:00" };
  if (role === "pharmacist")
    return slot === "am" ? { start: "08:00", end: "14:00" } : { start: "14:00", end: "20:00" };
  return slot === "am" ? { start: "08:00", end: "14:00" } : { start: "12:00", end: "18:00" };
}

function generateInternal(
  input: GenerateRunInput,
  ctx: GeneratorCtx,
  keepShifts: Shift[] = [],
): ScheduleRun {
  const runId = `run-${uid()}`;
  const days = eachDayISO(input.from, input.to);
  const activeLocations = ctx.locations.filter(
    (l) =>
      l.active &&
      (!input.locationIds || input.locationIds.length === 0 || input.locationIds.includes(l.id)),
  );

  const shifts: Shift[] = keepShifts.map((s) => ({ ...s, runId }));
  const issues: ScheduleIssue[] = [];

  // Round-robin index per role/location, hogy változatos legyen.
  const rr = new Map<string, number>();
  const bump = (key: string) => {
    const n = (rr.get(key) ?? 0) + 1;
    rr.set(key, n);
    return n;
  };

  const workedMinutes = new Map<string, number>(); // employeeId → sum
  const dayEmpLocation = new Map<string, string>(); // `${date}|${employeeId}` → locationId

  for (const date of days) {
    for (const loc of activeLocations) {
      for (const role of REQUIRED_ROLES) {
        for (const slot of ["am", "pm"] as const) {
          // Van-e már lakatolt megfelelő?
          const already = shifts.find(
            (s) =>
              s.date === date &&
              s.locationId === loc.id &&
              matchesRole(ctx.employees, s.employeeId, role) &&
              s.start === pickWindow(role, slot).start,
          );
          if (already) continue;
          const candidates = ctx.employees
            .filter(
              (e) =>
                e.active && e.schedulable && e.includeInAutoFill && e.locationIds.includes(loc.id),
            )
            .filter((e) => roleMatches(e.professionalRole, role))
            .filter((e) => empDaily(e, date, ctx.leaves) !== "leave")
            .filter(
              (e) =>
                !dayEmpLocation.has(`${date}|${e.id}`) ||
                dayEmpLocation.get(`${date}|${e.id}`) === loc.id,
            );
          if (candidates.length === 0) {
            issues.push({
              id: uid(),
              kind: role === "pharmacist" ? "missing_pharmacist" : "missing_assistant",
              severity: role === "pharmacist" ? "blocking" : "warning",
              message: `${loc.name}: nincs elérhető ${professionalRoleLabel(role).toLowerCase()} ${slot === "am" ? "délelőtt" : "délután"}.`,
              date,
              locationId: loc.id,
              professionalRole: role,
            });
            continue;
          }
          const idx = bump(`${loc.id}|${role}|${slot}`) % candidates.length;
          const emp = candidates[idx];
          const win = pickWindow(role, slot);
          const pending = empDaily(emp, date, ctx.leaves) === "pending";
          const explanation: ShiftExplanation = {
            reasons: [
              `Megfelelő szakmai szerepkör: ${professionalRoleLabel(emp.professionalRole)}.`,
              `A(z) ${loc.name} telephelyhez rendelhető.`,
              "Nincs jóváhagyott távolléte az adott napon.",
              "Nem lép ki a napi keretből.",
              `Lefedettségi hiányt old meg: ${professionalRoleLabel(role)} / ${slot === "am" ? "délelőtt" : "délután"}.`,
            ],
            alternatives: candidates
              .filter((c) => c.id !== emp.id)
              .slice(0, 2)
              .map((c) => ({
                employeeId: c.id,
                tradeoffs: [
                  `${c.displayName}: nagyobb heti terhelés lenne.`,
                  emp.countsAsPharmacist === c.countsAsPharmacist
                    ? "Hasonló szakmai illeszkedés."
                    : "Kevésbé kedvező szerepkör-fedettség.",
                ],
              })),
          };
          const shift: Shift = {
            id: `sh-${uid()}`,
            employeeId: emp.id,
            locationId: loc.id,
            date,
            start: win.start,
            end: win.end,
            type: "work",
            status: "draft",
            runId,
            explanation,
            segments: [
              {
                type: "work",
                startMin: toMinutes(win.start),
                endMin: toMinutes(win.end),
              },
            ],
          };
          shifts.push(shift);
          dayEmpLocation.set(`${date}|${emp.id}`, loc.id);
          const min = toMinutes(win.end) - toMinutes(win.start);
          workedMinutes.set(emp.id, (workedMinutes.get(emp.id) ?? 0) + min);

          if (pending) {
            issues.push({
              id: uid(),
              kind: "pending_request_overlap",
              severity: "warning",
              message: `${emp.displayName} függő kérelme érinti a ${date} műszakot.`,
              date,
              locationId: loc.id,
              employeeId: emp.id,
              shiftId: shift.id,
            });
          }
          // Blocked window violation ellenőrzés
          const wd = weekdayCode(date);
          const blocked = emp.blockedWindows.find(
            (w) => (w.weekday === "every" || w.weekday === wd) && overlaps(win, w),
          );
          if (blocked) {
            issues.push({
              id: uid(),
              kind: "blocked_window_violation",
              severity: "warning",
              message: `${emp.displayName} határozott elérhetetlensége ütközik (${blocked.start}–${blocked.end}).`,
              date,
              employeeId: emp.id,
              shiftId: shift.id,
            });
          }
          // Preference miss
          const preferred = emp.preferredWindows.find(
            (w) => w.weekday === "every" || w.weekday === wd,
          );
          if (preferred && !overlaps(win, preferred)) {
            issues.push({
              id: uid(),
              kind: "preference_missed",
              severity: "info",
              message: `${emp.displayName} preferált időszaka (${preferred.start}–${preferred.end}) nem teljesült.`,
              date,
              employeeId: emp.id,
              shiftId: shift.id,
            });
          }
        }
      }
    }
  }

  // Monthly cap ellenőrzés
  for (const [empId, mins] of workedMinutes) {
    const emp = ctx.employees.find((e) => e.id === empId);
    if (!emp) continue;
    const target = emp.monthlyHoursTarget * 60;
    if (mins > target * 1.05) {
      issues.push({
        id: uid(),
        kind: "monthly_cap_exceeded",
        severity: "warning",
        message: `${emp.displayName} havi kerete túllépve: ${Math.round(mins / 60)} ó / ${emp.monthlyHoursTarget} ó.`,
        employeeId: emp.id,
      });
    }
  }

  const coverage = buildCoverage(days, activeLocations, shifts, ctx.employees);
  const previousShifts = ctx.previousShifts.filter(
    (s) => s.date >= input.from && s.date <= input.to,
  );
  const summary = buildSummary(
    shifts,
    issues,
    coverage,
    ctx.employees,
    workedMinutes,
    previousShifts,
  );

  return {
    id: runId,
    from: input.from,
    to: input.to,
    status: "Draft",
    generatedAt: new Date().toISOString(),
    locationIds: activeLocations.map((l) => l.id),
    summary,
    shifts,
    issues,
    coverage,
    previousShifts,
  };
}

function roleMatches(actual: ProfessionalRole, need: ProfessionalRole): boolean {
  if (need === "pharmacist") return actual === "pharmacist" || actual === "pharmacy_manager";
  if (need === "assistant") return actual === "assistant" || actual === "specialist_assistant";
  return actual === need;
}

function matchesRole(employees: Employee[], id: string, role: ProfessionalRole): boolean {
  const e = employees.find((x) => x.id === id);
  return !!e && roleMatches(e.professionalRole, role);
}

function weekdayCode(iso: string): "mon" | "tue" | "wed" | "thu" | "fri" | "sat" | "sun" {
  const d = new Date(iso);
  const codes: ("sun" | "mon" | "tue" | "wed" | "thu" | "fri" | "sat")[] = [
    "sun",
    "mon",
    "tue",
    "wed",
    "thu",
    "fri",
    "sat",
  ];
  return codes[d.getDay()] as "mon" | "tue" | "wed" | "thu" | "fri" | "sat" | "sun";
}

function toMinutes(hm: string): number {
  const [h, m] = hm.split(":").map(Number);
  return h * 60 + m;
}

function overlaps(a: { start: string; end: string }, b: { start: string; end: string }): boolean {
  return toMinutes(a.start) < toMinutes(b.end) && toMinutes(b.start) < toMinutes(a.end);
}

function buildCoverage(
  days: string[],
  locations: Location[],
  shifts: Shift[],
  employees: Employee[],
): CoverageCell[] {
  const cells: CoverageCell[] = [];
  for (const date of days) {
    for (const loc of locations) {
      const daily = shifts.filter((s) => s.date === date && s.locationId === loc.id);
      const details = REQUIRED_ROLES.map((role) => {
        const actual = daily.filter((s) => matchesRole(employees, s.employeeId, role)).length;
        const required = 2; // délelőtt + délután
        return { role, required, actual };
      });
      const totalReq = details.reduce((a, b) => a + b.required, 0);
      const totalAct = details.reduce((a, b) => a + b.actual, 0);
      let status: CoverageCell["status"] = "ok";
      if (!loc.active) status = "inactive";
      else if (details.some((d) => d.actual < d.required && d.role === "pharmacist"))
        status = "blocking";
      else if (details.some((d) => d.actual < d.required)) status = "warning";
      cells.push({
        date,
        locationId: loc.id,
        status,
        required: totalReq,
        actual: totalAct,
        details,
      });
    }
  }
  return cells;
}

function buildSummary(
  shifts: Shift[],
  issues: ScheduleIssue[],
  coverage: CoverageCell[],
  employees: Employee[],
  workedMinutes: Map<string, number>,
  previousShifts: Shift[],
): ScheduleRunSummary {
  const totalReq = coverage.reduce((a, c) => a + c.required, 0) || 1;
  const totalAct = coverage.reduce((a, c) => a + Math.min(c.actual, c.required), 0);
  const coveragePct = Math.round((totalAct / totalReq) * 100);
  const bySev = (s: IssueSeverity) => issues.filter((i) => i.severity === s).length;
  const employeesOverTarget = employees.filter((e) => {
    const m = workedMinutes.get(e.id) ?? 0;
    const target = e.monthlyHoursTarget * 60;
    return Math.abs(m - target) > target * 0.1;
  }).length;
  const prevIds = new Set(previousShifts.map((s) => s.id));
  const curIds = new Set(shifts.map((s) => s.id));
  const added = shifts.filter((s) => !prevIds.has(s.id)).length;
  const removed = previousShifts.filter((s) => !curIds.has(s.id)).length;
  return {
    coveragePct,
    blocking: bySev("blocking"),
    warnings: bySev("warning"),
    requestFulfillmentPct: 78, // demó — igazi számítás backendben
    employeesOverTarget,
    pendingRequestOverlaps: issues.filter((i) => i.kind === "pending_request_overlap").length,
    multiLocationConflicts: issues.filter((i) => i.kind === "multi_location_conflict").length,
    added,
    modified: 0,
    removed,
  };
}

// ─── Élő state ─────────────────────────────────────────────────────

let currentRun: ScheduleRun | null = null;

export function mockGenerate(input: GenerateRunInput, ctx: GeneratorCtx): ScheduleRun {
  const keep = input.keepLocked && currentRun ? currentRun.shifts.filter((s) => s.locked) : [];
  currentRun = generateInternal(input, ctx, keep);
  return currentRun;
}

export function mockGetCurrentRun(): ScheduleRun | null {
  return currentRun;
}

export function mockRegenerateScope(
  runId: string,
  scope: RegenerateScope,
  ctx: GeneratorCtx,
): ScheduleRun {
  if (!currentRun || currentRun.id !== runId) throw new Error("A futás lejárt vagy nem található.");
  const keep: Shift[] = currentRun.shifts.filter((s) => {
    if (s.locked) return true;
    if (scope.date) return s.date !== scope.date;
    if (scope.weekStart) {
      const end = addDaysISO(scope.weekStart, 6);
      return !(s.date >= scope.weekStart && s.date <= end);
    }
    if (scope.locationId) return s.locationId !== scope.locationId;
    if (scope.professionalRole) {
      const emp = ctx.employees.find((e) => e.id === s.employeeId);
      return !emp || !roleMatches(emp.professionalRole, scope.professionalRole);
    }
    if (scope.issueIds && scope.issueIds.length > 0) {
      const affectedShiftIds = new Set(
        currentRun!.issues
          .filter((i) => scope.issueIds!.includes(i.id) && i.shiftId)
          .map((i) => i.shiftId!),
      );
      return !affectedShiftIds.has(s.id);
    }
    return true;
  });
  currentRun = generateInternal({ from: currentRun.from, to: currentRun.to }, ctx, keep);
  return currentRun;
}

export function mockLockShift(runId: string, shiftId: string, locked: boolean): ScheduleRun {
  if (!currentRun || currentRun.id !== runId) throw new Error("A futás lejárt.");
  currentRun = {
    ...currentRun,
    shifts: currentRun.shifts.map((s) => (s.id === shiftId ? { ...s, locked } : s)),
  };
  return currentRun;
}

export function mockRejectShift(runId: string, shiftId: string): ScheduleRun {
  if (!currentRun || currentRun.id !== runId) throw new Error("A futás lejárt.");
  currentRun = {
    ...currentRun,
    shifts: currentRun.shifts.filter((s) => s.id !== shiftId),
    issues: currentRun.issues.filter((i) => i.shiftId !== shiftId),
  };
  return currentRun;
}

export function mockFindAlternatives(runId: string, shiftId: string): ShiftAlternative[] {
  if (!currentRun || currentRun.id !== runId) return [];
  const s = currentRun.shifts.find((x) => x.id === shiftId);
  return s?.explanation?.alternatives ?? [];
}

export function mockApprove(runId: string): ScheduleRun {
  if (!currentRun || currentRun.id !== runId) throw new Error("A futás lejárt.");
  currentRun = { ...currentRun, status: "Approved" };
  return currentRun;
}

export function mockPublish(runId: string): ScheduleRun {
  if (!currentRun || currentRun.id !== runId) throw new Error("A futás lejárt.");
  currentRun = {
    ...currentRun,
    status: "Published",
    shifts: currentRun.shifts.map((s) => ({ ...s, status: "published" as const })),
  };
  return currentRun;
}
