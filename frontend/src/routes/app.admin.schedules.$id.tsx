import { createFileRoute, useSearch, Link, useNavigate } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { z } from "zod";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { LoadingState } from "@/components/common/states";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { fmtDate, capabilityLabel, timeTypeLabel } from "@/lib/format";
import { reasonLabel, issueLabel } from "@/lib/schedule-reason-labels";
import { isTerminalRunStatus, useScheduleRunPolling } from "@/hooks/use-schedule-run-polling";
import {
  isConcurrencyError,
  regenerateWithLatestScheduleVersion,
  refreshScheduleAfterGeneration,
  SCHEDULE_REFRESHED_MESSAGE,
} from "@/lib/schedule-generation-flow";
import type {
  ScheduleAlternative,
  ShiftAssignmentExplanation,
  ShiftAssignment,
  RegenerationScopeInput,
  RegenerationScopeType,
  StaffingCapability,
  TimeType,
  SchedulePlan,
} from "@/services/types";
import { ApiError } from "@/services/http/errors";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { useHasAnyPermission } from "@/hooks/use-auth";

const searchSchema = z.object({ run: z.string().optional() });

export const Route = createFileRoute("/app/admin/schedules/$id")({
  head: () => ({ meta: [{ title: "Beosztás — Patika Beosztás" }] }),
  validateSearch: (s) => searchSchema.parse(s),
  component: ScheduleWorkspacePage,
});

