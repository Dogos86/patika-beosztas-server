import { createFileRoute, Link, redirect } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { services, dataSource } from "@/services";
import { ModuleUnavailableNotice } from "@/components/common/ModuleNotice";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import { LoadingState, EmptyState } from "@/components/common/states";
import {
  addDaysISO,
  eachDayISO,
  fmtDate,
  fmtDateShort,
  fmtWeekday,
  issueKindLabel,
  periodKindLabel,
  periodRange,
  scheduleRunStatusLabel,
  shiftPeriod,
  weekStartISO,
} from "@/lib/format";
import type {
  IssueSeverity,
  PeriodKind,
  ScheduleIssue,
  ScheduleRun,
  Shift,
  WorkspaceView,
} from "@/services/types";
import { timeTypeLabel } from "@/lib/format";
import { formatHm } from "@/lib/duration";
import {
  AlertOctagon,
  AlertTriangle,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Info,
  Lock,
  LockOpen,
  RefreshCw,
  Send,
  Sparkles,
  X,
} from "lucide-react";
import { toast } from "sonner";
import { useRequirePermission } from "@/components/common/PermissionGate";

export const Route = createFileRoute("/app/admin/scheduler")({
  head: () => ({ meta: [{ title: "Beosztás munkatér — Patika Beosztás" }] }),
  // API-módban egyetlen beosztási felület van: a valódi Phase 3A munkatér.
  beforeLoad: () => {
    if (dataSource === "api") throw redirect({ to: "/app/admin/schedules" });
  },
  component: WorkspacePage,
});

