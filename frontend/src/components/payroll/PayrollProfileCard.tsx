import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { toast } from "sonner";
import { services } from "@/services";
import type { EmployeePayrollProfile, PayrollProfileStatus } from "@/services/types";
import { payrollProfileStatusLabel } from "@/lib/payroll-labels";
import { maskTaxIdCompact } from "@/lib/mask";
import { useHasAnyPermission } from "@/hooks/use-auth";

interface Props {
  employeeId: string;
  profile: EmployeePayrollProfile | null;
  canEdit: boolean;
}

const STATUSES: PayrollProfileStatus[] = ["Draft", "UnderReview", "Complete", "Archived"];

export function PayrollProfileCard({ employeeId, profile, canEdit }: Props) {
  const qc = useQueryClient();
  const canSeeSensitive = useHasAnyPermission(["ViewPayrollSensitiveData"]);
  const [form, setForm] = useState({
    employeeNumber: profile?.employeeNumber ?? "",
    taxIdentificationNumber: profile?.taxIdentificationNumber ?? "",
    employmentStartDate: profile?.employmentStartDate ?? new Date().toISOString().slice(0, 10),
    payrollExternalId: profile?.payrollExternalId ?? "",
    status: (profile?.status ?? "Draft") as PayrollProfileStatus,
  });

  useEffect(() => {
    if (profile) {
      setForm({
        employeeNumber: profile.employeeNumber,
        taxIdentificationNumber: profile.taxIdentificationNumber ?? "",
        employmentStartDate: profile.employmentStartDate,
        payrollExternalId: profile.payrollExternalId ?? "",
        status: profile.status,
      });
    }
  }, [profile]);

  const saveMut = useMutation({
    mutationFn: async () =>
      services.payroll.updateProfile(employeeId, {
        employeeNumber: form.employeeNumber.trim(),
        taxIdentificationNumber: form.taxIdentificationNumber.trim() || null,
        employmentStartDate: form.employmentStartDate,
        payrollExternalId: form.payrollExternalId.trim() || null,
        status: form.status,
        expectedVersion: profile?.version ?? null,
      }),
    onSuccess: () => {
      toast.success("Bérszámfejtési profil mentve.");
      qc.invalidateQueries({ queryKey: ["payroll-summary", employeeId] });
      qc.invalidateQueries({ queryKey: ["payroll-profile", employeeId] });
    },
    onError: (e) => toast.error("Nem sikerült menteni.", { description: (e as Error).message }),
  });

  const disabled = !canEdit || saveMut.isPending;
  const displayedTax = canSeeSensitive
    ? form.taxIdentificationNumber
    : (profile?.maskedTaxIdentificationNumber ?? maskTaxIdCompact(form.taxIdentificationNumber));

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Bérszámfejtési alapadatok</CardTitle>
        {profile && <Badge variant="outline">{payrollProfileStatusLabel(profile.status)}</Badge>}
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label>Belső dolgozói szám</Label>
            <Input
              value={form.employeeNumber}
              onChange={(e) => setForm({ ...form, employeeNumber: e.target.value })}
              disabled={disabled}
            />
          </div>
          <div className="space-y-2">
            <Label>Adóazonosító jel</Label>
            <Input
              value={canSeeSensitive ? form.taxIdentificationNumber : displayedTax}
              onChange={(e) => setForm({ ...form, taxIdentificationNumber: e.target.value })}
              disabled={disabled || !canSeeSensitive}
              placeholder={canSeeSensitive ? "10 karakter" : "csak jogosultsággal látható"}
            />
            {!canSeeSensitive && (
              <p className="text-xs text-muted-foreground">
                Az adóazonosítót csak „Bérszámfejtési érzékeny adatok" jogosultsággal láthatod.
              </p>
            )}
          </div>
          <div className="space-y-2">
            <Label>Munkaviszony kezdete</Label>
            <Input
              type="date"
              value={form.employmentStartDate}
              onChange={(e) => setForm({ ...form, employmentStartDate: e.target.value })}
              disabled={disabled}
            />
          </div>
          <div className="space-y-2">
            <Label>Bérszámfejtő rendszer azonosító</Label>
            <Input
              value={form.payrollExternalId}
              onChange={(e) => setForm({ ...form, payrollExternalId: e.target.value })}
              disabled={disabled}
            />
          </div>
          <div className="space-y-2">
            <Label>Státusz</Label>
            <Select
              value={form.status}
              onValueChange={(v) => setForm({ ...form, status: v as PayrollProfileStatus })}
              disabled={disabled}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {payrollProfileStatusLabel(s)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {profile && (
            <div className="space-y-2 text-xs text-muted-foreground pt-6">
              Verzió: {profile.version}
            </div>
          )}
        </div>
        {canEdit && (
          <div className="flex justify-end pt-2">
            <Button onClick={() => saveMut.mutate()} disabled={disabled}>
              {saveMut.isPending ? "Mentés…" : "Profil mentése"}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
