import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { services } from "@/services";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "@/components/ui/select";
import { LoadingState } from "@/components/common/states";
import { Plus, Power } from "lucide-react";
import type {
  ShiftTemplate,
  ShiftTemplateCategory,
  ShiftTemplateInput,
  StaffingCapability,
  WeekdayKey,
} from "@/services/types";
import { WEEKDAY_KEYS } from "@/lib/opening-hours";
import { formatHm, parseHm } from "@/lib/duration";
import { capabilityLabel, shiftTemplateCategoryLabel, weekdayLabel } from "@/lib/format";
import { CAPABILITIES } from "@/lib/capability-map";

interface Props {
  locationId: string;
  canEdit: boolean;
}

const CATEGORIES: ShiftTemplateCategory[] = ["AM", "PM", "Long", "Custom"];

function emptyInput(): ShiftTemplateInput {
  return {
    name: "Új sablon",
    category: "AM",
    days: ["mon", "tue", "wed", "thu", "fri"],
    startMin: 8 * 60,
    endMin: 14 * 60,
    active: true,
  };
}

function toInput(t: ShiftTemplate): ShiftTemplateInput {
  return {
    name: t.name,
    category: t.category,
    days: t.days,
    startMin: t.startMin,
    endMin: t.endMin,
    active: t.active,
    requiredCapability: t.requiredCapability,
  };
}

