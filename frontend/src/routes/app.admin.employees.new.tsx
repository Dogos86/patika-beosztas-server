import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { services } from "@/services";
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
import { toast } from "sonner";
import type { AppPermission, Employee, ProfessionalRole } from "@/services/types";
import { professionalRoleLabel } from "@/lib/format";
import { useRequirePermission } from "@/components/common/PermissionGate";

export const Route = createFileRoute("/app/admin/employees/new")({
  head: () => ({ meta: [{ title: "Új dolgozó — Patika Beosztás" }] }),
  component: NewEmployeePage,
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

const DEFAULT_ACCOUNT_PERMISSIONS: AppPermission[] = [
  "ViewOwnSchedule",
  "ManageOwnLeaveRequests",
  "ManageWorkPreferences",
];

function emptyEmployee(): Omit<Employee, "id"> {
  return {
    fullName: "",
    displayName: "",
    professionalRole: "assistant",
    active: true,
    schedulable: true,
    includeInAutoFill: true,
    countsAsPharmacist: false,
    locationIds: [],
    monthlyHoursTarget: 168,
    maxDailyMinutes: 12 * 60,
    allowedShiftTypes: ["work"],
    preferredWindows: [],
    blockedWindows: [],
  };
}

function NewEmployeePage() {
  const denied = useRequirePermission(["ManageEmployees"]);
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [form, setForm] = useState<Omit<Employee, "id">>(emptyEmployee());
  const [createAccount, setCreateAccount] = useState(false);
  const [account, setAccount] = useState({
    email: "",
    displayName: "",
    password: "",
  });

  const createMut = useMutation({
    mutationFn: async () => {
      // Step 1 — employee létrehozása explicit `create` hívással.
      const created = await services.employee.create(form);
      // Step 2 — opcionális belépési fiók.
      if (createAccount) {
        try {
          await services.user.create({
            email: account.email.trim(),
            displayName: account.displayName.trim() || form.displayName || form.fullName,
            initialPassword: account.password,
            permissions: DEFAULT_ACCOUNT_PERMISSIONS,
            linkedEmployeeId: created.id,
          });
          toast.success("Dolgozó és belépési fiók létrehozva.");
        } catch (err) {
          toast.warning(
            "A dolgozó létrejött, a fiók nem — később a Felhasználók menüben újrapróbálható.",
            { description: err instanceof Error ? err.message : undefined },
          );
        }
      } else {
        toast.success("Dolgozó létrehozva.");
      }
      return created;
    },
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ["employees"] });
      navigate({ to: "/app/admin/employees/$id", params: { id: created.id } });
    },
    onError: (err) => {
      toast.error("Nem sikerült létrehozni a dolgozót.", {
        description: err instanceof Error ? err.message : undefined,
      });
    },
  });

  if (denied) return denied;

  const canSubmit =
    form.fullName.trim().length > 0 &&
    form.displayName.trim().length > 0 &&
    (!createAccount || (account.email.includes("@") && account.password.length >= 6));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (canSubmit) createMut.mutate();
      }}
    >
      <PageHeader
        title="Új dolgozó"
        description="Alapadatok rögzítése — a részletes beállítások (kompetenciák, munkaidőprofil, kvóták) a mentés után szerkeszthetők."
        action={
          <div className="flex gap-2">
            <Button
              type="button"
              variant="ghost"
              onClick={() => navigate({ to: "/app/admin/employees" })}
            >
              Mégse
            </Button>
            <Button type="submit" disabled={!canSubmit || createMut.isPending}>
              {createMut.isPending ? "Létrehozás…" : "Létrehozás"}
            </Button>
          </div>
        }
      />

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
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                required
              />
            </div>
            <div className="space-y-2">
              <Label>Megjelenítési név</Label>
              <Input
                value={form.displayName}
                onChange={(e) => setForm({ ...form, displayName: e.target.value })}
                required
              />
            </div>
            <div className="space-y-2">
              <Label>Szakmai szerepkör</Label>
              <Select
                value={form.professionalRole}
                onValueChange={(v) => setForm({ ...form, professionalRole: v as ProfessionalRole })}
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
            <CardTitle>Beosztási alap</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <SwitchRow
              label="Aktív"
              checked={form.active}
              onChange={(v) => setForm({ ...form, active: v })}
            />
            <SwitchRow
              label="Beosztható"
              checked={form.schedulable}
              onChange={(v) => setForm({ ...form, schedulable: v })}
            />
            <SwitchRow
              label="Automatikus kitöltésbe bevonható"
              checked={form.includeInAutoFill}
              onChange={(v) => setForm({ ...form, includeInAutoFill: v })}
            />
            <SwitchRow
              label="Gyógyszerésznek számít"
              checked={form.countsAsPharmacist}
              onChange={(v) => setForm({ ...form, countsAsPharmacist: v })}
            />
            <div className="space-y-2">
              <Label>Havi óra keret</Label>
              <Input
                type="number"
                min={0}
                value={form.monthlyHoursTarget}
                onChange={(e) => setForm({ ...form, monthlyHoursTarget: Number(e.target.value) })}
              />
            </div>
          </CardContent>
        </Card>

        <Card className="md:col-span-2">
          <CardHeader>
            <CardTitle>Belépési fiók (opcionális)</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <SwitchRow
              label="Belépési fiók létrehozása a mentéssel egyidejűleg"
              checked={createAccount}
              onChange={setCreateAccount}
            />
            {createAccount && (
              <div className="grid gap-3 md:grid-cols-3">
                <div className="space-y-2">
                  <Label>Email</Label>
                  <Input
                    type="email"
                    value={account.email}
                    onChange={(e) => setAccount({ ...account, email: e.target.value })}
                    required={createAccount}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Megjelenítési név</Label>
                  <Input
                    value={account.displayName}
                    onChange={(e) => setAccount({ ...account, displayName: e.target.value })}
                    placeholder={form.displayName || form.fullName}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Ideiglenes jelszó</Label>
                  <Input
                    type="password"
                    minLength={6}
                    value={account.password}
                    onChange={(e) => setAccount({ ...account, password: e.target.value })}
                    required={createAccount}
                  />
                </div>
                <p className="md:col-span-3 text-xs text-muted-foreground">
                  Alap jogosultságok: saját beosztás megtekintése, saját kérelmek és
                  munkaidő-preferenciák kezelése. Ha a fiók létrehozása sikertelen, a dolgozó akkor
                  is létrejön — a fiókot később újra megpróbálhatod a Felhasználók menüben.
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="md:col-span-2 border-dashed">
          <CardHeader>
            <CardTitle className="text-base">HR és bérszámfejtési adatok</CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground space-y-1">
            <p>
              Születési dátum, külső bérszámfejtési azonosító és további HR mezők ezen a felületen
              később, egy dedikált „HR" tabon lesznek szerkeszthetők — a backend az illesztést a
              következő fázisban szállítja.
            </p>
            <p>
              Ha most kellene rögzíteni, kérjük jelezd a rendszergazdának — a mostani szerződés csak
              a fenti alapadatokat kezeli.
            </p>
          </CardContent>
        </Card>
      </div>
    </form>
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
    <label className="flex items-center justify-between gap-3 text-sm">
      <span>{label}</span>
      <Switch checked={checked} onCheckedChange={onChange} />
    </label>
  );
}