function WorkspacePage() {
  const denied = useRequirePermission(["ManageSchedules", "RunAutoFill"]);
  const qc = useQueryClient();
  const [period, setPeriod] = useState<PeriodKind>("week");
  const [anchor, setAnchor] = useState<string>(weekStartISO());
  const [view, setView] = useState<WorkspaceView>("employee");
  const [selectedShiftId, setSelectedShiftId] = useState<string | null>(null);
  const range = periodRange(anchor, period);
  // A legacy (mock) generátor-munkatér API-módban ki van váltva a Phase 3B
  // `/app/admin/schedules` felülettel — itt nem hívunk legacy service-t.
  const legacyDisabled = dataSource === "api";

  const employees = useQuery({
    queryKey: ["employees"],
    queryFn: () => services.employee.listAll(),
    enabled: !legacyDisabled,
  });
  const locations = useQuery({
    queryKey: ["locations-all"],
    queryFn: () => services.location.listAll(),
    enabled: !legacyDisabled,
  });
  const runQuery = useQuery({
    queryKey: ["scheduleWorkspace", "current"],
    queryFn: () => services.scheduleWorkspace.getCurrentRun(),
    enabled: !legacyDisabled,
  });
  const run: ScheduleRun | null = runQuery.data ?? null;

  const generate = useMutation({
    mutationFn: () =>
      services.scheduleWorkspace.generate({ from: range.from, to: range.to, keepLocked: !!run }),
    onSuccess: (r) => {
      qc.setQueryData(["scheduleWorkspace", "current"], r);
      toast.success(`Generálás kész: ${r.shifts.length} műszak, ${r.issues.length} probléma.`);
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Hiba a generáláskor"),
  });

  const regenerate = useMutation({
    mutationFn: (scope: Parameters<typeof services.scheduleWorkspace.regenerateScope>[1]) =>
      services.scheduleWorkspace.regenerateScope(run!.id, scope),
    onSuccess: (r) => {
      qc.setQueryData(["scheduleWorkspace", "current"], r);
      toast.success("Részleges újragenerálás kész.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Hiba"),
  });

  const toggleLock = useMutation({
    mutationFn: async (shift: Shift) => {
      return shift.locked
        ? services.scheduleWorkspace.unlockShift(run!.id, shift.id)
        : services.scheduleWorkspace.lockShift(run!.id, shift.id);
    },
    onSuccess: (r) => qc.setQueryData(["scheduleWorkspace", "current"], r),
  });

  const rejectShift = useMutation({
    mutationFn: (id: string) => services.scheduleWorkspace.rejectShift(run!.id, id),
    onSuccess: (r) => {
      qc.setQueryData(["scheduleWorkspace", "current"], r);
      setSelectedShiftId(null);
      toast.success("Javaslat elutasítva.");
    },
  });

  const approve = useMutation({
    mutationFn: () => services.scheduleWorkspace.approve(run!.id),
    onSuccess: (r) => {
      qc.setQueryData(["scheduleWorkspace", "current"], r);
      toast.success("Jóváhagyva.");
    },
  });
  const publish = useMutation({
    mutationFn: () => services.scheduleWorkspace.publish(run!.id),
    onSuccess: (r) => {
      qc.setQueryData(["scheduleWorkspace", "current"], r);
      toast.success("Közzétéve. A dolgozók már látják.");
    },
  });

  const empName = (id: string) => employees.data?.find((e) => e.id === id)?.displayName ?? id;
  const locName = (id: string) => locations.data?.find((l) => l.id === id)?.name ?? id;
  const locShort = (id: string) => {
    const n = locName(id);
    return n
      .split(" ")
      .map((w) => w[0])
      .join("")
      .slice(0, 3)
      .toUpperCase();
  };
  const selectedShift = run?.shifts.find((s) => s.id === selectedShiftId) ?? null;

  const rangeLabel =
    period === "month"
      ? new Date(range.from).toLocaleDateString("hu-HU", { year: "numeric", month: "long" })
      : `${fmtDate(range.from)} – ${fmtDate(range.to)}`;

  if (denied) return denied;
  if (legacyDisabled) {
    return (
      <div className="space-y-4">
        <PageHeader
          title="Beosztás munkatér"
          description="Ez a demó munkatér API-módban ki van váltva."
        />
        <ModuleUnavailableNotice title="A valódi beosztásgenerátor a Beosztások felületen érhető el">
          <p>
            API-módban a Phase 3A generátor és a review/publish folyamat a Beosztások oldalon
            működik.
          </p>
          <Button asChild size="sm" className="mt-3">
            <Link to="/app/admin/schedules">Tovább a Beosztások oldalra</Link>
          </Button>
        </ModuleUnavailableNotice>
      </div>
    );
  }
  return (
    <div>
      <PageHeader
        title="Beosztás munkatér"
        description={rangeLabel}
        action={
          <div className="flex items-center gap-2">
            <Badge
              variant="secondary"
              title="A backend beosztás-endpointok még nem elérhetők; a generátor előnézeti (mock) módban fut."
            >
              Előnézeti generátor
            </Badge>
            {run && <Badge variant="outline">{scheduleRunStatusLabel(run.status)}</Badge>}
          </div>
        }
      />

      {/* Vezérlő sáv */}
      <Card className="mb-4">
        <CardContent className="p-3 flex flex-wrap items-center gap-2">
          <div className="inline-flex rounded-md border overflow-hidden">
            {(["week", "biweek", "month"] as PeriodKind[]).map((p) => (
              <button
                key={p}
                onClick={() => setPeriod(p)}
                className={`px-3 py-1.5 text-sm ${period === p ? "bg-primary text-primary-foreground" : "hover:bg-muted"}`}
              >
                {periodKindLabel(p)}
              </button>
            ))}
          </div>
          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="icon"
              onClick={() => setAnchor(shiftPeriod(anchor, period, -1))}
              aria-label="Előző időszak"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              onClick={() => setAnchor(shiftPeriod(anchor, period, 1))}
              aria-label="Következő időszak"
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setAnchor(weekStartISO())}>
              Ma
            </Button>
          </div>
          <div className="ml-auto flex gap-2 flex-wrap">
            <Button size="sm" onClick={() => generate.mutate()} disabled={generate.isPending}>
              <Sparkles className="h-4 w-4 mr-1" />
              {generate.isPending ? "Generálás..." : run ? "Újragenerálás" : "Generálás"}
            </Button>
            {run && run.status === "Draft" && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => approve.mutate()}
                disabled={approve.isPending}
              >
                <CheckCircle2 className="h-4 w-4 mr-1" />
                Jóváhagyás
              </Button>
            )}
            {run && (run.status === "Approved" || run.status === "Draft") && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => publish.mutate()}
                disabled={publish.isPending}
              >
                <Send className="h-4 w-4 mr-1" />
                Közzététel
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {!run && (
        <EmptyState
          title="Még nincs generált beosztás erre az időszakra"
          description="Kattints a Generálás gombra: a rendszer figyelembe veszi a dolgozói kéréseket, preferenciákat, elérhetetlenségeket, jóváhagyott távolléteket és a lefedettségi szabályokat."
          action={
            <Button onClick={() => generate.mutate()} disabled={generate.isPending}>
              <Sparkles className="h-4 w-4 mr-1" />
              Generálás indítása
            </Button>
          }
        />
      )}

      {generate.isPending && <LoadingState label="Beosztás generálása..." />}

      {run && (
        <>
          <SummaryStrip run={run} />

          <Tabs value={view} onValueChange={(v) => setView(v as WorkspaceView)} className="mt-4">
            <TabsList>
              <TabsTrigger value="employee">Dolgozói beosztás</TabsTrigger>
              <TabsTrigger value="coverage">Telephelyi lefedettség</TabsTrigger>
              <TabsTrigger value="issues">Problémák ({run.issues.length})</TabsTrigger>
            </TabsList>

            <TabsContent value="employee" className="mt-4">
              <EmployeeMatrix
                run={run}
                employees={employees.data ?? []}
                onSelectShift={setSelectedShiftId}
                locShort={locShort}
              />
            </TabsContent>

            <TabsContent value="coverage" className="mt-4">
              <CoverageMatrix
                run={run}
                locName={locName}
                onFocusDay={(date, locationId) => {
                  setView("issues");
                  toast.info(
                    `${locName(locationId)} · ${fmtDate(date)} — részletek a Problémák panelen.`,
                  );
                }}
              />
            </TabsContent>

            <TabsContent value="issues" className="mt-4">
              <IssuesList
                run={run}
                empName={empName}
                locName={locName}
                onJump={(iss) => {
                  if (iss.shiftId) {
                    setView("employee");
                    setSelectedShiftId(iss.shiftId);
                  }
                }}
                onRegenerateIssue={(iss) => regenerate.mutate({ issueIds: [iss.id] })}
              />
            </TabsContent>
          </Tabs>

          <Card className="mt-4">
            <CardContent className="p-3 flex flex-wrap items-center gap-2 text-sm">
              <span className="text-muted-foreground mr-2">Részleges újragenerálás:</span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => regenerate.mutate({ weekStart: weekStartISO(new Date(anchor)) })}
              >
                <RefreshCw className="h-3 w-3 mr-1" />
                Teljes hét
              </Button>
              {(locations.data ?? [])
                .filter((l) => l.active)
                .map((l) => (
                  <Button
                    key={l.id}
                    variant="outline"
                    size="sm"
                    onClick={() => regenerate.mutate({ locationId: l.id })}
                  >
                    <RefreshCw className="h-3 w-3 mr-1" />
                    {l.name}
                  </Button>
                ))}
              <Button
                variant="outline"
                size="sm"
                onClick={() => regenerate.mutate({ professionalRole: "pharmacist" })}
              >
                <RefreshCw className="h-3 w-3 mr-1" />
                Gyógyszerészek
              </Button>
              <span className="ml-auto text-xs text-muted-foreground">
                Lakatolt műszakok minden újrageneráláskor megmaradnak.
              </span>
            </CardContent>
          </Card>
        </>
      )}

      <ShiftDetailSheet
        open={!!selectedShift}
        onClose={() => setSelectedShiftId(null)}
        shift={selectedShift}
        empName={empName}
        locName={locName}
        onToggleLock={() => selectedShift && toggleLock.mutate(selectedShift)}
        onReject={() => selectedShift && rejectShift.mutate(selectedShift.id)}
        onRegenerateDay={() => selectedShift && regenerate.mutate({ date: selectedShift.date })}
      />
    </div>
  );
}