/** Műszaksablonok valódi service-en: külön create / update / deactivate. */
export function ShiftTemplatesEditor({ locationId, canEdit }: Props) {
  const qc = useQueryClient();
  const listQ = useQuery({
    queryKey: ["location-templates", locationId],
    queryFn: () => services.location.listShiftTemplates(locationId, true),
  });
  const [expanded, setExpanded] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<Record<string, ShiftTemplateInput>>({});
  const [creating, setCreating] = useState<ShiftTemplateInput | null>(null);

  const invalidate = () => qc.invalidateQueries({ queryKey: ["location-templates", locationId] });
  const fail = (e: unknown) =>
    toast.error("A sablon mentése nem sikerült.", { description: (e as Error).message });

  const createMut = useMutation({
    mutationFn: (input: ShiftTemplateInput) =>
      services.location.createShiftTemplate(locationId, input),
    onSuccess: () => {
      toast.success("Sablon létrehozva.");
      setCreating(null);
      void invalidate();
    },
    onError: fail,
  });

  const updateMut = useMutation({
    mutationFn: (args: { id: string; input: ShiftTemplateInput; version: number }) =>
      services.location.updateShiftTemplate(args.id, args.input, args.version),
    onSuccess: () => {
      toast.success("Sablon mentve.");
      void invalidate();
    },
    onError: fail,
  });

  const deactivateMut = useMutation({
    mutationFn: (args: { id: string; version: number }) =>
      services.location.deactivateShiftTemplate(args.id, args.version),
    onSuccess: () => {
      toast.success("Sablon deaktiválva.");
      void invalidate();
    },
    onError: (e) =>
      toast.error("A deaktiválás nem sikerült.", { description: (e as Error).message }),
  });

  if (listQ.isLoading) return <LoadingState />;
  if (listQ.isError) {
    return (
      <p className="text-sm text-destructive">
        A sablonok betöltése nem sikerült: {(listQ.error as Error).message}
      </p>
    );
  }

  const items = listQ.data ?? [];

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">
          Sablonok gyorsítják a beosztás készítést és a lefedettségi ellenőrzést.
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={!canEdit || creating !== null}
          onClick={() => setCreating(emptyInput())}
        >
          <Plus className="h-4 w-4 mr-1" />
          Új sablon
        </Button>
      </div>

      {items.length === 0 && creating === null && (
        <p className="text-sm text-muted-foreground italic">Még nincs sablon.</p>
      )}

      {creating && (
        <div className="rounded-md border p-3 space-y-3">
          <p className="text-sm font-medium">Új sablon</p>
          <TemplateFields value={creating} onChange={setCreating} disabled={!canEdit} />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" onClick={() => setCreating(null)}>
              Mégse
            </Button>
            <Button
              type="button"
              size="sm"
              disabled={!canEdit || createMut.isPending}
              onClick={() => createMut.mutate(creating)}
            >
              Létrehozás
            </Button>
          </div>
        </div>
      )}

      <div className="space-y-2">
        {items.map((t) => {
          const open = expanded === t.id;
          const draft = drafts[t.id] ?? toInput(t);
          return (
            <div key={t.id} className="rounded-md border">
              <div className="flex items-center justify-between gap-2 p-2">
                <button
                  type="button"
                  className="text-left flex-1"
                  onClick={() => setExpanded(open ? null : t.id)}
                >
                  <div className="font-medium text-sm">
                    {t.name}{" "}
                    <span className="text-xs text-muted-foreground">
                      · {shiftTemplateCategoryLabel(t.category)} · {formatHm(t.startMin)}–
                      {formatHm(t.endMin)}
                    </span>
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {t.days.map(weekdayLabel).join(", ")}
                    {!t.active && " · inaktív"}
                    {t.requiredCapability && ` · ${capabilityLabel(t.requiredCapability)}`}
                  </div>
                </button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Deaktiválás"
                  disabled={!canEdit || !t.active || deactivateMut.isPending}
                  onClick={() => deactivateMut.mutate({ id: t.id, version: t.version ?? 0 })}
                >
                  <Power className="h-4 w-4" />
                </Button>
              </div>
              {open && (
                <div className="p-3 border-t space-y-3">
                  <TemplateFields
                    value={draft}
                    onChange={(v) => setDrafts((d) => ({ ...d, [t.id]: v }))}
                    disabled={!canEdit}
                  />
                  <div className="flex justify-end">
                    <Button
                      type="button"
                      size="sm"
                      disabled={!canEdit || updateMut.isPending}
                      onClick={() =>
                        updateMut.mutate({ id: t.id, input: draft, version: t.version ?? 0 })
                      }
                    >
                      Sablon mentése
                    </Button>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function TemplateFields({
  value,
  onChange,
  disabled,
}: {
  value: ShiftTemplateInput;
  onChange: (v: ShiftTemplateInput) => void;
  disabled?: boolean;
}) {
  const patch = (p: Partial<ShiftTemplateInput>) => onChange({ ...value, ...p });
  return (
    <>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1">
          <Label>Név</Label>
          <Input
            value={value.name}
            disabled={disabled}
            onChange={(e) => patch({ name: e.target.value })}
          />
        </div>
        <div className="space-y-1">
          <Label>Kategória</Label>
          <Select
            value={value.category}
            disabled={disabled}
            onValueChange={(v) => patch({ category: v as ShiftTemplateCategory })}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {CATEGORIES.map((c) => (
                <SelectItem key={c} value={c}>
                  {shiftTemplateCategoryLabel(c)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Kezdés</Label>
          <Input
            type="time"
            step={60}
            disabled={disabled}
            value={formatHm(value.startMin)}
            onChange={(e) => {
              const m = parseHm(e.target.value);
              if (m !== null) patch({ startMin: m });
            }}
          />
        </div>
        <div className="space-y-1">
          <Label>Vége</Label>
          <Input
            type="time"
            step={60}
            disabled={disabled}
            value={formatHm(value.endMin)}
            onChange={(e) => {
              const m = parseHm(e.target.value);
              if (m !== null) patch({ endMin: m });
            }}
          />
        </div>
        <div className="space-y-1 sm:col-span-2">
          <Label>Kompetencia (opcionális)</Label>
          <Select
            value={value.requiredCapability ?? "__none"}
            disabled={disabled}
            onValueChange={(v) =>
              patch({ requiredCapability: v === "__none" ? undefined : (v as StaffingCapability) })
            }
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__none">Nincs megkötés</SelectItem>
              {CAPABILITIES.map((c) => (
                <SelectItem key={c} value={c}>
                  {capabilityLabel(c)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div>
        <Label className="block mb-1">Napok</Label>
        <div className="flex flex-wrap gap-2">
          {WEEKDAY_KEYS.map((d) => (
            <label key={d} className="flex items-center gap-1 text-sm">
              <Checkbox
                checked={value.days.includes(d)}
                disabled={disabled}
                onCheckedChange={(v) => {
                  const set = new Set<WeekdayKey>(value.days);
                  if (v) set.add(d);
                  else set.delete(d);
                  patch({ days: WEEKDAY_KEYS.filter((x) => set.has(x)) });
                }}
              />
              {weekdayLabel(d).slice(0, 3)}
            </label>
          ))}
        </div>
      </div>
      <label className="flex items-center gap-2 text-sm">
        <Checkbox
          checked={value.active}
          disabled={disabled}
          onCheckedChange={(v) => patch({ active: Boolean(v) })}
        />
        Aktív
      </label>
    </>
  );
}
