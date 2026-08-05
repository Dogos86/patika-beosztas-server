import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { services } from "@/services";
import { WorkPreferencesCard } from "@/components/work-preferences/WorkPreferencesCard";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "@/components/ui/select";
import { Checkbox } from "@/components/ui/checkbox";
import { LoadingState } from "@/components/common/states";
import { useEffect, useState } from "react";
import type {
  ApiQuotaPeriod,
  ApiQuotaSeverity,
  ApiShiftQuotaDimension,
  CreateShiftQuotaRuleInput,
  Employee,
  EmployeeShiftQuotaRule,
  EmployeeWorkProfile,
  PreferenceWindow,
  ProfessionalRole,
  RecurringRuleKind,
  RecurringWorkRule,
  ShiftType,
  StaffingCapability,
  Weekday,
} from "@/services/types";
import {
  capabilityLabel,
  minutesToHuman,
  professionalRoleLabel,
  recurringRuleKindLabel,
  weekdayLabel,
} from "@/lib/format";
import { formatHm, parseHm } from "@/lib/duration";
import { CAPABILITIES } from "@/lib/capability-map";
import { toast } from "sonner";
import { ApiError } from "@/services/http/errors";
import { hoursAndMinutesToMinutes, splitMinutes } from "@/lib/minutes";
import {
  getWorkProfileFieldErrors,
  refetchEmployeeWorkProfile,
  setLongShiftAllowed,
  type WorkProfileField,
} from "@/lib/work-profile";
import { X, Plus, Save } from "lucide-react";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { PayrollHrTab } from "@/components/payroll/PayrollHrTab";
import { useHasAnyPermission } from "@/hooks/use-auth";

export const Route = createFileRoute("/app/admin/employees/$id")({
  head: () => ({ meta: [{ title: "Dolgozó szerkesztése — Patika Beosztás" }] }),
  component: EmployeeEditor,
});

const PROF_ROLES: ProfessionalRole[] = [
  "pharmacy_manager",
  "pharmacist",
  "specialist_assistant",
  "assistant",
  "pharmacist_trainee",
  "assistant_trainee",
  "cleaner",
  "finance_helper",
  "other",
];
const WEEKDAYS: Weekday[] = ["every", "mon", "tue", "wed", "thu", "fri", "sat", "sun"];
const SHIFT_TYPES: { id: ShiftType; label: string }[] = [
  { id: "work", label: "Munka" },
  { id: "on_call", label: "Ügyelet" },
  { id: "training", label: "Képzés" },
  { id: "meeting", label: "Értekezlet" },
];