// ─── Összefoglaló csík ─────────────────────────────────────────────

function SummaryStrip({ run }: { run: ScheduleRun }) {
  const s = run.summary;
  const items = [
    {
      label: "Lefedettség",
      value: `${s.coveragePct}%`,
      tone: s.coveragePct >= 95 ? "ok" : s.coveragePct >= 80 ? "warn" : "bad",
    },
    { label: "Blokkoló hibák", value: String(s.blocking), tone: s.blocking === 0 ? "ok" : "bad" },
    {
      label: "Figyelmeztetések",
      value: String(s.warnings),
      tone: s.warnings === 0 ? "ok" : "warn",
    },
    { label: "Kérések teljesülése", value: `${s.requestFulfillmentPct}%`, tone: "info" },
    {
      label: "Kerettől eltér",
      value: `${s.employeesOverTarget} dolgozó`,
      tone: s.employeesOverTarget === 0 ? "ok" : "warn",
    },
    { label: "Függő kérelem érint", value: String(s.pendingRequestOverlaps), tone: "info" },
    {
      label: "Több telephely-ütközés",
      value: String(s.multiLocationConflicts),
      tone: s.multiLocationConflicts === 0 ? "ok" : "bad",
    },
    {
      label: "Új / módosított / törölt",
      value: `${s.added} · ${s.modified} · ${s.removed}`,
      tone: "info",
    },
  ] as const;
  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
      {items.map((it) => (
        <div key={it.label} className={`rounded-md border p-3 ${toneBg(it.tone)}`}>
          <p className="text-[11px] uppercase text-muted-foreground tracking-wider">{it.label}</p>
          <p className="text-lg font-semibold">{it.value}</p>
        </div>
      ))}
    </div>
  );
}

