import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { services } from "@/services";
import { ApiError } from "@/services/http/errors";
import type { WeekdayKey, WorkPreference, WorkPreferenceInput } from "@/services/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { WEEKDAY_KEYS } from "@/lib/opening-hours";
import { weekdayLabel } from "@/lib/format";
import {
  WORK_PREFERENCE_TYPES,
  WORK_PREFERENCE_TYPE_HINTS,
  workPreferenceTypeLabel,
} from "@/lib/work-preference-labels";

type Mode = "self" | "admin";

interface Props {
  mode: Mode;
  /** Admin módban kötelező; self módban nem küldünk employeeId-t. */
  employeeId?: string;
  canEdit?: boolean;
}

const today = () => new Date().toISOString().slice(0, 10);

function emptyInput(): WorkPreferenceInput {
  return {
    type: "Preferred",
    dateFrom: today(),
    dateTo: today(),
    weekday: null,
    isFullDay: true,
    startTime: null,
    endTime: null,
    locationId: null,
    note: null,
  };
}

function toInput(p: WorkPreference): WorkPreferenceInput {
  return {
    type: p.type,
    dateFrom: p.dateFrom,
    dateTo: p.dateTo,
    weekday: p.weekday,
    isFullDay: p.isFullDay,
    startTime: p.startTime,
    endTime: p.endTime,
    locationId: p.locationId,
    note: p.note,
  };
}

function strongInput(p: WorkPreference) {
  return p.type === "Unavailable" || p.type === "Fixed";
}

