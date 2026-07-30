import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import { services } from "@/services";
import type { DeclarationRequirementStatus, TaxDeclarationRequirement } from "@/services/types";
import { declarationStatusLabel, declarationTypeLabel } from "@/lib/payroll-labels";

const STATUSES: DeclarationRequirementStatus[] = [
  "NotRequired",
  "Required",
  "ToSend",
  "Sent",
  "ReceivedOnya",
  "ReceivedPaper",
  "Verified",
  "Applied",
  "Rejected",
  "Expired",
];

interface Props {
  employeeId: string;
  requirements: TaxDeclarationRequirement[];
  canEdit: boolean;
}

export function DeclarationRequirementsList({ employeeId, requirements, canEdit }: Props) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Adónyilatkozat státuszok</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {requirements.length === 0 && (
          <p className="text-sm text-muted-foreground">
            Nincs generált nyilatkozat — először töltsd ki a kérdőívet.
          </p>
        )}
        {requirements.map((r) => (
          <RequirementRow key={r.id} req={r} employeeId={employeeId} canEdit={canEdit} />
        ))}
      </CardContent>
    </Card>
  );
}

function RequirementRow({
  req,
  employeeId,
  canEdit,
}: {
  req: TaxDeclarationRequirement;
  employeeId: string;
  canEdit: boolean;
}) {
  const qc = useQueryClient();
  const [expanded, setExpanded] = useState(false);
  const [status, setStatus] = useState<DeclarationRequirementStatus>(req.status);
  const [effectiveTo, setEffectiveTo] = useState(req.effectiveTo ?? "");
  const [notes, setNotes] = useState(req.notes ?? "");
  const [overrideReason, setOverrideReason] = useState("");

  const invalidate = () => qc.invalidateQueries({ queryKey: ["payroll-summary", employeeId] });

  const statusMut = useMutation({
    mutationFn: async () =>
      services.payroll.updateDeclarationStatus(req.id, {
        status,
        effectiveTo: effectiveTo || null,
        notes: notes.trim() ? notes.trim() : null,
        expectedVersion: req.version,
      }),
    onSuccess: () => {
      toast.success("Státusz frissítve.");
      invalidate();
    },
    onError: (e) => toast.error((e as Error).message),
  });

  const overrideMut = useMutation({
    mutationFn: async () => {
      if (!overrideReason.trim()) throw new Error("A felülbírálás indoklása kötelező.");
      return services.payroll.overrideDeclaration(req.id, {
        requiredDecision: !req.requiredDecision,
        status,
        reason: overrideReason.trim(),
        effectiveTo: effectiveTo || null,
        expectedVersion: req.version,
      });
    },
    onSuccess: () => {
      toast.success("Felülbírálva.");
      setOverrideReason("");
      invalidate();
    },
    onError: (e) => toast.error((e as Error).message),
  });

  return (
    <div className="rounded-md border p-3 space-y-2">
      <div className="flex items-start justify-between gap-2 flex-wrap">
        <div>
          <p className="font-medium">{declarationTypeLabel(req.type)}</p>
          <p className="text-xs text-muted-foreground">
            Érvényes: {req.effectiveFrom}
            {req.effectiveTo ? ` – ${req.effectiveTo}` : ""}
            {req.manualOverride && " · Felülbírált"}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant={req.requiredDecision ? "default" : "outline"}>
            {req.requiredDecision ? "Szükséges" : "Nem szükséges"}
          </Badge>
          <Badge variant="secondary">{declarationStatusLabel(req.status)}</Badge>
        </div>
      </div>
      {canEdit && (
        <>
          <Button type="button" variant="ghost" size="sm" onClick={() => setExpanded((v) => !v)}>
            {expanded ? "Bezárás" : "Kezelés"}
          </Button>
          {expanded && (
            <div className="grid gap-3 md:grid-cols-2 pt-2 border-t">
              <div className="space-y-2">
                <Label>Státusz</Label>
                <Select
                  value={status}
                  onValueChange={(v) => setStatus(v as DeclarationRequirementStatus)}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STATUSES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {declarationStatusLabel(s)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Érvényesség vége</Label>
                <Input
                  type="date"
                  value={effectiveTo}
                  onChange={(e) => setEffectiveTo(e.target.value)}
                />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Megjegyzés</Label>
                <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
              </div>
              <div className="flex gap-2 md:col-span-2 flex-wrap">
                <Button onClick={() => statusMut.mutate()} disabled={statusMut.isPending} size="sm">
                  Státusz mentése
                </Button>
              </div>
              <div className="space-y-2 md:col-span-2 pt-2 border-t">
                <Label>Kötelezettség felülbírálása — indoklás</Label>
                <Textarea
                  rows={2}
                  value={overrideReason}
                  onChange={(e) => setOverrideReason(e.target.value)}
                  placeholder="Miért írod felül a rendszer által számított követelményt?"
                />
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => overrideMut.mutate()}
                  disabled={overrideMut.isPending || !overrideReason.trim()}
                >
                  {req.requiredDecision ? "Nem szükségesnek jelölés" : "Szükségesnek jelölés"}
                </Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
