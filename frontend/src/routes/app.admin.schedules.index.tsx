import { createFileRoute, Link } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { z } from "zod";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { services, dataSource } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { LoadingState, EmptyState } from "@/components/common/states";
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
import { fmtDate, weekStartISO, addDaysISO } from "@/lib/format";
import { ApiError } from "@/services/http/errors";
import { useRequirePermission } from "@/components/common/PermissionGate";

export const Route = createFileRoute("/app/admin/schedules/")({
  head: () => ({ meta: [{ title: "Beosztások — Patika Beosztás" }] }),
  component: SchedulesListPage,
});

const schema = z.object({
  periodStart: z.string().min(1, "Kötelező"),
  periodEnd: z.string().min(1, "Kötelező"),
  deterministicSeed: z.string().optional(),
  maxSolveSeconds: z.string().optional(),
});
type FormValues = z.infer<typeof schema>;

function SchedulesListPage() {
  const denied = useRequirePermission(["ManageSchedules", "RunAutoFill"]);
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);

  const list = useQuery({
    queryKey: ["schedules"],
    queryFn: () => services.adminSchedule.list(),
    enabled: !denied,
  });

  const defStart = weekStartISO();
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      periodStart: defStart,
      periodEnd: addDaysISO(defStart, 6),
      deterministicSeed: "",
      maxSolveSeconds: "",
    },
  });
  const periodStart = useWatch({ control: form.control, name: "periodStart" });
  const periodEnd = useWatch({ control: form.control, name: "periodEnd" });
  const isApi = dataSource === "api";
  const preflight = useQuery({
    queryKey: ["schedule-generation-preflight", periodStart, periodEnd],
    queryFn: () => services.scheduleGeneration.preflight({ periodStart, periodEnd }),
    enabled: open && isApi && !!periodStart && !!periodEnd,
    retry: false,
  });

  const start = useMutation({
    mutationFn: (input: {
      periodStart: string;
      periodEnd: string;
      deterministicSeed?: number | null;
      maxSolveSeconds?: number | null;
    }) => services.scheduleGeneration.start(input),
    onSuccess: async (run) => {
      setOpen(false);
      await qc.invalidateQueries({ queryKey: ["schedules"] });
      // A workspace oldal a scheduleId alapján poll-ozza a runt.
      window.location.href = `/app/admin/schedules/${run.schedulePlanId}?run=${run.id}`;
    },
  });

  if (denied) return denied;

  return (
    <div>
      <PageHeader
        title="Beosztások"
        description="Draft, jóváhagyott és publikált beosztási időszakok."
        action={
          <Button onClick={() => setOpen(true)} disabled={!isApi}>
            Új generálás
          </Button>
        }
      />

      {!isApi && (
        <Card className="mb-4 border-amber-200 bg-amber-50">
          <CardContent className="p-4 text-sm text-amber-900">
            A Phase 3B beosztás-generátor csak API módban érhető el. Állítsd be a{" "}
            <code>VITE_DATA_SOURCE=api</code> változót és indítsd újra a devet.
          </CardContent>
        </Card>
      )}

      {list.isLoading && <LoadingState />}
      {!list.isLoading && (list.data ?? []).length === 0 && (
        <EmptyState
          title="Még nincs beosztás"
          description={
            "Kezdd egy új generálással — válaszd ki az időszakot és kattints az „Új generálás” gombra."
          }
        />
      )}

      <div className="grid gap-3">
        {(list.data ?? []).map((s) => (
          <Card key={s.id}>
            <CardContent className="p-4 flex items-center justify-between gap-4">
              <div className="min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <Link
                    to="/app/admin/schedules/$id"
                    params={{ id: s.id }}
                    className="font-semibold hover:underline"
                  >
                    {fmtDate(s.periodStart)} – {fmtDate(s.periodEnd)}
                  </Link>
                  <Badge variant="outline">{s.status}</Badge>
                  {s.blockingIssueCount > 0 && (
                    <Badge className="bg-destructive text-destructive-foreground">
                      {s.blockingIssueCount} blokkoló
                    </Badge>
                  )}
                  {s.warningIssueCount > 0 && (
                    <Badge
                      className="bg-amber-100 text-amber-800 border-amber-200"
                      variant="outline"
                    >
                      {s.warningIssueCount} figyelmeztető
                    </Badge>
                  )}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  {s.shiftCount} műszak · v{s.version} · algoritmus {s.algorithmVersion}
                </p>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link to="/app/admin/schedules/$id" params={{ id: s.id }}>
                  Megnyitás
                </Link>
              </Button>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Új beosztás-generálás</DialogTitle>
          </DialogHeader>
          <form
            className="space-y-3"
            onSubmit={form.handleSubmit((v) =>
              start.mutate({
                periodStart: v.periodStart,
                periodEnd: v.periodEnd,
                deterministicSeed: v.deterministicSeed ? Number(v.deterministicSeed) : null,
                maxSolveSeconds: v.maxSolveSeconds ? Number(v.maxSolveSeconds) : null,
              }),
            )}
          >
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="periodStart">Kezdet</Label>
                <Input id="periodStart" type="date" {...form.register("periodStart")} />
              </div>
              <div>
                <Label htmlFor="periodEnd">Vég</Label>
                <Input id="periodEnd" type="date" {...form.register("periodEnd")} />
              </div>
              <div>
                <Label htmlFor="deterministicSeed">Seed (opcionális)</Label>
                <Input id="deterministicSeed" {...form.register("deterministicSeed")} />
              </div>
              <div>
                <Label htmlFor="maxSolveSeconds">Max megoldó idő (mp)</Label>
                <Input id="maxSolveSeconds" {...form.register("maxSolveSeconds")} />
              </div>
            </div>
            {preflight.isLoading && (
              <p className="text-sm text-muted-foreground">Előfeltételek ellenőrzése…</p>
            )}
            {preflight.data && (
              <div className="space-y-2 rounded border p-3">
                <p className="text-sm font-medium">Generálási előfeltételek</p>
                <p className="text-xs text-muted-foreground">
                  {preflight.data.counts.activeLocationCount} aktív telephely ·{" "}
                  {preflight.data.counts.openingIntervalCount} nyitvatartási intervallum ·{" "}
                  {preflight.data.counts.applicableShiftTemplateCount} alkalmazható műszaksablon ·{" "}
                  {preflight.data.counts.coverageRequirementCount} coverage szabály ·{" "}
                  {preflight.data.counts.autoFillEmployeeCount} autofill dolgozó ·{" "}
                  {preflight.data.counts.candidateOptionCount} jelölt
                </p>
                {preflight.data.issues.map((issue) => (
                  <div
                    key={`${issue.code}-${issue.settingsPath}-${issue.message}`}
                    className={
                      issue.severity === "blocking"
                        ? "text-sm text-destructive"
                        : "text-sm text-amber-700"
                    }
                  >
                    <span>{issue.message}</span>{" "}
                    {issue.settingsPath && (
                      <a className="underline" href={issue.settingsPath}>
                        Megnyitás
                      </a>
                    )}
                  </div>
                ))}
                <div className="space-y-1 border-t pt-2">
                  <p className="text-sm font-medium">Telephelyenként</p>
                  {preflight.data.locations.map((location) => (
                    <div key={location.locationId} className="text-xs text-muted-foreground">
                      <span className="font-medium text-foreground">{location.locationName}</span>
                      {" · "}
                      {location.isActive ? "aktív" : "inaktív"}
                      {" · "}
                      {location.hasOpeningHours ? "nyitvatartás ✓" : "nyitvatartás hiányzik"}
                      {" · "}
                      {location.hasApplicableShiftTemplate
                        ? "műszaksablon ✓"
                        : "műszaksablon hiányzik"}
                      {" · "}
                      {location.hasCoverageRequirement
                        ? "lefedettségi szabály ✓"
                        : "lefedettségi szabály hiányzik"}{" "}
                      <a
                        className="underline"
                        href={`/app/admin/locations?locationId=${location.locationId}`}
                      >
                        Megnyitás
                      </a>
                    </div>
                  ))}
                </div>
                <div className="max-h-48 space-y-1 overflow-auto border-t pt-2">
                  <p className="text-sm font-medium">Dolgozónként</p>
                  {preflight.data.employees.map((employee) => (
                    <div key={employee.employeeId} className="text-xs text-muted-foreground">
                      <span className="font-medium text-foreground">
                        {employee.employeeDisplayName}
                      </span>
                      {" · "}
                      {employee.isActive ? "aktív" : "inaktív"}
                      {" · "}
                      {employee.isSchedulable ? "beosztható" : "nem beosztható"}
                      {" · "}
                      {employee.includeInAutoFill ? "autofill ✓" : "autofill kikapcsolva"}
                      {" · "}
                      {employee.hasLocationAssignment ? "telephely ✓" : "telephely hiányzik"}
                      {" · "}
                      {employee.hasWorkProfile ? "munkaidőprofil ✓" : "munkaidőprofil hiányzik"}
                      {" · "}
                      {employee.hasPositiveContractedTime
                        ? "szerződéses idő ✓"
                        : "szerződéses idő hiányzik"}
                      {" · "}
                      {employee.hasCapability ? "kompetencia ✓" : "kompetencia hiányzik"}
                      {" · "}
                      {employee.hasBlockingAbsenceOrUnavailable
                        ? "távollét/Unavailable érinti"
                        : "nincs blokkoló távollét"}
                      {` · ${employee.candidateOptionCount} jelölt `}
                      <a className="underline" href={`/app/admin/employees/${employee.employeeId}`}>
                        Megnyitás
                      </a>
                    </div>
                  ))}
                </div>
              </div>
            )}
            {preflight.error && (
              <p className="text-sm text-destructive">
                Az előfeltételek ellenőrzése nem sikerült. Próbáld újra.
              </p>
            )}
            {start.error && (
              <p className="text-sm text-destructive">
                {start.error instanceof ApiError
                  ? start.error.message
                  : "A generálás indítása nem sikerült. Próbáld újra."}
              </p>
            )}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                Mégse
              </Button>
              <Button
                type="submit"
                disabled={
                  start.isPending ||
                  preflight.isLoading ||
                  !!preflight.error ||
                  preflight.data?.canStart !== true
                }
              >
                {start.isPending ? "Indítás…" : "Indítás"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
