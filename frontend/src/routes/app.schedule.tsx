import { createFileRoute } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useMyEmployeeId } from "@/hooks/use-auth";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { services, dataSource } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { LoadingState, EmptyState } from "@/components/common/states";
import {
  fmtDate,
  fmtWeekday,
  weekStartISO,
  addDaysISO,
  shiftTypeLabel,
  timeTypeLabel,
  fmtDateTime,
} from "@/lib/format";
import { ChevronLeft, ChevronRight } from "lucide-react";

export const Route = createFileRoute("/app/schedule")({
  head: () => ({ meta: [{ title: "Beosztásom — Patika Beosztás" }] }),
  component: SchedulePage,
});

function SchedulePage() {
  const denied = useRequirePermission(["ViewOwnSchedule"]);
  const employeeId = useMyEmployeeId();
  const [weekStart, setWeekStart] = useState(weekStartISO());
  const weekEnd = addDaysISO(weekStart, 6);
  const isApi = dataSource === "api";

  // API módban a Phase 3B publikált saját beosztás endpoint az elsődleges;
  // mock módban a régi `getMySchedule` shift-lista adja az adatot.
  const ownPublished = useQuery({
    enabled: isApi,
    queryKey: ["ownPublishedSchedule", weekStart],
    queryFn: () => services.schedule.getOwnPublishedSchedule({ date: weekStart }),
  });

  const shifts = useQuery({
    enabled: !isApi && !!employeeId,
    queryKey: ["mySchedule", employeeId, weekStart],
    queryFn: () => services.schedule.getMySchedule({ from: weekStart, to: weekEnd }),
  });
  const locations = useQuery({
    queryKey: ["locations-all"],
    queryFn: () => services.location.listAll(),
  });

  const days = Array.from({ length: 7 }, (_, i) => addDaysISO(weekStart, i));
  const locName = (id: string) => locations.data?.find((l) => l.id === id)?.name ?? "—";

  // API módban a szerver session-ből dönt; mock módban linked employee kell.
  if (denied) return denied;

  if (!isApi && !employeeId) {
    return (
      <div>
        <PageHeader
          title="Saját beosztás"
          description="Nincs dolgozói profil a fiókodhoz kapcsolva."
        />
        <EmptyState
          title="Nem elérhető"
          description="Az adminnak először kapcsolnia kell a fiókodat egy dolgozói rekordhoz."
        />
      </div>
    );
  }

  const apiShifts = (ownPublished.data?.shifts ?? []).filter(
    (s) => s.date >= weekStart && s.date <= weekEnd,
  );
  const isLoading = isApi ? ownPublished.isLoading : shifts.isLoading;
  const hasData = isApi ? apiShifts.length > 0 : (shifts.data ?? []).length > 0;
  const shiftsForDay = (d: string) =>
    isApi
      ? apiShifts
          .filter((s) => s.date === d)
          .map((s) => ({
            id: s.id,
            date: s.date,
            start: s.startTime,
            end: s.endTime,
            locationId: s.locationId,
            locationName: s.locationName,
            typeLabel: s.segments[0]?.timeType ?? "work",
            segments: s.segments,
            changed: false,
          }))
      : (shifts.data ?? [])
          .filter((s) => s.date === d)
          .map((s) => ({
            id: s.id,
            date: s.date,
            start: s.start,
            end: s.end,
            locationId: s.locationId,
            locationName: locName(s.locationId),
            typeLabel: s.type,
            segments: [] as { timeType: string; startTime: string; endTime: string }[],
            changed: !!s.changed,
          }));

  return (
    <div>
      <PageHeader
        title="Saját beosztás"
        description={
          isApi && ownPublished.data
            ? `${fmtDate(weekStart)} – ${fmtDate(weekEnd)} · publikált v${ownPublished.data.publishedRevisionNumber} · ${fmtDateTime(ownPublished.data.publishedAtUtc)}`
            : `${fmtDate(weekStart)} – ${fmtDate(weekEnd)}`
        }
        action={
          <div className="flex gap-1">
            <Button
              variant="outline"
              size="icon"
              onClick={() => setWeekStart(addDaysISO(weekStart, -7))}
              aria-label="Előző hét"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              onClick={() => setWeekStart(addDaysISO(weekStart, 7))}
              aria-label="Következő hét"
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        }
      />

      <Tabs defaultValue="week">
        <TabsList>
          <TabsTrigger value="week">Heti nézet</TabsTrigger>
          <TabsTrigger value="month">Havi nézet</TabsTrigger>
        </TabsList>

        <TabsContent value="week" className="mt-4">
          {isLoading && <LoadingState />}
          {!isLoading && !hasData && (
            <EmptyState
              title="Nincs beosztott műszak"
              description="Erre a hétre nincs rögzített műszakod."
            />
          )}

          <div className="space-y-2 md:hidden">
            {days.map((d) => {
              const dayShifts = shiftsForDay(d);
              if (dayShifts.length === 0) return null;
              return dayShifts.map((s) => (
                <Card key={s.id}>
                  <CardContent className="p-3 flex items-center justify-between gap-3">
                    <div>
                      <p className="text-xs text-muted-foreground uppercase">{fmtWeekday(d)}</p>
                      <p className="font-semibold">{fmtDate(d)}</p>
                      <p className="text-sm">
                        {s.start}–{s.end} · {s.locationName}
                      </p>
                      {s.segments.length > 0 && (
                        <p className="text-xs text-muted-foreground">
                          {s.segments
                            .map(
                              (seg) =>
                                `${timeTypeLabel(seg.timeType)} ${seg.startTime}–${seg.endTime}`,
                            )
                            .join(" · ")}
                        </p>
                      )}
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <Badge variant="secondary">
                        {isApi ? timeTypeLabel(s.typeLabel) : shiftTypeLabel(s.typeLabel)}
                      </Badge>
                      {s.changed && (
                        <Badge
                          className="bg-amber-100 text-amber-800 border-amber-200"
                          variant="outline"
                        >
                          Módosított
                        </Badge>
                      )}
                    </div>
                  </CardContent>
                </Card>
              ));
            })}
          </div>

          <div className="hidden md:grid grid-cols-7 gap-2">
            {days.map((d) => {
              const dayShifts = shiftsForDay(d);
              return (
                <div key={d} className="min-h-32 rounded-md border bg-card p-2">
                  <div className="text-xs text-muted-foreground uppercase">{fmtWeekday(d)}</div>
                  <div className="text-sm font-semibold mb-2">{fmtDate(d)}</div>
                  {dayShifts.map((s) => (
                    <div
                      key={s.id}
                      className="rounded bg-primary/10 border border-primary/20 p-2 mb-1"
                    >
                      <p className="text-sm font-medium">
                        {s.start}–{s.end}
                      </p>
                      <p className="text-xs text-muted-foreground">{s.locationName}</p>
                      {s.segments.map((seg, i) => (
                        <p key={i} className="text-[10px] text-muted-foreground">
                          {timeTypeLabel(seg.timeType)} {seg.startTime}–{seg.endTime}
                        </p>
                      ))}
                      {s.changed && <p className="text-[10px] text-amber-700 mt-1">Módosított</p>}
                    </div>
                  ))}
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="month" className="mt-4">
          <Card>
            <CardContent className="p-6 text-sm text-muted-foreground">
              Havi nézet – hamarosan bővül. Egyelőre lapozz hetenként.
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