function toneBg(t: "ok" | "warn" | "bad" | "info"): string {
  if (t === "ok") return "bg-emerald-50 border-emerald-200";
  if (t === "warn") return "bg-amber-50 border-amber-200";
  if (t === "bad") return "bg-rose-50 border-rose-200";
  return "bg-card";
}

// ─── Dolgozó × nap mátrix ──────────────────────────────────────────

function EmployeeMatrix({
  run,
  employees,
  onSelectShift,
  locShort,
}: {
  run: ScheduleRun;
  employees: {
    id: string;
    displayName: string;
    professionalRole: string;
    monthlyHoursTarget: number;
  }[];
  onSelectShift: (id: string) => void;
  locShort: (id: string) => string;
}) {
  const days = eachDayISO(run.from, run.to);
  const rows = useMemo(() => {
    const withShifts = employees.filter((e) => run.shifts.some((s) => s.employeeId === e.id));
    return withShifts;
  }, [employees, run.shifts]);

  return (
    <div className="rounded-md border bg-card overflow-x-auto">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr>
            <th className="sticky left-0 z-20 bg-muted/50 border-b border-r p-2 text-left min-w-40">
              Dolgozó
            </th>
            {days.map((d) => (
              <th key={d} className="border-b p-1 text-center min-w-24">
                <div className="text-[10px] uppercase text-muted-foreground">
                  {fmtWeekday(d).slice(0, 3)}
                </div>
                <div className="text-xs font-semibold">{fmtDateShort(d)}</div>
              </th>
            ))}
            <th className="border-b p-2 text-right min-w-32">Összesítés</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((emp) => {
            const empShifts = run.shifts.filter((s) => s.employeeId === emp.id);
            const totalMin = empShifts.reduce((a, s) => a + minutes(s.end) - minutes(s.start), 0);
            const warns = run.issues.filter((i) => i.employeeId === emp.id).length;
            return (
              <tr key={emp.id} className="border-b hover:bg-muted/30">
                <td className="sticky left-0 z-10 bg-card border-r p-2 font-medium">
                  <div className="truncate">{emp.displayName}</div>
                  <div className="text-[11px] text-muted-foreground truncate">
                    {emp.professionalRole}
                  </div>
                </td>
                {days.map((d) => {
                  const cell = empShifts.filter((s) => s.date === d);
                  return (
                    <td key={d} className="p-1 align-top">
                      <div className="flex flex-col gap-1">
                        {cell.map((s) => {
                          const hasIssue = run.issues.some((i) => i.shiftId === s.id);
                          return (
                            <button
                              key={s.id}
                              onClick={() => onSelectShift(s.id)}
                              className={`text-left rounded px-1.5 py-1 text-[11px] border ${
                                hasIssue
                                  ? "bg-amber-50 border-amber-300"
                                  : "bg-primary/10 border-primary/20"
                              } hover:bg-primary/20`}
                            >
                              <div className="flex items-center gap-1">
                                <span className="font-semibold">
                                  {s.start}–{s.end}
                                </span>
                                {s.locked && <Lock className="h-2.5 w-2.5" />}
                              </div>
                              <div className="text-[10px] text-muted-foreground">
                                {locShort(s.locationId)}
                              </div>
                            </button>
                          );
                        })}
                      </div>
                    </td>
                  );
                })}
                <td className="p-2 text-right text-xs text-muted-foreground align-top">
                  <div>
                    {Math.round(totalMin / 60)} ó / {emp.monthlyHoursTarget} ó
                  </div>
                  {warns > 0 && <div className="text-amber-700">{warns} jelzés</div>}
                </td>
              </tr>
            );
          })}
          {rows.length === 0 && (
            <tr>
              <td
                colSpan={days.length + 2}
                className="p-6 text-center text-sm text-muted-foreground"
              >
                Nincs beosztott dolgozó ebben az időszakban.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

// ─── Telephely × nap lefedettség ───────────────────────────────────

function CoverageMatrix({
  run,
  locName,
  onFocusDay,
}: {
  run: ScheduleRun;
  locName: (id: string) => string;
  onFocusDay: (date: string, locationId: string) => void;
}) {
  const days = eachDayISO(run.from, run.to);
  const locIds = run.locationIds;
  const cell = (date: string, locId: string) =>
    run.coverage.find((c) => c.date === date && c.locationId === locId);
  return (
    <div className="rounded-md border bg-card overflow-x-auto">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr>
            <th className="sticky left-0 z-20 bg-muted/50 border-b border-r p-2 text-left min-w-40">
              Telephely
            </th>
            {days.map((d) => (
              <th key={d} className="border-b p-1 text-center min-w-20">
                <div className="text-[10px] uppercase text-muted-foreground">
                  {fmtWeekday(d).slice(0, 3)}
                </div>
                <div className="text-xs font-semibold">{fmtDateShort(d)}</div>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {locIds.map((lid) => (
            <tr key={lid} className="border-b">
              <td className="sticky left-0 z-10 bg-card border-r p-2 font-medium">
                {locName(lid)}
              </td>
              {days.map((d) => {
                const c = cell(d, lid);
                if (!c)
                  return (
                    <td key={d} className="p-1">
                      <div className="rounded h-12 bg-muted/30" />
                    </td>
                  );
                const bg =
                  c.status === "ok"
                    ? "bg-emerald-100 border-emerald-300"
                    : c.status === "warning"
                      ? "bg-amber-100 border-amber-300"
                      : c.status === "blocking"
                        ? "bg-rose-100 border-rose-300"
                        : c.status === "inactive"
                          ? "bg-slate-100 border-slate-200"
                          : "bg-slate-50";
                return (
                  <td key={d} className="p-1">
                    <button
                      onClick={() => onFocusDay(d, lid)}
                      className={`w-full h-12 rounded border ${bg} text-xs px-1 hover:opacity-80`}
                    >
                      <div className="font-semibold">
                        {c.actual}/{c.required}
                      </div>
                      <div className="text-[10px]">
                        {c.details
                          .map(
                            (det) => `${det.role[0].toUpperCase()}:${det.actual}/${det.required}`,
                          )
                          .join(" ")}
                      </div>
                    </button>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
      <div className="p-3 flex flex-wrap gap-3 text-xs border-t">
        <LegendDot cls="bg-emerald-200 border-emerald-400">Megfelelő</LegendDot>
        <LegendDot cls="bg-amber-200 border-amber-400">Figyelmeztetés</LegendDot>
        <LegendDot cls="bg-rose-200 border-rose-400">Blokkoló</LegendDot>
        <LegendDot cls="bg-slate-200 border-slate-400">Inaktív</LegendDot>
      </div>
    </div>
  );
}

function LegendDot({ cls, children }: { cls: string; children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1">
      <span className={`inline-block h-3 w-3 rounded border ${cls}`} />
      {children}
    </span>
  );
}

// ─── Problémák lista ───────────────────────────────────────────────

function IssuesList({
  run,
  empName,
  locName,
  onJump,
  onRegenerateIssue,
}: {
  run: ScheduleRun;
  empName: (id: string) => string;
  locName: (id: string) => string;
  onJump: (iss: ScheduleIssue) => void;
  onRegenerateIssue: (iss: ScheduleIssue) => void;
}) {
  if (run.issues.length === 0) {
    return (
      <EmptyState
        title="Nincs jelenleg probléma"
        description="A generált beosztás minden ellenőrzést teljesített."
      />
    );
  }
  const sorted = [...run.issues].sort((a, b) => sevOrder(a.severity) - sevOrder(b.severity));
  return (
    <div className="space-y-2">
      {sorted.map((iss) => (
        <Card key={iss.id}>
          <CardContent className="p-3 flex items-start gap-3">
            <SeverityIcon sev={iss.severity} />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline" className={sevBadge(iss.severity)}>
                  {sevLabel(iss.severity)}
                </Badge>
                <span className="text-xs font-medium">{issueKindLabel(iss.kind)}</span>
              </div>
              <p className="text-sm mt-1">{iss.message}</p>
              <p className="text-xs text-muted-foreground mt-1">
                {iss.date && <>Nap: {fmtDate(iss.date)} · </>}
                {iss.locationId && <>{locName(iss.locationId)} · </>}
                {iss.employeeId && <>{empName(iss.employeeId)}</>}
              </p>
            </div>
            <div className="flex flex-col gap-1">
              {iss.shiftId && (
                <Button size="sm" variant="outline" onClick={() => onJump(iss)}>
                  Ugrás
                </Button>
              )}
              <Button size="sm" variant="ghost" onClick={() => onRegenerateIssue(iss)}>
                <RefreshCw className="h-3 w-3 mr-1" />
                Újragen.
              </Button>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function sevOrder(s: IssueSeverity): number {
  return s === "blocking" ? 0 : s === "warning" ? 1 : 2;
}
function sevLabel(s: IssueSeverity): string {
  return s === "blocking" ? "Blokkoló" : s === "warning" ? "Figyelmeztetés" : "Infó";
}
function sevBadge(s: IssueSeverity): string {
  if (s === "blocking") return "bg-rose-100 text-rose-800 border-rose-200";
  if (s === "warning") return "bg-amber-100 text-amber-800 border-amber-200";
  return "bg-sky-100 text-sky-800 border-sky-200";
}
function SeverityIcon({ sev }: { sev: IssueSeverity }) {
  if (sev === "blocking") return <AlertOctagon className="h-5 w-5 text-rose-600 shrink-0" />;
  if (sev === "warning") return <AlertTriangle className="h-5 w-5 text-amber-600 shrink-0" />;
  return <Info className="h-5 w-5 text-sky-600 shrink-0" />;
}

// ─── Műszak részletpanel („Miért ezt választotta?") ────────────────

function ShiftDetailSheet({
  open,
  onClose,
  shift,
  empName,
  locName,
  onToggleLock,
  onReject,
  onRegenerateDay,
}: {
  open: boolean;
  onClose: () => void;
  shift: Shift | null;
  empName: (id: string) => string;
  locName: (id: string) => string;
  onToggleLock: () => void;
  onReject: () => void;
  onRegenerateDay: () => void;
}) {
  return (
    <Sheet open={open} onOpenChange={(v) => !v && onClose()}>
      <SheetContent side="right" className="w-full sm:max-w-md overflow-y-auto">
        {shift && (
          <>
            <SheetHeader>
              <SheetTitle>{empName(shift.employeeId)}</SheetTitle>
              <SheetDescription>
                {fmtDate(shift.date)} · {shift.start}–{shift.end} · {locName(shift.locationId)}
              </SheetDescription>
            </SheetHeader>

            <div className="mt-4 space-y-4">
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" onClick={onToggleLock}>
                  {shift.locked ? (
                    <LockOpen className="h-4 w-4 mr-1" />
                  ) : (
                    <Lock className="h-4 w-4 mr-1" />
                  )}
                  {shift.locked ? "Feloldás" : "Rögzítés"}
                </Button>
                <Button size="sm" variant="outline" onClick={onRegenerateDay}>
                  <RefreshCw className="h-4 w-4 mr-1" />
                  Nap újragenerálása
                </Button>
                <Button size="sm" variant="destructive" onClick={onReject}>
                  <X className="h-4 w-4 mr-1" />
                  Javaslat elutasítása
                </Button>
              </div>

              <section>
                <h3 className="text-sm font-semibold mb-2">Miért ezt választotta?</h3>
                {shift.segments && shift.segments.length > 0 && (
                  <ul className="mb-3 space-y-1 text-xs">
                    {shift.segments.map((seg, i) => (
                      <li
                        key={i}
                        className="flex justify-between rounded border px-2 py-1 bg-muted/30"
                      >
                        <span className="font-medium">{timeTypeLabel(seg.type)}</span>
                        <span className="text-muted-foreground">
                          {formatHm(seg.startMin)}–{formatHm(seg.endMin)}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
                <ul className="space-y-1.5 text-sm">
                  {(shift.explanation?.reasons ?? []).map((r, i) => (
                    <li key={i} className="flex gap-2">
                      <CheckCircle2 className="h-4 w-4 text-emerald-600 shrink-0 mt-0.5" />
                      <span>{r}</span>
                    </li>
                  ))}
                </ul>
              </section>

              <section>
                <h3 className="text-sm font-semibold mb-2">Alternatív jelöltek</h3>
                {(shift.explanation?.alternatives ?? []).length === 0 && (
                  <p className="text-xs text-muted-foreground">Nincs elérhető alternatíva.</p>
                )}
                <ul className="space-y-2">
                  {(shift.explanation?.alternatives ?? []).map((alt) => (
                    <li key={alt.employeeId} className="rounded border p-2">
                      <p className="text-sm font-medium">{empName(alt.employeeId)}</p>
                      <ul className="text-xs text-muted-foreground mt-1 space-y-0.5">
                        {alt.tradeoffs.map((t, i) => (
                          <li key={i}>· {t}</li>
                        ))}
                      </ul>
                    </li>
                  ))}
                </ul>
              </section>
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}

function minutes(hm: string): number {
  const [h, m] = hm.split(":").map(Number);
  return h * 60 + m;
}
