import { useMemo } from "react";
import type { CoverageRule, LocationOpeningHours, StaffingCapability } from "@/services/types";
import { WEEKDAY_KEYS, defaultOpeningHours, isMinuteOpen } from "@/lib/opening-hours";
import { buildDemandCurve, maxRequiredBetween } from "@/lib/coverage-overlap";
import { CAPABILITIES } from "@/lib/capability-map";
import { capabilityLabel, weekdayLabel } from "@/lib/format";

interface Props {
  locationId: string;
  openingHours?: LocationOpeningHours;
  rules: CoverageRule[];
}

/** Heti óra-rács: sorok = kompetencia+nap, oszlopok = 24 óra, cellák = igényelt fő. */
export function WeeklyCoverageGrid({ locationId, openingHours, rules }: Props) {
  const hours = openingHours ?? defaultOpeningHours();
  const locationRules = useMemo(
    () => rules.filter((r) => r.locationId === locationId && r.active),
    [rules, locationId],
  );
  const usedCaps = useMemo<StaffingCapability[]>(() => {
    const set = new Set<StaffingCapability>();
    for (const r of locationRules) set.add(r.capability);
    return CAPABILITIES.filter((c) => set.has(c));
  }, [locationRules]);

  if (locationRules.length === 0) {
    return (
      <p className="text-sm text-muted-foreground italic">
        A telephelyhez még nincs lefedettségi szabály.
      </p>
    );
  }

  return (
    <div className="space-y-4">
      {usedCaps.map((cap) => (
        <div key={cap} className="rounded-md border overflow-x-auto">
          <div className="p-2 border-b bg-muted/40 text-sm font-medium">{capabilityLabel(cap)}</div>
          <table className="w-full text-[10px] border-collapse">
            <thead>
              <tr>
                <th className="border-b p-1 text-left min-w-16">Nap</th>
                {Array.from({ length: 24 }).map((_, h) => (
                  <th
                    key={h}
                    className="border-b p-0.5 text-center font-normal text-muted-foreground"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {WEEKDAY_KEYS.map((wk, wIdx) => {
                const points = buildDemandCurve(locationRules, wIdx, cap);
                const day = hours[wk];
                return (
                  <tr key={wk} className="border-b">
                    <td className="p-1 font-medium">{weekdayLabel(wk).slice(0, 3)}</td>
                    {Array.from({ length: 24 }).map((_, h) => {
                      const req = maxRequiredBetween(points, h * 60, (h + 1) * 60);
                      const open = isMinuteOpen(day, h * 60);
                      const cls = !open
                        ? "bg-slate-100 text-slate-400"
                        : req >= 3
                          ? "bg-rose-200"
                          : req === 2
                            ? "bg-amber-200"
                            : req === 1
                              ? "bg-emerald-100"
                              : "";
                      return (
                        <td key={h} className={`text-center p-0.5 border-l ${cls}`}>
                          {open ? req || "" : ""}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ))}
      <p className="text-xs text-muted-foreground">
        A cellák az adott órára eső maximálisan igényelt létszámot mutatják (átfedő szabályok
        max-overlap szemantikával). Szürke: zárva. Zöld: 1 fő. Sárga: 2 fő. Piros: 3+ fő.
      </p>
    </div>
  );
}