export function WorkPreferencesCard({ mode, employeeId, canEdit = true }: Props) {
  const qc = useQueryClient();
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<WorkPreference | null>(null);
  const [draft, setDraft] = useState<WorkPreferenceInput | null>(null);

  const queryKey = useMemo(
    () => ["work-preferences", mode, employeeId ?? "me", includeInactive] as const,
    [mode, employeeId, includeInactive],
  );

  const listQ = useQuery({
    queryKey,
    enabled: mode === "self" || !!employeeId,
    queryFn: () =>
      mode === "self"
        ? services.workPreference.listMine(includeInactive)
        : services.adminWorkPreference.listForEmployee(employeeId!, includeInactive),
  });

  const locationsQ = useQuery({
    queryKey: ["locations-all"],
    queryFn: () => services.location.listAll(),
    staleTime: 300_000,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ["work-preferences"] });

  const onError = (e: unknown) => {
    const err = e as ApiError;
    if (err?.code === "CONFLICT") {
      toast.error("Közben más módosította — az adatokat újratöltöttük.");
      invalidate();
      return;
    }
    if (err?.code === "VALIDATION") {
      const fields = err.fieldErrors ? Object.values(err.fieldErrors).flat().join(" ") : "";
      toast.error(`Érvénytelen adat. ${fields}`.trim());
      return;
    }
    toast.error(err?.message ?? "A művelet nem sikerült.");
  };

  const saveM = useMutation({
    mutationFn: async (input: WorkPreferenceInput) => {
      if (editing) {
        return mode === "self"
          ? services.workPreference.updateMine(editing.id, input, editing.version)
          : services.adminWorkPreference.update(editing.id, input, editing.version);
      }
      return mode === "self"
        ? services.workPreference.createMine(input)
        : services.adminWorkPreference.createForEmployee(employeeId!, input);
    },
    onSuccess: () => {
      toast.success("Mentve.");
      setDraft(null);
      setEditing(null);
      invalidate();
    },
    onError,
  });

  const deactivateM = useMutation({
    mutationFn: (p: WorkPreference) =>
      mode === "self"
        ? services.workPreference.deactivateMine(p.id, p.version)
        : services.adminWorkPreference.deactivate(p.id, p.version),
    onSuccess: () => {
      toast.success("Inaktiválva.");
      invalidate();
    },
    onError,
  });

  const openCreate = () => {
    setEditing(null);
    setDraft(emptyInput());
  };
  const openEdit = (p: WorkPreference) => {
    setEditing(p);
    setDraft(toInput(p));
  };

  const items = listQ.data ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2 flex-wrap">
        <CardTitle className="text-base">Munkavégzési kérések és visszatérő szabályok</CardTitle>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-2 text-xs text-muted-foreground">
            <Switch checked={includeInactive} onCheckedChange={setIncludeInactive} />
            Inaktívak is
          </label>
          {canEdit && (
            <Button size="sm" onClick={openCreate}>
              Új szabály
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-xs text-muted-foreground">
          A Preferált és Kerülendő bejegyzés optimalizálási kívánság, a Nem elérhető és Rögzített
          erős generátori bemenet.
        </p>

        {listQ.isLoading && <p className="text-sm text-muted-foreground">Betöltés…</p>}
        {listQ.isError && (
          <p className="text-sm text-destructive">
            Nem sikerült betölteni: {(listQ.error as Error).message}
          </p>
        )}
        {!listQ.isLoading && !listQ.isError && items.length === 0 && (
          <p className="text-sm text-muted-foreground italic">Nincs rögzített szabály.</p>
        )}

        <div className="grid gap-2">
          {items.map((p) => (
            <div
              key={p.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3"
            >
              <div className="min-w-0 space-y-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <Badge variant={strongInput(p) ? "default" : "outline"}>
                    {workPreferenceTypeLabel(p.type)}
                  </Badge>
                  {!p.isActive && <Badge variant="secondary">inaktív</Badge>}
                  {p.weekday && (
                    <span className="text-xs text-muted-foreground">{weekdayLabel(p.weekday)}</span>
                  )}
                </div>
                <p className="text-sm">
                  {p.dateFrom} – {p.dateTo} ·{" "}
                  {p.isFullDay ? "egész nap" : `${p.startTime ?? "?"}–${p.endTime ?? "?"}`}
                  {p.locationName ? ` · ${p.locationName}` : ""}
                </p>
                {p.note && <p className="text-xs text-muted-foreground">{p.note}</p>}
              </div>
              {canEdit && (
                <div className="flex items-center gap-2">
                  <Button size="sm" variant="outline" onClick={() => openEdit(p)}>
                    Szerkesztés
                  </Button>
                  {p.isActive && (
                    <Button
                      size="sm"
                      variant="ghost"
                      disabled={deactivateM.isPending}
                      onClick={() => deactivateM.mutate(p)}
                    >
                      Inaktiválás
                    </Button>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      </CardContent>

      <Dialog
        open={draft !== null}
        onOpenChange={(o) => {
          if (!o) {
            setDraft(null);
            setEditing(null);
          }
        }}
      >
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? "Szabály szerkesztése" : "Új szabály"}</DialogTitle>
          </DialogHeader>
          {draft && (
            <div className="space-y-3">
              <div className="space-y-1">
                <Label>Típus</Label>
                <Select
                  value={draft.type}
                  onValueChange={(v) =>
                    setDraft({ ...draft, type: v as WorkPreferenceInput["type"] })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {WORK_PREFERENCE_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>
                        {workPreferenceTypeLabel(t)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {WORK_PREFERENCE_TYPE_HINTS[draft.type]}
                </p>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label>Kezdő dátum</Label>
                  <Input
                    type="date"
                    value={draft.dateFrom}
                    onChange={(e) => setDraft({ ...draft, dateFrom: e.target.value })}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Záró dátum</Label>
                  <Input
                    type="date"
                    value={draft.dateTo}
                    onChange={(e) => setDraft({ ...draft, dateTo: e.target.value })}
                  />
                </div>
              </div>

              <div className="space-y-1">
                <Label>Hét napja</Label>
                <Select
                  value={draft.weekday ?? "all"}
                  onValueChange={(v) =>
                    setDraft({ ...draft, weekday: v === "all" ? null : (v as WeekdayKey) })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Minden nap</SelectItem>
                    {WEEKDAY_KEYS.map((k) => (
                      <SelectItem key={k} value={k}>
                        {weekdayLabel(k)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <label className="flex items-center gap-2 text-sm">
                <Switch
                  checked={draft.isFullDay}
                  onCheckedChange={(c) =>
                    setDraft({
                      ...draft,
                      isFullDay: c,
                      startTime: c ? null : (draft.startTime ?? "08:00"),
                      endTime: c ? null : (draft.endTime ?? "16:00"),
                    })
                  }
                />
                Egész nap
              </label>

              {!draft.isFullDay && (
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label>Kezdés</Label>
                    <Input
                      type="time"
                      value={draft.startTime ?? ""}
                      onChange={(e) => setDraft({ ...draft, startTime: e.target.value })}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label>Vége</Label>
                    <Input
                      type="time"
                      value={draft.endTime ?? ""}
                      onChange={(e) => setDraft({ ...draft, endTime: e.target.value })}
                    />
                  </div>
                </div>
              )}

              <div className="space-y-1">
                <Label>Telephely (opcionális)</Label>
                <Select
                  value={draft.locationId ?? "none"}
                  onValueChange={(v) => setDraft({ ...draft, locationId: v === "none" ? null : v })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Bármelyik</SelectItem>
                    {(locationsQ.data ?? []).map((l) => (
                      <SelectItem key={l.id} value={l.id}>
                        {l.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1">
                <Label>Megjegyzés</Label>
                <Textarea
                  value={draft.note ?? ""}
                  onChange={(e) => setDraft({ ...draft, note: e.target.value || null })}
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setDraft(null);
                setEditing(null);
              }}
            >
              Mégse
            </Button>
            <Button
              disabled={saveM.isPending || !draft}
              onClick={() => draft && saveM.mutate(draft)}
            >
              Mentés
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
