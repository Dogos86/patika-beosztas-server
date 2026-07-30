import { createFileRoute } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { LoadingState, EmptyState } from "@/components/common/states";
import { useState } from "react";
import type { CoverageRule, StaffingCapability } from "@/services/types";
import { capabilityLabel } from "@/lib/format";
import { CAPABILITIES } from "@/lib/capability-map";
import { toast } from "sonner";
import { Plus, Trash2 } from "lucide-react";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { WeeklyCoverageGrid } from "@/components/coverage/WeeklyCoverageGrid";

const weekdays = ["Hétfő", "Kedd", "Szerda", "Csütörtök", "Péntek", "Szombat", "Vasárnap"];

export const Route = createFileRoute("/app/admin/coverage")({
  head: () => ({ meta: [{ title: "Lefedettség — Patika Beosztás" }] }),
  component: CoveragePage,
});

function CoveragePage() {
  const denied = useRequirePermission(["ManageCoverageRules"]);
  const qc = useQueryClient();
  const rules = useQuery({ queryKey: ["coverage"], queryFn: () => services.coverage.list() });
  const locations = useQuery({
    queryKey: ["locations-all"],
    queryFn: () => services.location.listAll(),
  });
  const [editing, setEditing] = useState<CoverageRule | null>(null);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [gridLocationId, setGridLocationId] = useState<string>("");

  const save = useMutation({
    mutationFn: (r: CoverageRule) => {
      const src = rules.data?.find((x) => x.id === r.id) as
        (CoverageRule & { version?: number }) | undefined;
      const version = src?.version;
      return services.coverage.save({ ...r, __version: version });
    },
    onSuccess: () => {
      toast.success("Mentve");
      qc.invalidateQueries({ queryKey: ["coverage"] });
      setEditing(null);
    },
  });
  const del = useMutation({
    mutationFn: (id: string) => {
      const src = rules.data?.find((x) => x.id === id) as
        (CoverageRule & { version?: number }) | undefined;
      return services.coverage.delete(id, src?.version);
    },
    onSuccess: () => {
      toast.success("Törölve");
      qc.invalidateQueries({ queryKey: ["coverage"] });
      setDeleteId(null);
    },
  });

  const locName = (id: string) => locations.data?.find((l) => l.id === id);
  const activeLocationId =
    gridLocationId || locations.data?.find((l) => l.active)?.id || locations.data?.[0]?.id || "";
  const selectedLocation = locations.data?.find((l) => l.id === activeLocationId);

  if (denied) return denied;
  return (
    <div>
      <PageHeader
        title="Lefedettségi szabályok"
        description="Kötelező létszám telephelyenként és időszakonként."
        action={
          <Button
            onClick={() =>
              setEditing({
                id: `c-${Math.random().toString(36).slice(2, 8)}`,
                locationId: locations.data?.[0]?.id ?? "",
                weekday: 0,
                range: { start: "08:00", end: "16:00" },
                capability: "pharmacist",
                active: true,
                requiredCount: 1,
                severity: "warning",
              })
            }
          >
            <Plus className="h-4 w-4 mr-1" />
            Új szabály
          </Button>
        }
      />
      {rules.isLoading && <LoadingState />}
      {!rules.isLoading && locations.data && locations.data.length > 0 && (
        <Card className="mb-4">
          <CardContent className="p-4 space-y-3">
            <div className="flex flex-wrap items-center gap-2">
              <Label className="mr-2">Heti nézet — telephely:</Label>
              <Select value={activeLocationId} onValueChange={setGridLocationId}>
                <SelectTrigger className="w-60">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(locations.data ?? []).map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {l.name}
                      {!l.active ? " (inaktív)" : ""}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {selectedLocation && (
              <WeeklyCoverageGrid
                locationId={selectedLocation.id}
                openingHours={selectedLocation.openingHours}
                rules={rules.data ?? []}
              />
            )}
          </CardContent>
        </Card>
      )}
      {!rules.isLoading && (rules.data ?? []).length === 0 && (
        <EmptyState title="Nincs szabály" description="Adj hozzá lefedettségi szabályt." />
      )}
      <div className="space-y-2">
        {(rules.data ?? []).map((r) => {
          const loc = locName(r.locationId);
          return (
            <Card key={r.id}>
              <CardContent className="p-4 flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-semibold">{loc?.name ?? "—"}</p>
                    {loc && !loc.active && (
                      <Badge variant="outline" className="bg-slate-100">
                        Inaktív
                      </Badge>
                    )}
                    <Badge variant={r.severity === "blocking" ? "destructive" : "secondary"}>
                      {r.severity === "blocking" ? "Blokkoló" : "Figyelmeztető"}
                    </Badge>
                  </div>
                  <p className="text-sm text-muted-foreground mt-1">
                    {weekdays[r.weekday]} · {r.range.start}–{r.range.end} · {r.requiredCount} ×{" "}
                    {capabilityLabel(r.capability)}
                    {!r.active && " · inaktív"}
                  </p>
                </div>
                <div className="flex gap-1">
                  <Button variant="outline" size="sm" onClick={() => setEditing(r)}>
                    Szerk.
                  </Button>
                  <Button variant="ghost" size="icon" onClick={() => setDeleteId(r.id)}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Lefedettségi szabály</DialogTitle>
          </DialogHeader>
          {editing && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                save.mutate(editing);
              }}
              className="space-y-3"
            >
              <div className="space-y-2">
                <Label>Telephely</Label>
                <Select
                  value={editing.locationId}
                  onValueChange={(v) => setEditing({ ...editing, locationId: v })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(locations.data ?? []).map((l) => (
                      <SelectItem key={l.id} value={l.id}>
                        {l.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Nap</Label>
                <Select
                  value={String(editing.weekday)}
                  onValueChange={(v) => setEditing({ ...editing, weekday: Number(v) })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {weekdays.map((w, i) => (
                      <SelectItem key={i} value={String(i)}>
                        {w}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-2">
                  <Label>Kezdés</Label>
                  <Input
                    type="time"
                    value={editing.range.start}
                    onChange={(e) =>
                      setEditing({ ...editing, range: { ...editing.range, start: e.target.value } })
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Vége</Label>
                  <Input
                    type="time"
                    value={editing.range.end}
                    onChange={(e) =>
                      setEditing({ ...editing, range: { ...editing.range, end: e.target.value } })
                    }
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Kompetencia</Label>
                <Select
                  value={editing.capability}
                  onValueChange={(v) =>
                    setEditing({ ...editing, capability: v as StaffingCapability })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CAPABILITIES.map((c) => (
                      <SelectItem key={c} value={c}>
                        {capabilityLabel(c)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-2">
                  <Label>Szükséges létszám</Label>
                  <Input
                    type="number"
                    min={1}
                    value={editing.requiredCount}
                    onChange={(e) =>
                      setEditing({ ...editing, requiredCount: Number(e.target.value) })
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Súlyosság</Label>
                  <Select
                    value={editing.severity}
                    onValueChange={(v) =>
                      setEditing({ ...editing, severity: v as "warning" | "blocking" })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="warning">Figyelmeztető</SelectItem>
                      <SelectItem value="blocking">Blokkoló</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="flex items-center justify-between">
                <Label>Aktív</Label>
                <input
                  type="checkbox"
                  checked={editing.active}
                  onChange={(e) => setEditing({ ...editing, active: e.target.checked })}
                />
              </div>
              <p className="text-xs text-muted-foreground">
                Átfedő szabályok esetén az időpontban a maximum szükséges létszám érvényes
                (max-overlap szemantika).
              </p>
              <DialogFooter>
                <Button type="button" variant="ghost" onClick={() => setEditing(null)}>
                  Mégse
                </Button>
                <Button type="submit">Mentés</Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>

      <AlertDialog open={deleteId !== null} onOpenChange={(o) => !o && setDeleteId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Törlöd a szabályt?</AlertDialogTitle>
            <AlertDialogDescription>Ez a művelet nem vonható vissza.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Mégse</AlertDialogCancel>
            <AlertDialogAction onClick={() => deleteId && del.mutate(deleteId)}>
              Törlés
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
