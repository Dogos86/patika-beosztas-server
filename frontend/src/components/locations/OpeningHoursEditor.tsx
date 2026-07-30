import { useMemo } from "react";
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
import { Copy, Plus, Trash2 } from "lucide-react";
import type {
  LocationOpeningHours,
  OpeningHoursDay,
  OpeningHoursMode,
  OpeningInterval,
  WeekdayKey,
} from "@/services/types";
import {
  WEEKDAY_KEYS,
  emptyDay,
  twentyFourDay,
  validateDay,
  defaultOpeningHours,
} from "@/lib/opening-hours";
import { formatHm, parseHm } from "@/lib/duration";
import { openingModeLabel, weekdayLabel } from "@/lib/format";
import { cn } from "@/lib/utils";

interface Props {
  value?: LocationOpeningHours;
  onChange: (v: LocationOpeningHours) => void;
}

const WEEKDAYS_ONLY: WeekdayKey[] = ["mon", "tue", "wed", "thu", "fri"];

export function OpeningHoursEditor({ value, onChange }: Props) {
  const hours = value ?? defaultOpeningHours();

  const setDay = (k: WeekdayKey, day: OpeningHoursDay) => onChange({ ...hours, [k]: day });

  const setMode = (k: WeekdayKey, mode: OpeningHoursMode) => {
    if (mode === "closed") setDay(k, emptyDay());
    else if (mode === "twentyFour") setDay(k, twentyFourDay());
    else
      setDay(k, {
        mode: "custom",
        intervals:
          hours[k].intervals.length > 0
            ? hours[k].intervals
            : [{ startMin: 8 * 60, endMin: 16 * 60 }],
      });
  };

  const copyWeekdays = () => {
    const src = hours.mon;
    const next: LocationOpeningHours = { ...hours };
    for (const k of WEEKDAYS_ONLY) next[k] = cloneDay(src);
    onChange(next);
  };

  const copyPrev = (k: WeekdayKey) => {
    const idx = WEEKDAY_KEYS.indexOf(k);
    if (idx <= 0) return;
    const prev = hours[WEEKDAY_KEYS[idx - 1]];
    setDay(k, cloneDay(prev));
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Napi mód és intervallumok. Az adatok helyi időben, percpontosan kerülnek tárolásra.
        </p>
        <Button type="button" variant="outline" size="sm" onClick={copyWeekdays}>
          <Copy className="h-4 w-4 mr-1" />
          Hétfő → hétköznapok
        </Button>
      </div>
      <div className="space-y-2">
        {WEEKDAY_KEYS.map((k, i) => (
          <DayRow
            key={k}
            weekday={k}
            day={hours[k]}
            onModeChange={(m) => setMode(k, m)}
            onChange={(d) => setDay(k, d)}
            onCopyPrev={i > 0 ? () => copyPrev(k) : undefined}
          />
        ))}
      </div>
    </div>
  );
}

function cloneDay(d: OpeningHoursDay): OpeningHoursDay {
  return { mode: d.mode, intervals: d.intervals.map((iv) => ({ ...iv })) };
}

function DayRow({
  weekday,
  day,
  onModeChange,
  onChange,
  onCopyPrev,
}: {
  weekday: WeekdayKey;
  day: OpeningHoursDay;
  onModeChange: (m: OpeningHoursMode) => void;
  onChange: (d: OpeningHoursDay) => void;
  onCopyPrev?: () => void;
}) {
  const errors = useMemo(() => validateDay(weekday, day), [weekday, day]);

  const setInterval = (idx: number, patch: Partial<OpeningInterval>) => {
    const intervals = day.intervals.map((iv, i) => (i === idx ? { ...iv, ...patch } : iv));
    onChange({ ...day, intervals });
  };
  const addInterval = () =>
    onChange({
      ...day,
      intervals: [...day.intervals, { startMin: 8 * 60, endMin: 16 * 60 }],
    });
  const removeInterval = (idx: number) =>
    onChange({ ...day, intervals: day.intervals.filter((_, i) => i !== idx) });

  return (
    <div
      className={cn(
        "rounded-md border p-3 space-y-2",
        errors.length > 0 && "border-destructive/60 bg-destructive/5",
      )}
    >
      <div className="flex flex-wrap items-center gap-2">
        <Label className="w-20 shrink-0">{weekdayLabel(weekday)}</Label>
        <Select value={day.mode} onValueChange={(v) => onModeChange(v as OpeningHoursMode)}>
          <SelectTrigger className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(["closed", "twentyFour", "custom"] as OpeningHoursMode[]).map((m) => (
              <SelectItem key={m} value={m}>
                {openingModeLabel(m)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {onCopyPrev && (
          <Button type="button" variant="ghost" size="sm" onClick={onCopyPrev}>
            Előző nap másolása
          </Button>
        )}
      </div>
      {day.mode === "custom" && (
        <div className="space-y-2 sm:pl-24">
          {day.intervals.map((iv, i) => (
            <div key={i} className="flex flex-wrap items-center gap-2">
              <Input
                type="time"
                step={60}
                className="w-32"
                value={formatHm(iv.startMin)}
                onChange={(e) => {
                  const m = parseHm(e.target.value);
                  if (m !== null) setInterval(i, { startMin: m });
                }}
              />
              <span className="text-muted-foreground">–</span>
              <Input
                type="time"
                step={60}
                className="w-32"
                value={formatHm(iv.endMin)}
                onChange={(e) => {
                  const m = parseHm(e.target.value);
                  if (m !== null) setInterval(i, { endMin: m });
                }}
              />
              <Button type="button" variant="ghost" size="icon" onClick={() => removeInterval(i)}>
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          ))}
          <Button type="button" variant="outline" size="sm" onClick={addInterval}>
            <Plus className="h-4 w-4 mr-1" />
            Új intervallum
          </Button>
        </div>
      )}
      {errors.length > 0 && (
        <ul className="text-xs text-destructive space-y-0.5">
          {errors.map((e, i) => (
            <li key={i}>• {e.message}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