function EmployeeEditor() {
  const denied = useRequirePermission(["ManageEmployees"]);
  const canSeeHr = useHasAnyPermission([
    "ManagePayrollOnboarding",
    "ReviewTaxAllowanceSurvey",
    "ViewPayrollSensitiveData",
  ]);
  const { id } = Route.useParams();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const emp = useQuery({ queryKey: ["employee", id], queryFn: () => services.employee.get(id) });
  const locations = useQuery({
    queryKey: ["locations-all"],
    queryFn: () => services.location.listAll(),
  });
  const [form, setForm] = useState<Employee | null>(null);

  useEffect(() => {
    if (emp.data && !form) setForm(emp.data);
  }, [emp.data, form]);

  const saveMut = useMutation({
    mutationFn: (e: Employee) => {
      const version = (e as Employee & { version?: number }).version;
      if (typeof version !== "number") {
        throw new Error("Hiányzó verziószám — töltsd újra a dolgozó adatlapját.");
      }
      return services.employee.update(e.id, e, version);
    },
    onSuccess: () => {
      toast.success("Mentve");
      qc.invalidateQueries({ queryKey: ["employees"] });
      qc.invalidateQueries({ queryKey: ["employee", id] });
      navigate({ to: "/app/admin/employees" });
    },
  });

  if (emp.isLoading || !form) return <LoadingState />;

  const update = (patch: Partial<Employee>) => setForm({ ...form, ...patch });
  const updateWin = (
    key: "preferredWindows" | "blockedWindows",
    idx: number,
    patch: Partial<PreferenceWindow>,
  ) => {
    const arr = [...form[key]];
    arr[idx] = { ...arr[idx], ...patch };
    update({ [key]: arr } as Partial<Employee>);
  };
  const addWin = (key: "preferredWindows" | "blockedWindows") =>
    update({
      [key]: [
        ...form[key],
        {
          weekday: "every" as Weekday,
          start: "08:00",
          end: "16:00",
          kind: key === "preferredWindows" ? "preferred" : "blocked",
        },
      ],
    } as Partial<Employee>);
  const removeWin = (key: "preferredWindows" | "blockedWindows", idx: number) =>
    update({ [key]: form[key].filter((_, i) => i !== idx) } as Partial<Employee>);
  const toggleShiftType = (t: ShiftType, on: boolean) =>
    update({
      allowedShiftTypes: on
        ? [...new Set([...form.allowedShiftTypes, t])]
        : form.allowedShiftTypes.filter((x) => x !== t),
    });

  const hoursOfMax = Math.floor(form.maxDailyMinutes / 60);
  const minsOfMax = form.maxDailyMinutes % 60;

  if (denied) return denied;
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        saveMut.mutate(form);
      }}
    >
      <PageHeader
        title={form.fullName}
        description="Szakmai adatok, munkaidőprofil, elérhetőség és kompetenciák."
        action={
          <div className="flex gap-2">
            <Button
              type="button"
              variant="ghost"
              onClick={() => navigate({ to: "/app/admin/employees" })}
            >
              Vissza
            </Button>
            <Button type="submit">Mentés</Button>
          </div>
        }
      />

      <Tabs defaultValue="basic" className="w-full">
        <TabsList className="flex flex-wrap h-auto">
          <TabsTrigger value="basic">Alap</TabsTrigger>
          <TabsTrigger value="capabilities">Kompetenciák</TabsTrigger>
          <TabsTrigger value="workProfile">Munkaidőprofil és elérhetőség</TabsTrigger>
          <TabsTrigger value="recurring">Visszatérő szabályok</TabsTrigger>
          <TabsTrigger value="quotas">Kvóták</TabsTrigger>
          <TabsTrigger value="preferences">Preferenciák</TabsTrigger>
          {canSeeHr && <TabsTrigger value="hr">HR és bérszámfejtés</TabsTrigger>}
        </TabsList>

        <TabsContent value="basic" className="pt-4">
          <div className="grid gap-4 md:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Alapadatok</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="space-y-2">
                  <Label>Teljes név</Label>
                  <Input
                    value={form.fullName}
                    onChange={(e) => update({ fullName: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Megjelenítési név</Label>
                  <Input
                    value={form.displayName}
                    onChange={(e) => update({ displayName: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Szakmai szerepkör</Label>
                  <Select
                    value={form.professionalRole}
                    onValueChange={(v) => update({ professionalRole: v as ProfessionalRole })}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {PROF_ROLES.map((r) => (
                        <SelectItem key={r} value={r}>
                          {professionalRoleLabel(r)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Beosztás beállítások</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <SwitchRow
                  label="Aktív"
                  checked={form.active}
                  onChange={(v) => update({ active: v })}
                />
                <SwitchRow
                  label="Beosztható"
                  checked={form.schedulable}
                  onChange={(v) => update({ schedulable: v })}
                />
                <SwitchRow
                  label="Automatikus kitöltésbe bevonható"
                  checked={form.includeInAutoFill}
                  onChange={(v) => update({ includeInAutoFill: v })}
                />
                <SwitchRow
                  label="Gyógyszerésznek számít"
                  checked={form.countsAsPharmacist}
                  onChange={(v) => update({ countsAsPharmacist: v })}
                />
                <div className="space-y-2">
                  <Label>Havi óra keret</Label>
                  <Input
                    type="number"
                    value={form.monthlyHoursTarget}
                    onChange={(e) => update({ monthlyHoursTarget: Number(e.target.value) })}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Max napi munkaidő</Label>
                  <div className="grid grid-cols-2 gap-2">
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        min={0}
                        max={24}
                        value={hoursOfMax}
                        onChange={(e) =>
                          update({ maxDailyMinutes: Number(e.target.value) * 60 + minsOfMax })
                        }
                      />
                      <span className="text-sm text-muted-foreground">óra</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Input
                        type="number"
                        min={0}
                        max={59}
                        value={minsOfMax}
                        onChange={(e) =>
                          update({ maxDailyMinutes: hoursOfMax * 60 + Number(e.target.value) })
                        }
                      />
                      <span className="text-sm text-muted-foreground">perc</span>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Telephelyek</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {(locations.data ?? []).map((l) => (
                  <label key={l.id} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={form.locationIds.includes(l.id)}
                      onCheckedChange={(v) =>
                        update({
                          locationIds: v
                            ? [...form.locationIds, l.id]
                            : form.locationIds.filter((x) => x !== l.id),
                        })
                      }
                    />
                    {l.name}
                    {!l.active && <span className="text-xs text-muted-foreground">(inaktív)</span>}
                  </label>
                ))}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Engedélyezett munkaidőtípusok</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {SHIFT_TYPES.map((st) => (
                  <label key={st.id} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={form.allowedShiftTypes.includes(st.id)}
                      onCheckedChange={(v) => toggleShiftType(st.id, Boolean(v))}
                    />
                    {st.label}
                  </label>
                ))}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Belépési fiók</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                {form.linkedUser ? (
                  <>
                    <p>
                      <span className="text-muted-foreground">Email: </span>
                      <span className="font-medium">{form.linkedUser.email}</span>
                    </p>
                    <p>
                      <span className="text-muted-foreground">Név: </span>
                      {form.linkedUser.displayName}
                    </p>
                    <p>
                      <span className="text-muted-foreground">Állapot: </span>
                      {form.linkedUser.active ? "aktív" : "inaktív"}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      A jogosultságokat a Felhasználók menüben kezelheted.
                    </p>
                  </>
                ) : (
                  <p className="text-muted-foreground">
                    Ehhez a dolgozóhoz nincs belépési fiók. A Felhasználók menüben hozhatsz létre
                    egyet, és a dolgozói kapcsolat mezőben kösd hozzá.
                  </p>
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="capabilities" className="pt-4">
          <CapabilitiesTab employeeId={id} />
        </TabsContent>

        <TabsContent value="workProfile" className="pt-4">
          <WorkProfileTab employeeId={id} />
        </TabsContent>

        <TabsContent value="recurring" className="pt-4">
          <WorkPreferencesCard mode="admin" employeeId={id} />
        </TabsContent>

        <TabsContent value="quotas" className="pt-4">
          <QuotasTab employeeId={id} />
        </TabsContent>

        <TabsContent value="preferences" className="pt-4">
          <div className="grid gap-4 md:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Preferált időszakok</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {form.preferredWindows.map((r, i) => (
                  <WindowRow
                    key={i}
                    window={r}
                    onChange={(p) => updateWin("preferredWindows", i, p)}
                    onRemove={() => removeWin("preferredWindows", i)}
                  />
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => addWin("preferredWindows")}
                >
                  <Plus className="h-4 w-4 mr-1" />
                  Hozzáadás
                </Button>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Tiltott időszakok</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {form.blockedWindows.map((r, i) => (
                  <WindowRow
                    key={i}
                    window={r}
                    onChange={(p) => updateWin("blockedWindows", i, p)}
                    onRemove={() => removeWin("blockedWindows", i)}
                  />
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => addWin("blockedWindows")}
                >
                  <Plus className="h-4 w-4 mr-1" />
                  Hozzáadás
                </Button>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {canSeeHr && (
          <TabsContent value="hr" className="pt-4">
            <PayrollHrTab employeeId={id} />
          </TabsContent>
        )}
      </Tabs>
    </form>
  );
}

function MinutesField({
  label,
  value,
  onChange,
  hint,
  disabled = false,
  error,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
  hint?: string;
  disabled?: boolean;
  error?: string;
}) {
  const { hours, minutes } = splitMinutes(value);
  return (
    <div className={`space-y-2 ${disabled ? "opacity-50" : ""}`}>
      <Label>{label}</Label>
      <div className="flex items-center gap-2">
        <Input
          type="number"
          min={0}
          className="w-20"
          value={hours}
          disabled={disabled}
          aria-invalid={!!error}
          onChange={(e) => onChange(hoursAndMinutesToMinutes(Number(e.target.value), minutes))}
        />
        <span className="text-sm text-muted-foreground">ó</span>
        <Input
          type="number"
          min={0}
          max={59}
          className="w-20"
          value={minutes}
          disabled={disabled}
          aria-invalid={!!error}
          onChange={(e) => onChange(hoursAndMinutesToMinutes(hours, Number(e.target.value)))}
        />
        <span className="text-sm text-muted-foreground">p</span>
        <span className="text-xs text-muted-foreground ml-2">≈ {minutesToHuman(value)}</span>
      </div>
      {error && <p className="text-xs text-destructive">{error}</p>}
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

function SwitchRow({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <div className="flex items-center justify-between">
      <Label>{label}</Label>
      <Switch checked={checked} onCheckedChange={onChange} />
    </div>
  );
}

function WindowRow({
  window,
  onChange,
  onRemove,
}: {
  window: PreferenceWindow;
  onChange: (p: Partial<PreferenceWindow>) => void;
  onRemove: () => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <Select value={window.weekday} onValueChange={(v) => onChange({ weekday: v as Weekday })}>
        <SelectTrigger className="w-40">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {WEEKDAYS.map((w) => (
            <SelectItem key={w} value={w}>
              {weekdayLabel(w)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Input
        type="time"
        step={60}
        value={window.start}
        onChange={(e) => onChange({ start: e.target.value })}
        className="w-32"
      />
      <span className="text-muted-foreground">–</span>
      <Input
        type="time"
        step={60}
        value={window.end}
        onChange={(e) => onChange({ end: e.target.value })}
        className="w-32"
      />
      <Button type="button" variant="ghost" size="icon" onClick={onRemove}>
        <X className="h-4 w-4" />
      </Button>
    </div>
  );
}

// ─── Phase 2E — API-alapú planning tab-ek ──────────────────────────

const DIMENSIONS: ApiShiftQuotaDimension[] = [
  "MorningShift",
  "AfternoonShift",
  "EveningShift",
  "LongShift",
  "SaturdayShift",
  "SundayShift",
  "OnCallDuty",
  "Standby",
];
const DIMENSION_LABEL: Record<ApiShiftQuotaDimension, string> = {
  MorningShift: "Délelőtti",
  AfternoonShift: "Délutáni",
  EveningShift: "Esti",
  LongShift: "Hosszú",
  SaturdayShift: "Szombati",
  SundayShift: "Vasárnapi",
  OnCallDuty: "Ügyelet",
  Standby: "Készenlét",
};
const PERIODS: ApiQuotaPeriod[] = ["Week", "Month"];
const PERIOD_LABEL: Record<ApiQuotaPeriod, string> = { Week: "Heti", Month: "Havi" };
const SEVERITIES: ApiQuotaSeverity[] = ["Preferred", "Required"];
const SEVERITY_LABEL: Record<ApiQuotaSeverity, string> = {
  Preferred: "Javasolt",
  Required: "Kötelező",
};

function emptyWorkProfile(): EmployeeWorkProfile {
  return {
    id: null,
    version: null,
    contractedMonthlyMinutes: 168 * 60,
    contractedWeeklyMinutes: null,
    standardShiftMinutes: 8 * 60,
    minimumShiftMinutes: 4 * 60,
    maximumRegularShiftMinutes: 10 * 60,
    maximumDailyMinutes: 12 * 60,
    allowsLongShift: false,
    maximumLongShiftMinutes: null,
    allowsFullOpeningHoursShift: false,
    allowsOvertime: false,
    maximumOvertimeMinutesPerMonth: null,
    allowsOnCallDuty: false,
    maximumOnCallAssignmentsPerMonth: null,
    allowsStandby: false,
    maximumStandbyAssignmentsPerMonth: null,
    allowsSaturday: false,
    maximumSaturdaysPerMonth: null,
    allowsSunday: false,
    maximumSundaysPerMonth: null,
    includeInAutoFill: true,
  };
}

function CapabilitiesTab({ employeeId }: { employeeId: string }) {
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ["employee-capabilities", employeeId],
    queryFn: () => services.employee.getCapabilities(employeeId),
  });
  const [selected, setSelected] = useState<StaffingCapability[] | null>(null);
  useEffect(() => {
    if (q.data && selected === null) setSelected([...q.data.assignedCapabilities]);
  }, [q.data, selected]);

  const save = useMutation({
    mutationFn: async () => {
      if (!q.data || !selected) throw new Error("Nincs betöltött adat.");
      return services.employee.updateCapabilities(employeeId, selected, q.data.employeeVersion);
    },
    onSuccess: (data) => {
      toast.success("Kompetenciák mentve");
      setSelected([...data.assignedCapabilities]);
      qc.setQueryData(["employee-capabilities", employeeId], data);
      qc.invalidateQueries({ queryKey: ["employee", employeeId] });
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : "Mentés sikertelen"),
  });

  if (q.isLoading || !q.data || !selected) return <LoadingState />;
  const effective = q.data.effectiveCapabilities;
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center justify-between">
          Kompetenciák
          <Button type="button" size="sm" onClick={() => save.mutate()} disabled={save.isPending}>
            <Save className="h-4 w-4 mr-1" /> Mentés
          </Button>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        <p className="text-sm text-muted-foreground">
          A lefedettségi szabályok és a beosztás-generátor a kompetenciák alapján dönt. Üresen
          hagyva a szakmai szerepkörből származó alapkészlet érvényesül (effektív kompetencia).
        </p>
        {CAPABILITIES.map((c) => {
          const checked = selected.includes(c);
          const effectiveOnly = !checked && effective.includes(c);
          return (
            <label key={c} className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={checked}
                onCheckedChange={(v) => {
                  const set = new Set<StaffingCapability>(selected);
                  if (v) set.add(c);
                  else set.delete(c);
                  setSelected([...set]);
                }}
              />
              {capabilityLabel(c)}
              {effectiveOnly && (
                <span className="text-xs text-muted-foreground">(szerepkörből)</span>
              )}
            </label>
          );
        })}
      </CardContent>
    </Card>
  );
}

function WorkProfileTab({ employeeId }: { employeeId: string }) {
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ["employee-work-profile", employeeId],
    queryFn: () => services.employee.getWorkProfile(employeeId),
  });
  const [form, setForm] = useState<EmployeeWorkProfile | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<WorkProfileField, string>>>({});
  useEffect(() => {
    if (q.isSuccess && form === null) setForm(q.data ?? emptyWorkProfile());
  }, [q.isSuccess, q.data, form]);

  const save = useMutation({
    mutationFn: async () => {
      if (!form) throw new Error("Nincs adat.");
      return services.employee.updateWorkProfile(employeeId, form);
    },
    onMutate: () => setFieldErrors({}),
    onSuccess: async (data) => {
      setForm(data);
      const refreshed = await refetchEmployeeWorkProfile(qc, employeeId, () =>
        services.employee.getWorkProfile(employeeId),
      );
      setForm(refreshed ?? data);
      toast.success("Munkaidőprofil mentve");
    },
    onError: async (err) => {
      const errors = getWorkProfileFieldErrors(err);
      setFieldErrors(errors);
      if (err instanceof ApiError && err.code === "CONFLICT") {
        const refreshed = await refetchEmployeeWorkProfile(qc, employeeId, () =>
          services.employee.getWorkProfile(employeeId),
        );
        if (refreshed) setForm(refreshed);
        toast.error("A munkaidőprofil közben frissült. Az adatokat újratöltöttük.");
        return;
      }
      toast.error(
        Object.keys(errors).length > 0
          ? "Ellenőrizd a megjelölt mezőket."
          : "A munkaidőprofil mentése nem sikerült.",
      );
    },
  });

  if (q.isLoading || !form) return <LoadingState />;
  const set = (patch: Partial<EmployeeWorkProfile>) => {
    setForm({ ...form, ...patch });
    setFieldErrors((current) => {
      const next = { ...current };
      for (const field of Object.keys(patch)) {
        delete next[field as WorkProfileField];
      }
      return next;
    });
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          {form.id
            ? `Verzió: ${form.version}`
            : "Új profil — az első mentés hozza létre a szerveren."}
        </p>
        <Button type="button" size="sm" onClick={() => save.mutate()} disabled={save.isPending}>
          <Save className="h-4 w-4 mr-1" /> Mentés
        </Button>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Szerződéses idő</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <MinutesField
              label="Havi szerződéses idő"
              value={form.contractedMonthlyMinutes}
              error={fieldErrors.contractedMonthlyMinutes}
              onChange={(v) => set({ contractedMonthlyMinutes: v })}
            />
            <MinutesField
              label="Heti szerződéses idő (opcionális)"
              value={form.contractedWeeklyMinutes ?? 0}
              error={fieldErrors.contractedWeeklyMinutes}
              onChange={(v) => set({ contractedWeeklyMinutes: v > 0 ? v : null })}
            />
            <SwitchRow
              label="Automatikus beosztásba bevonható"
              checked={form.includeInAutoFill}
              onChange={(v) => set({ includeInAutoFill: v })}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Műszakhossz</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <MinutesField
              label="Standard műszak"
              value={form.standardShiftMinutes}
              error={fieldErrors.standardShiftMinutes}
              onChange={(v) => set({ standardShiftMinutes: v })}
            />
            <MinutesField
              label="Minimum műszak"
              value={form.minimumShiftMinutes}
              error={fieldErrors.minimumShiftMinutes}
              onChange={(v) => set({ minimumShiftMinutes: v })}
            />
            <MinutesField
              label="Maximum normál műszak"
              value={form.maximumRegularShiftMinutes}
              error={fieldErrors.maximumRegularShiftMinutes}
              onChange={(v) => set({ maximumRegularShiftMinutes: v })}
            />
            <MinutesField
              label="Napi teljes maximum"
              value={form.maximumDailyMinutes}
              error={fieldErrors.maximumDailyMinutes}
              onChange={(v) => set({ maximumDailyMinutes: v })}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Hosszú és egésznapos műszak</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <SwitchRow
              label="Hosszú műszak engedélyezett"
              checked={form.allowsLongShift}
              onChange={(v) => {
                setForm(setLongShiftAllowed(form, v));
                setFieldErrors((current) => ({
                  ...current,
                  maximumLongShiftMinutes: undefined,
                }));
              }}
            />
            <MinutesField
              label="Hosszú műszak maximum"
              value={form.maximumLongShiftMinutes ?? 0}
              disabled={!form.allowsLongShift}
              error={fieldErrors.maximumLongShiftMinutes}
              onChange={(v) => {
                set({ maximumLongShiftMinutes: v });
                setFieldErrors((current) => ({
                  ...current,
                  maximumLongShiftMinutes: undefined,
                }));
              }}
            />
            <SwitchRow
              label="Teljes nyitvatartás lefedhető"
              checked={form.allowsFullOpeningHoursShift}
              onChange={(v) => set({ allowsFullOpeningHoursShift: v })}
            />
            <SwitchRow
              label="Túlóra engedélyezett"
              checked={form.allowsOvertime}
              onChange={(v) =>
                set({
                  allowsOvertime: v,
                  maximumOvertimeMinutesPerMonth: v ? form.maximumOvertimeMinutesPerMonth : null,
                })
              }
            />
            {form.allowsOvertime && (
              <MinutesField
                label="Havi túlóra maximum"
                value={form.maximumOvertimeMinutesPerMonth ?? 0}
                error={fieldErrors.maximumOvertimeMinutesPerMonth}
                onChange={(v) => set({ maximumOvertimeMinutesPerMonth: v > 0 ? v : null })}
              />
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Elérhetőség</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <SwitchRow
              label="Ügyeletet vállal"
              checked={form.allowsOnCallDuty}
              onChange={(v) => set({ allowsOnCallDuty: v })}
            />
            {form.allowsOnCallDuty && (
              <CountField
                label="Havi ügyelet maximum (alkalom)"
                value={form.maximumOnCallAssignmentsPerMonth}
                error={fieldErrors.maximumOnCallAssignmentsPerMonth}
                onChange={(v) => set({ maximumOnCallAssignmentsPerMonth: v })}
              />
            )}
            <SwitchRow
              label="Készenlétet vállal"
              checked={form.allowsStandby}
              onChange={(v) => set({ allowsStandby: v })}
            />
            {form.allowsStandby && (
              <CountField
                label="Havi készenlét maximum (alkalom)"
                value={form.maximumStandbyAssignmentsPerMonth}
                error={fieldErrors.maximumStandbyAssignmentsPerMonth}
                onChange={(v) => set({ maximumStandbyAssignmentsPerMonth: v })}
              />
            )}
            <SwitchRow
              label="Szombaton dolgozik"
              checked={form.allowsSaturday}
              onChange={(v) => set({ allowsSaturday: v })}
            />
            {form.allowsSaturday && (
              <CountField
                label="Havi szombat maximum"
                value={form.maximumSaturdaysPerMonth}
                error={fieldErrors.maximumSaturdaysPerMonth}
                onChange={(v) => set({ maximumSaturdaysPerMonth: v })}
              />
            )}
            <SwitchRow
              label="Vasárnap dolgozik"
              checked={form.allowsSunday}
              onChange={(v) => set({ allowsSunday: v })}
            />
            {form.allowsSunday && (
              <CountField
                label="Havi vasárnap maximum"
                value={form.maximumSundaysPerMonth}
                error={fieldErrors.maximumSundaysPerMonth}
                onChange={(v) => set({ maximumSundaysPerMonth: v })}
              />
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function CountField({
  label,
  value,
  onChange,
  error,
}: {
  label: string;
  value: number | null;
  onChange: (v: number | null) => void;
  error?: string;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <Input
        type="number"
        min={0}
        value={value ?? ""}
        aria-invalid={!!error}
        onChange={(e) => onChange(e.target.value === "" ? null : Number(e.target.value))}
      />
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}

function QuotasTab({ employeeId }: { employeeId: string }) {
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: ["employee-quotas", employeeId],
    queryFn: () => services.employee.listQuotas(employeeId),
  });
  const invalidate = () => qc.invalidateQueries({ queryKey: ["employee-quotas", employeeId] });

  const createMut = useMutation({
    mutationFn: (input: CreateShiftQuotaRuleInput) =>
      services.employee.createQuota(employeeId, input),
    onSuccess: () => {
      toast.success("Kvóta létrehozva");
      invalidate();
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : "Létrehozás sikertelen"),
  });
  const updateMut = useMutation({
    mutationFn: (v: { id: string; input: import("@/services/types").UpdateShiftQuotaRuleInput }) =>
      services.employee.updateQuota(v.id, v.input),
    onSuccess: () => {
      toast.success("Kvóta mentve");
      invalidate();
    },
    onError: (err) => {
      if (err instanceof ApiError && err.code === "CONFLICT") {
        toast.error("Konkurens módosítás — töltsd újra a kvótákat.");
        invalidate();
        return;
      }
      toast.error(err instanceof Error ? err.message : "Mentés sikertelen");
    },
  });
  const deactivateMut = useMutation({
    mutationFn: (v: { id: string; expectedVersion: number }) =>
      services.employee.deactivateQuota(v.id, v.expectedVersion),
    onSuccess: () => {
      toast.success("Kvóta deaktiválva");
      invalidate();
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : "Deaktiválás sikertelen"),
  });

  if (q.isLoading) return <LoadingState />;
  const rows = q.data ?? [];
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center justify-between">
          Kvóták
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() =>
              createMut.mutate({
                dimension: "MorningShift",
                period: "Month",
                minimum: 0,
                target: 0,
                maximum: 0,
                severity: "Preferred",
                isActive: true,
              })
            }
            disabled={createMut.isPending}
          >
            <Plus className="h-4 w-4 mr-1" /> Új kvóta
          </Button>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {rows.length === 0 && (
          <p className="text-sm text-muted-foreground italic">Nincs aktív kvóta.</p>
        )}
        {rows.map((qr) => (
          <QuotaRow
            key={qr.id}
            row={qr}
            onSave={(patch) =>
              updateMut.mutate({
                id: qr.id,
                input: {
                  dimension: patch.dimension,
                  period: patch.period,
                  minimum: patch.minimum,
                  target: patch.target,
                  maximum: patch.maximum,
                  severity: patch.severity,
                  isActive: patch.isActive,
                  expectedVersion: qr.version,
                },
              })
            }
            onDeactivate={() => deactivateMut.mutate({ id: qr.id, expectedVersion: qr.version })}
          />
        ))}
      </CardContent>
    </Card>
  );
}

function QuotaRow({
  row,
  onSave,
  onDeactivate,
}: {
  row: EmployeeShiftQuotaRule;
  onSave: (patch: EmployeeShiftQuotaRule) => void;
  onDeactivate: () => void;
}) {
  const [draft, setDraft] = useState<EmployeeShiftQuotaRule>(row);
  useEffect(() => setDraft(row), [row]);
  const set = (patch: Partial<EmployeeShiftQuotaRule>) => setDraft({ ...draft, ...patch });
  const dirty = JSON.stringify(draft) !== JSON.stringify(row);
  return (
    <div className="rounded-md border p-3 flex flex-wrap items-center gap-2">
      <Select
        value={draft.dimension}
        onValueChange={(v) => set({ dimension: v as ApiShiftQuotaDimension })}
      >
        <SelectTrigger className="w-40">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {DIMENSIONS.map((d) => (
            <SelectItem key={d} value={d}>
              {DIMENSION_LABEL[d]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={draft.period} onValueChange={(v) => set({ period: v as ApiQuotaPeriod })}>
        <SelectTrigger className="w-28">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {PERIODS.map((p) => (
            <SelectItem key={p} value={p}>
              {PERIOD_LABEL[p]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <NumberCell label="Min" value={draft.minimum} onChange={(v) => set({ minimum: v })} />
      <NumberCell label="Cél" value={draft.target} onChange={(v) => set({ target: v })} />
      <NumberCell label="Max" value={draft.maximum} onChange={(v) => set({ maximum: v })} />
      <Select
        value={draft.severity}
        onValueChange={(v) => set({ severity: v as ApiQuotaSeverity })}
      >
        <SelectTrigger className="w-32">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {SEVERITIES.map((s) => (
            <SelectItem key={s} value={s}>
              {SEVERITY_LABEL[s]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => onSave(draft)}
        disabled={!dirty}
      >
        Mentés
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="ml-auto"
        onClick={onDeactivate}
        title="Deaktiválás"
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  );
}

function NumberCell({
  label,
  value,
  onChange,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
}) {
  return (
    <div className="flex items-center gap-1">
      <Label className="text-xs">{label}</Label>
      <Input
        type="number"
        min={0}
        className="w-20"
        value={value}
        onChange={(e) => onChange(Number(e.target.value) || 0)}
      />
    </div>
  );
}
