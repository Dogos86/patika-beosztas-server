import { createFileRoute } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { services } from "@/services";
import { useAuth } from "@/hooks/use-auth";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { LoadingState, EmptyState } from "@/components/common/states";
import { fmtDate, fmtRelative, leaveTypeLabel } from "@/lib/format";
import { AlertTriangle, Check, X } from "lucide-react";
import { toast } from "sonner";
import { useRequirePermission } from "@/components/common/PermissionGate";

export const Route = createFileRoute("/app/admin/approvals")({
  head: () => ({ meta: [{ title: "Jóváhagyások — Patika Beosztás" }] }),
  component: ApprovalsPage,
});

function ApprovalsPage() {
  const denied = useRequirePermission(["ApproveLeaveRequests", "ManageAllLeaveRequests"]);
  const { user } = useAuth();
  const qc = useQueryClient();
  const pending = useQuery({
    queryKey: ["pendingApprovals"],
    queryFn: () => services.adminLeaveRequest.listRequests({ status: "pending" }),
  });
  const employees = useQuery({
    queryKey: ["employees"],
    queryFn: () => services.employee.listAll(),
  });
  const [rejectId, setRejectId] = useState<string | null>(null);
  const [rejectNote, setRejectNote] = useState("");

  const approve = useMutation({
    mutationFn: (r: { id: string; version?: number }) =>
      services.adminLeaveRequest.decide(r.id, {
        action: "approve",
        expectedVersion: r.version,
      }),
    onSuccess: () => {
      toast.success("Jóváhagyva");
      qc.invalidateQueries({ queryKey: ["pendingApprovals"] });
      qc.invalidateQueries({ queryKey: ["myRequests"] });
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Hiba történt."),
  });
  const reject = useMutation({
    mutationFn: (r: { id: string; version?: number; note: string }) =>
      services.adminLeaveRequest.decide(r.id, {
        action: "reject",
        note: r.note,
        expectedVersion: r.version,
      }),
    onSuccess: () => {
      toast.success("Elutasítva");
      qc.invalidateQueries({ queryKey: ["pendingApprovals"] });
      setRejectId(null);
      setRejectNote("");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Hiba történt."),
  });

  const empName = (id: string) => employees.data?.find((e) => e.id === id)?.fullName ?? id;

  if (denied) return denied;
  return (
    <div>
      <PageHeader
        title="Jóváhagyások"
        description="Függőben lévő szabadság- és távollét-kérelmek."
      />
      {pending.isLoading && <LoadingState />}
      {!pending.isLoading && (pending.data ?? []).length === 0 && (
        <EmptyState title="Nincs függő kérelem" description="Minden jóváhagyva vagy elutasítva." />
      )}
      <div className="space-y-2">
        {(pending.data ?? []).map((r) => (
          <Card key={r.id}>
            <CardContent className="p-4">
              <div className="grid grid-cols-1 md:grid-cols-[minmax(0,1fr)_auto] gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-semibold">{empName(r.employeeId)}</p>
                    <span className="text-sm text-muted-foreground">
                      · {leaveTypeLabel(r.type)}
                    </span>
                  </div>
                  <p className="text-sm text-muted-foreground mt-1">
                    {fmtDate(r.startDate)}
                    {r.startDate !== r.endDate && ` – ${fmtDate(r.endDate)}`}
                    {!r.fullDay && ` · ${r.startTime}–${r.endTime}`}· beadva{" "}
                    {fmtRelative(r.createdAt)}
                  </p>
                  {r.note && <p className="text-sm mt-2">{r.note}</p>}
                  <div className="mt-2 rounded-md bg-amber-50 border border-amber-200 p-2 text-xs text-amber-800 flex items-start gap-2">
                    <AlertTriangle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
                    <span>Ütközés: 1 beosztott műszak érintett. Lefedettség: figyelmeztetés.</span>
                  </div>
                </div>
                <div className="flex gap-2 items-start">
                  <Button variant="outline" size="sm" onClick={() => setRejectId(r.id)}>
                    <X className="h-4 w-4 mr-1" />
                    Elutasítás
                  </Button>
                  <Button
                    size="sm"
                    onClick={() => approve.mutate({ id: r.id, version: r.version })}
                  >
                    <Check className="h-4 w-4 mr-1" />
                    Jóváhagyás
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog
        open={rejectId !== null}
        onOpenChange={(o) => {
          if (!o) {
            setRejectId(null);
            setRejectNote("");
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Elutasítás indoklása</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Indoklás</Label>
            <Textarea
              value={rejectNote}
              onChange={(e) => setRejectNote(e.target.value)}
              placeholder="A dolgozó ezt fogja látni"
            />
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setRejectId(null)}>
              Mégse
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                if (!rejectId) return;
                const req = pending.data?.find((x) => x.id === rejectId);
                reject.mutate({ id: rejectId, version: req?.version, note: rejectNote });
              }}
              disabled={!rejectNote.trim()}
            >
              Elutasítás
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