function ScheduleWorkspacePage() {
  const denied = useRequirePermission([
    "ManageSchedules",
    "RunAutoFill",
    "ApproveSchedules",
    "PublishSchedules",
  ]);
  const canManage = useHasAnyPermission(["ManageSchedules"]);
  const canRun = useHasAnyPermission(["RunAutoFill"]);
  const canApprove = useHasAnyPermission(["ApproveSchedules"]);
  const canPublish = useHasAnyPermission(["PublishSchedules"]);
  const { id } = Route.useParams();
  const search = useSearch({ from: "/app/admin/schedules/$id" });
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [selectedShift, setSelectedShift] = useState<ShiftAssignment | null>(null);
  const [regenOpen, setRegenOpen] = useState(false);
  const [concurrencyNotice, setConcurrencyNotice] = useState<string | null>(null);
  const refreshedRunId = useRef<string | null>(null);

  const runPoll = useScheduleRunPolling(search.run);
  const runIsTerminal = !!runPoll.data && isTerminalRunStatus(runPoll.data.status);

  const plan = useQuery({
    queryKey: ["schedule", id, "detail"],
    queryFn: () => services.adminSchedule.get(id),
    enabled: (!search.run || runIsTerminal) && !denied,
  });
  const matrix = useQuery({
    queryKey: ["schedule", id, "matrix"],
    queryFn: () => services.adminSchedule.getMatrix(id),
    enabled: !!plan.data,
  });
  const coverage = useQuery({
    queryKey: ["schedule", id, "coverage"],
    queryFn: () => services.adminSchedule.getCoverage(id),
    enabled: !!plan.data,
  });
  const issues = useQuery({
    queryKey: ["schedule", id, "issues"],
    queryFn: () => services.adminSchedule.listIssues(id),
    enabled: !!plan.data,
  });
  const changes = useQuery({
    queryKey: ["schedule", id, "changes"],
    queryFn: () => services.adminSchedule.listChanges(id),
    enabled: !!plan.data,
  });

  const refresh = async () => {
    await qc.invalidateQueries({ queryKey: ["schedule", id] });
  };

  useEffect(() => {
    const run = runPoll.data;
    if (run?.status !== "Succeeded" || refreshedRunId.current === run.id) return;
    refreshedRunId.current = run.id;
    void refreshScheduleAfterGeneration(qc, id);
  }, [id, qc, runPoll.data]);

  const submit = useMutation({
    mutationFn: (v: number) => services.adminSchedule.submitForReview(id, v),
    onSuccess: refresh,
  });
  const returnDraft = useMutation({
    mutationFn: (v: number) => services.adminSchedule.returnToDraft(id, v),
    onSuccess: refresh,
  });
  const approve = useMutation({
    mutationFn: (v: number) => services.adminSchedule.approve(id, v),
    onSuccess: refresh,
  });
  const publish = useMutation({
    mutationFn: (v: number) => services.adminSchedule.publish(id, v),
    onSuccess: refresh,
  });
  const archive = useMutation({
    mutationFn: (v: number) => services.adminSchedule.archive(id, v),
    onSuccess: refresh,
  });
  const archiveEmptyDraft = useMutation({
    mutationFn: (v: number) => services.adminSchedule.archiveEmptyDraft(id, v),
    onSuccess: refresh,
  });
  const cloneDraft = useMutation({
    mutationFn: (v: number) => services.adminSchedule.cloneDraft(id, v),
    onSuccess: (p: SchedulePlan) => {
      navigate({ to: "/app/admin/schedules/$id", params: { id: p.id } });
    },
  });
  const regenerate = useMutation({
    mutationFn: (scope: RegenerationScopeInput) =>
      regenerateWithLatestScheduleVersion(qc, services.adminSchedule, id, scope),
    onSuccess: (run) => {
      setConcurrencyNotice(null);
      setRegenOpen(false);
      navigate({ to: "/app/admin/schedules/$id", params: { id }, search: { run: run.id } });
    },
    onError: async (error) => {
      if (!isConcurrencyError(error)) return;
      setRegenOpen(false);
      await refreshScheduleAfterGeneration(qc, id);
      setConcurrencyNotice(SCHEDULE_REFRESHED_MESSAGE);
      regenerate.reset();
    },
  });
  const cancelRun = useMutation({
    mutationFn: () => services.scheduleGeneration.cancel(runPoll.data!.id, runPoll.data!.version),
    onSuccess: () => runPoll.refetch(),
  });

  if (search.run && (runPoll.isLoading || runPoll.isPolling)) {
    return (
      <div>
        <PageHeader title="Generálás fut…" description={`Futás ID: ${runPoll.data?.id}`} />
        <Card>
          <CardContent className="p-6 space-y-2">
            <p className="text-sm">Állapot: {runPoll.data?.status}</p>
            <p className="text-sm text-muted-foreground">
              Solver: {runPoll.data?.solverStatus} · algoritmus {runPoll.data?.algorithmVersion}
            </p>
            {runPoll.data?.statistics ? (
              <p className="text-sm text-muted-foreground">
                {runPoll.data.statistics.candidateOptionCount} jelölt ·{" "}
                {runPoll.data.statistics.variableCount} változó ·{" "}
                {runPoll.data.statistics.constraintCount} korlát
              </p>
            ) : (
              <p className="text-sm text-muted-foreground">Az optimalizáló még dolgozik.</p>
            )}
            {runPoll.error && (
              <p className="text-sm text-destructive">
                Hálózati hiba a futás lekérdezésekor — újrapróbálkozás folyamatban.
              </p>
            )}
            {cancelRun.error && (
              <p className="text-sm text-destructive">{errText(cancelRun.error)}</p>
            )}
            <Button
              variant="outline"
              size="sm"
              disabled={
                !canRun || cancelRun.isPending || !!runPoll.data?.cancellationRequestedAtUtc
              }
              onClick={() => cancelRun.mutate()}
            >
              {runPoll.data?.cancellationRequestedAtUtc ? "Megszakítás kérve…" : "Megszakítás"}
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }
  if (runPoll.data?.status === "Cancelled") {
    return (
      <div>
        <PageHeader title="Generálás megszakítva" />
        <Card>
          <CardContent className="p-6 space-y-3">
            <p className="text-sm text-muted-foreground">A futást megszakítottad.</p>
            <Button asChild variant="outline">
              <Link to="/app/admin/schedules">Vissza a beosztásokhoz</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }
  if (runPoll.data?.status === "Failed") {
    return (
      <div>
        <PageHeader title="Generálás sikertelen" />
        <Card>
          <CardContent className="p-6 space-y-2">
            <p className="text-sm text-destructive">
              A generálás technikai okból sikertelen. Ellenőrizd a beállításokat, majd próbáld újra.
            </p>
            <Button asChild variant="outline">
              <Link to="/app/admin/schedules">Vissza</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (denied) return denied;
  if (plan.isLoading || !plan.data) return <LoadingState />;

  const p = plan.data;
  const hasBlocking = p.summary.blockingIssueCount > 0;
  const isPublished = p.status === "Published";
  const workflowError =
    errText(submit.error) ??
    errText(returnDraft.error) ??
    errText(approve.error) ??
    errText(publish.error) ??
    errText(archive.error) ??
    errText(archiveEmptyDraft.error) ??
    errText(cloneDraft.error) ??
    errText(regenerate.error);

  return (
    <div>
      <PageHeader
        title={`Beosztás: ${fmtDate(p.periodStart)} – ${fmtDate(p.periodEnd)}`}
        description={`Állapot: ${p.status} · v${p.version} · ${p.shifts.length} műszak`}
        action={
          <div className="flex gap-2 flex-wrap">
            {p.status === "Draft" && canManage && (
              <Button size="sm" onClick={() => submit.mutate(p.version)}>
                Átadás review-ra
              </Button>
            )}
            {p.status === "UnderReview" && (canManage || canApprove) && (
              <>
                {canManage && (
                  <Button variant="outline" size="sm" onClick={() => returnDraft.mutate(p.version)}>
                    Vissza draft-ra
                  </Button>
                )}
                {canApprove && (
                  <Button
                    size="sm"
                    disabled={hasBlocking || approve.isPending}
                    title={hasBlocking ? "Blokkoló probléma miatt nem hagyható jóvá" : undefined}
                    onClick={() => approve.mutate(p.version)}
                  >
                    Jóváhagyás
                  </Button>
                )}
              </>
            )}
            {p.status === "Approved" && canPublish && (
              <Button
                size="sm"
                disabled={hasBlocking || publish.isPending}
                title={hasBlocking ? "Blokkoló probléma miatt nem publikálható" : undefined}
                onClick={() => publish.mutate(p.version)}
              >
                Publikálás
              </Button>
            )}
            {isPublished && canPublish && (
              <Button
                size="sm"
                variant="outline"
                disabled={archive.isPending}
                onClick={() => archive.mutate(p.version)}
              >
                Archiválás
              </Button>
            )}
            {p.status === "Draft" && p.shifts.length === 0 && canManage && (
              <Button
                size="sm"
                variant="outline"
                disabled={archiveEmptyDraft.isPending}
                onClick={() => archiveEmptyDraft.mutate(p.version)}
              >
                Üres draft archiválása
              </Button>
            )}
            {(isPublished || p.status === "Archived" || p.status === "Approved") && canManage && (
              <Button
                size="sm"
                variant="outline"
                disabled={cloneDraft.isPending}
                onClick={() => cloneDraft.mutate(p.version)}
              >
                Másolás draft-ba
              </Button>
            )}
            {(p.status === "Draft" || p.status === "UnderReview") && canRun && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => setRegenOpen(true)}
                disabled={regenerate.isPending || plan.isFetching}
              >
                Újragenerálás
              </Button>
            )}
          </div>
        }
      />

      {workflowError && <p className="mt-2 text-sm text-destructive">{workflowError}</p>}
      {concurrencyNotice && <p className="mt-2 text-sm text-amber-700">{concurrencyNotice}</p>}
      {runPoll.data?.status === "Succeeded" && (
        <Card className="mt-2 border-emerald-200 bg-emerald-50">
          <CardContent className="p-3 text-sm text-emerald-900">
            <p className="font-medium">Elkészült</p>
            {runPoll.data.statistics && (
              <p className="text-xs">
                {runPoll.data.statistics.candidateOptionCount} jelölt ·{" "}
                {runPoll.data.statistics.variableCount} változó ·{" "}
                {runPoll.data.statistics.constraintCount} korlát
              </p>
            )}
          </CardContent>
        </Card>
      )}
      {hasBlocking && (
        <p className="mt-2 text-sm text-destructive">
          {p.summary.blockingIssueCount} blokkoló probléma — jóváhagyás és publikálás tiltva.
        </p>
      )}
      {isPublished && (
        <p className="mt-2 text-sm text-muted-foreground">
          A publikált beosztás nem módosítható. Változtatáshoz készíts draft másolatot.
        </p>
      )}

      <SummaryBar
        summary={p.summary}
        hasCoverageRequirements={coverage.data?.hasConfiguredRequirements}
      />

      <Tabs defaultValue="employees" className="mt-4">
        <TabsList>
          <TabsTrigger value="employees">Dolgozók</TabsTrigger>
          <TabsTrigger value="coverage">Lefedettség</TabsTrigger>
          <TabsTrigger value="issues">Problémák ({issues.data?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="changes">Változások ({changes.data?.length ?? 0})</TabsTrigger>
        </TabsList>

        <TabsContent value="employees" className="mt-4">
          {matrix.isLoading && <LoadingState />}
          {matrix.data && (
            <div className="space-y-2">
              {matrix.data.employees.map((row) => (
                <Card key={row.employeeId}>
                  <CardContent className="p-3">
                    <div className="flex items-center justify-between mb-2">
                      <p className="font-semibold">{row.employeeDisplayName}</p>
                      <p className="text-xs text-muted-foreground">
                        {!row.hasWorkProfile || row.targetMinutes <= 0
                          ? "Hiányzó munkaidőprofil"
                          : `${Math.round(row.assignedMinutes / 60)}h / ${Math.round(
                              row.targetMinutes / 60,
                            )}h`}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-1">
                      {row.days.flatMap((d) =>
                        d.shifts.map((s) => (
                          <button
                            key={s.id}
                            onClick={() => !isPublished && setSelectedShift(s)}
                            disabled={isPublished}
                            className={`text-xs px-2 py-1 rounded border ${
                              s.isLocked ? "bg-primary/20" : "bg-primary/5"
                            } hover:bg-primary/30 disabled:opacity-60`}
                          >
                            {fmtDate(d.date)} {s.startTime}–{s.endTime}
                          </button>
                        )),
                      )}
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="coverage" className="mt-4">
          {coverage.isLoading && <LoadingState />}
          {coverage.data && !coverage.data.hasConfiguredRequirements && (
            <p className="text-sm text-amber-700">Nincs beállított lefedettségi követelmény.</p>
          )}
          {coverage.data && (
            <div className="space-y-1">
              {coverage.data.slots.map((slot, idx) => (
                <div
                  key={idx}
                  className="flex items-center justify-between rounded border p-2 text-sm"
                >
                  <div>
                    <p className="font-medium">
                      {fmtDate(slot.date)} · {slot.startTime}–{slot.endTime} · {slot.locationName}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {slot.requiredCapability} · szükséges {slot.requiredCount} · van{" "}
                      {slot.actualCount}
                    </p>
                  </div>
                  <Badge variant={slot.severity === "blocking" ? "destructive" : "outline"}>
                    {slot.status}
                  </Badge>
                </div>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="issues" className="mt-4">
          {issues.isLoading && <LoadingState />}
          {issues.data && (
            <div className="space-y-2">
              {issues.data.map((i) => (
                <Card key={i.id}>
                  <CardContent className="p-3 flex items-center justify-between">
                    <div>
                      <p className="font-medium">{issueLabel(i.code)}</p>
                      <p className="text-xs text-muted-foreground">
                        {i.date ? `${fmtDate(i.date)} · ` : ""}
                        {i.startTime && `${i.startTime}–${i.endTime}`}
                      </p>
                    </div>
                    <Badge variant={i.severity === "blocking" ? "destructive" : "outline"}>
                      {i.severity}
                    </Badge>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="changes" className="mt-4">
          {changes.isLoading && <LoadingState />}
          {changes.data && changes.data.length === 0 && (
            <p className="text-sm text-muted-foreground">Nincs eltérés az előző beosztáshoz.</p>
          )}
          <div className="space-y-1">
            {(changes.data ?? []).map((c, idx) => (
              <div
                key={`${c.shiftAssignmentId ?? c.basedOnShiftId ?? idx}`}
                className="flex items-center justify-between rounded border p-2 text-sm"
              >
                <div>
                  <p className="font-medium">
                    {fmtDate(c.date)} · {c.startTime}–{c.endTime}
                  </p>
                  <p className="text-xs text-muted-foreground">{c.employeeId}</p>
                </div>
                <Badge variant="outline">{changeKindLabel(c.changeKind)}</Badge>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>

      <ShiftDialog
        scheduleId={id}
        shift={selectedShift}
        scheduleVersion={p.version}
        onClose={() => setSelectedShift(null)}
        onChanged={refresh}
      />

      <RegenerateDialog
        open={regenOpen}
        onOpenChange={setRegenOpen}
        periodStart={p.periodStart}
        periodEnd={p.periodEnd}
        issues={(issues.data ?? []).map((i) => ({ id: i.id, code: i.code }))}
        pending={regenerate.isPending}
        error={errText(regenerate.error)}
        onSubmit={(scope) => regenerate.mutate(scope)}
      />
    </div>
  );
}

function errText(e: unknown): string | null {
  if (e instanceof ApiError) return e.message;
  return e ? "A művelet nem sikerült. Próbáld újra." : null;
}

function changeKindLabel(kind: string) {
  switch (kind) {
    case "new":
      return "Új";
    case "modified":
      return "Módosított";
    case "deleted":
      return "Törölt";
    default:
      return "Változatlan";
  }
}

const SCOPE_OPTIONS: { value: RegenerationScopeType; label: string }[] = [
  { value: "full", label: "Teljes időszak" },
  { value: "day", label: "Egy nap" },
  { value: "range", label: "Dátumtartomány" },
  { value: "week", label: "Hét" },
  { value: "location", label: "Telephely" },
  { value: "capability_time", label: "Kompetencia és időtípus" },
  { value: "issues", label: "Kijelölt problémák" },
];

const CAPABILITY_OPTIONS: StaffingCapability[] = [
  "pharmacist",
  "specialist_pharmacist",
  "senior_assistant",
  "assistant",
  "cleaner",
  "finance",
  "other",
];

const TIME_TYPE_OPTIONS: TimeType[] = ["work", "overtime", "on_call", "standby"];

function RegenerateDialog({
  open,
  onOpenChange,
  periodStart,
  periodEnd,
  issues,
  pending,
  error,
  onSubmit,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  periodStart: string;
  periodEnd: string;
  issues: { id: string; code: string }[];
  pending: boolean;
  error: string | null;
  onSubmit: (scope: RegenerationScopeInput) => void;
}) {
  const [type, setType] = useState<RegenerationScopeType>("full");
  const [dateFrom, setDateFrom] = useState(periodStart);
  const [dateTo, setDateTo] = useState(periodEnd);
  const [locationId, setLocationId] = useState("");
  const [capability, setCapability] = useState<StaffingCapability>("pharmacist");
  const [timeType, setTimeType] = useState<TimeType>("work");
  const [issueIds, setIssueIds] = useState<string[]>([]);

  const locations = useQuery({
    queryKey: ["locations-all"],
    queryFn: () => services.location.listAll(),
    enabled: open,
  });

  const build = (): RegenerationScopeInput => {
    switch (type) {
      case "day":
        return { type, dateFrom, dateTo: dateFrom };
      case "range":
      case "week":
        return { type, dateFrom, dateTo };
      case "location":
        return { type, locationId: locationId || undefined };
      case "capability_time":
        return { type, capability, timeType };
      case "issues":
        return { type, issueIds };
      default:
        return { type: "full" };
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Újragenerálás</DialogTitle>
        </DialogHeader>
        <div className="space-y-3">
          <div>
            <Label htmlFor="scopeType">Hatókör</Label>
            <select
              id="scopeType"
              className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
              value={type}
              onChange={(e) => setType(e.target.value as RegenerationScopeType)}
            >
              {SCOPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>

          {(type === "day" || type === "range" || type === "week") && (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="scopeFrom">{type === "day" ? "Nap" : "Kezdet"}</Label>
                <Input
                  id="scopeFrom"
                  type="date"
                  value={dateFrom}
                  onChange={(e) => setDateFrom(e.target.value)}
                />
              </div>
              {type !== "day" && (
                <div>
                  <Label htmlFor="scopeTo">Vég</Label>
                  <Input
                    id="scopeTo"
                    type="date"
                    value={dateTo}
                    onChange={(e) => setDateTo(e.target.value)}
                  />
                </div>
              )}
            </div>
          )}

          {type === "location" && (
            <div>
              <Label htmlFor="scopeLocation">Telephely</Label>
              <select
                id="scopeLocation"
                className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                value={locationId}
                onChange={(e) => setLocationId(e.target.value)}
              >
                <option value="">Válassz…</option>
                {(locations.data ?? []).map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name}
                  </option>
                ))}
              </select>
            </div>
          )}

          {type === "capability_time" && (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="scopeCap">Kompetencia</Label>
                <select
                  id="scopeCap"
                  className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                  value={capability}
                  onChange={(e) => setCapability(e.target.value as StaffingCapability)}
                >
                  {CAPABILITY_OPTIONS.map((c) => (
                    <option key={c} value={c}>
                      {capabilityLabel(c)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label htmlFor="scopeTime">Időtípus</Label>
                <select
                  id="scopeTime"
                  className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                  value={timeType}
                  onChange={(e) => setTimeType(e.target.value as TimeType)}
                >
                  {TIME_TYPE_OPTIONS.map((t) => (
                    <option key={t} value={t}>
                      {timeTypeLabel(t)}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {type === "issues" && (
            <div className="max-h-48 space-y-1 overflow-auto rounded border p-2">
              {issues.length === 0 && (
                <p className="text-sm text-muted-foreground">Nincs kiválasztható probléma.</p>
              )}
              {issues.map((i) => (
                <label key={i.id} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={issueIds.includes(i.id)}
                    onChange={(e) =>
                      setIssueIds((prev) =>
                        e.target.checked ? [...prev, i.id] : prev.filter((x) => x !== i.id),
                      )
                    }
                  />
                  {issueLabel(i.code)}
                </label>
              ))}
            </div>
          )}

          {error && <p className="text-sm text-destructive">{error}</p>}
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Mégse
          </Button>
          <Button type="button" disabled={pending} onClick={() => onSubmit(build())}>
            {pending ? "Indítás…" : "Újragenerálás indítása"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function SummaryBar({
  summary,
  hasCoverageRequirements,
}: {
  summary: import("@/services/types").ScheduleGenerationSummary;
  hasCoverageRequirements: boolean | undefined;
}) {
  const cells: { label: string; value: string | number }[] = [
    {
      label: "Lefedettség",
      value:
        hasCoverageRequirements === false
          ? "Nincs beállítva"
          : hasCoverageRequirements === undefined
            ? "Ellenőrzés…"
            : `${summary.blockingCoveragePercent}%`,
    },
    { label: "Blokkoló", value: summary.blockingIssueCount },
    { label: "Figyelmeztető", value: summary.warningIssueCount },
    { label: "Preferencia", value: `${summary.preferenceFulfillmentPercent}%` },
    { label: "Új", value: summary.newShiftCount },
    { label: "Módosított", value: summary.modifiedShiftCount },
    { label: "Törölt", value: summary.deletedShiftCount },
  ];
  return (
    <div className="grid grid-cols-2 md:grid-cols-7 gap-2 mt-2">
      {cells.map((c) => (
        <div key={c.label} className="rounded border bg-card p-2 text-center">
          <p className="text-xs text-muted-foreground">{c.label}</p>
          <p className="text-sm font-semibold">{c.value}</p>
        </div>
      ))}
    </div>
  );
}

function ShiftDialog({
  scheduleId,
  shift,
  scheduleVersion,
  onClose,
  onChanged,
}: {
  scheduleId: string;
  shift: ShiftAssignment | null;
  scheduleVersion: number;
  onClose: () => void;
  onChanged: () => Promise<void>;
}) {
  const qc = useQueryClient();
  const explain = useQuery({
    enabled: !!shift,
    queryKey: ["shiftExplain", scheduleId, shift?.id],
    queryFn: () =>
      services.adminSchedule.explainShift(
        scheduleId,
        shift!.id,
      ) as Promise<ShiftAssignmentExplanation>,
  });
  const alts = useQuery({
    enabled: !!shift,
    queryKey: ["shiftAlts", scheduleId, shift?.id],
    queryFn: () => services.adminSchedule.findAlternatives(scheduleId, shift!.id),
  });

  const lock = useMutation({
    mutationFn: () =>
      services.adminSchedule.lockShift(scheduleId, shift!.id, {
        expectedShiftVersion: shift!.version,
        expectedScheduleVersion: scheduleVersion,
      }),
    onSuccess: async () => {
      await onChanged();
      onClose();
    },
  });
  const unlock = useMutation({
    mutationFn: () =>
      services.adminSchedule.unlockShift(scheduleId, shift!.id, {
        expectedShiftVersion: shift!.version,
        expectedScheduleVersion: scheduleVersion,
      }),
    onSuccess: async () => {
      await onChanged();
      onClose();
    },
  });
  const reject = useMutation({
    mutationFn: (reason: string) =>
      services.adminSchedule.rejectShift(scheduleId, shift!.id, {
        expectedShiftVersion: shift!.version,
        expectedScheduleVersion: scheduleVersion,
        reason,
      }),
    onSuccess: async () => {
      await onChanged();
      onClose();
    },
  });
  const replace = useMutation({
    mutationFn: (alt: ScheduleAlternative) =>
      services.adminSchedule.replaceShift(scheduleId, shift!.id, {
        replacementEmployeeId: alt.employeeId,
        expectedShiftVersion: shift!.version,
        expectedScheduleVersion: scheduleVersion,
        reason: "Admin csere",
      }),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ["schedule", scheduleId] });
      await onChanged();
      onClose();
    },
  });

  const errorMsg = (e: unknown) =>
    e instanceof ApiError ? e.message : e instanceof Error ? e.message : null;
  const mutationErr =
    errorMsg(lock.error) ??
    errorMsg(unlock.error) ??
    errorMsg(reject.error) ??
    errorMsg(replace.error);

  return (
    <Dialog open={!!shift} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {shift?.employeeDisplayName} — {shift ? fmtDate(shift.date) : ""} {shift?.startTime}–
            {shift?.endTime}
          </DialogTitle>
        </DialogHeader>

        {explain.data && (
          <div className="text-sm">
            <p className="font-semibold mb-1">Miért ez?</p>
            <ul className="list-disc pl-5 space-y-0.5">
              {explain.data.reasonCodes.map((c) => (
                <li key={c}>{reasonLabel(c)}</li>
              ))}
            </ul>
          </div>
        )}

        {alts.data && alts.data.length > 0 && (
          <div className="text-sm">
            <p className="font-semibold mb-1">Alternatívák</p>
            <div className="space-y-1">
              {alts.data.map((a) => (
                <div
                  key={a.employeeId}
                  className="flex items-center justify-between rounded border p-2"
                >
                  <div>
                    <p className="font-medium">{a.employeeDisplayName}</p>
                    <p className="text-xs text-muted-foreground">
                      Δ {a.scoreDifference} · {a.tradeoffCodes.map(reasonLabel).join(", ")}
                    </p>
                  </div>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => replace.mutate(a)}
                    disabled={replace.isPending}
                  >
                    Csere
                  </Button>
                </div>
              ))}
            </div>
          </div>
        )}

        {mutationErr && <p className="text-sm text-destructive">{mutationErr}</p>}

        <DialogFooter className="flex-wrap gap-2">
          {shift?.isLocked ? (
            <Button variant="outline" size="sm" onClick={() => unlock.mutate()}>
              Feloldás
            </Button>
          ) : (
            <Button variant="outline" size="sm" onClick={() => lock.mutate()}>
              Lakatolás
            </Button>
          )}
          <Button
            variant="destructive"
            size="sm"
            onClick={() => reject.mutate("Admin elutasította")}
          >
            Elutasítás
          </Button>
          <Button size="sm" onClick={onClose}>
            Bezárás
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
