import { createFileRoute } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMyEmployeeId } from "@/hooks/use-auth";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
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
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { LoadingState, EmptyState } from "@/components/common/states";
import { StatusBadge } from "@/components/common/StatusBadge";
import { fmtDate, fmtRelative, leaveTypeLabel, leaveActionLabel } from "@/lib/format";
import type { LeaveStatus, LeaveType } from "@/services/types";
import { toast } from "sonner";

export const Route = createFileRoute("/app/requests")({
  head: () => ({ meta: [{ title: "Kérelmeim — Patika Beosztás" }] }),
  component: RequestsPage,
});

function RequestsPage() {
  const denied = useRequirePermission(["ManageOwnLeaveRequests", "ManageAllLeaveRequests"]);
  const employeeId = useMyEmployeeId();
  const qc = useQueryClient();
  const [status, setStatus] = useState<LeaveStatus | "all">("all");
  const [open, setOpen] = useState(false);
  const [reportOpen, setReportOpen] = useState(false);
  const [cancelReq, setCancelReq] = useState<{ id: string; version?: number } | null>(null);

  const requests = useQuery({
    enabled: !!employeeId,
    queryKey: ["myRequests", employeeId, status],
    queryFn: () => services.leaveRequest.listMyRequests(status === "all" ? undefined : { status }),
  });

  const cancelMut = useMutation({
    mutationFn: (r: { id: string; version?: number }) =>
      services.leaveRequest.withdrawMyRequest(r.id, r.version),
    onSuccess: () => {
      toast.success("Kérelem visszavonva");
      qc.invalidateQueries({ queryKey: ["myRequests"] });
      setCancelReq(null);
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Hiba történt."),
  });

  if (denied) return denied;

  if (!employeeId) {
    return (
      <div>
        <PageHeader title="Kérelmeim" description="Nincs dolgozói profil a fiókodhoz kapcsolva." />
        <EmptyState
          title="Nem elérhető"
          description="A saját kérelmek felülete csak akkor működik, ha az adminod hozzárendeli a fiókodat egy Employee rekordhoz."
        />
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="Kérelmeim"
        description="Szabadság-igénylés és betegállomány-bejelentés."
        action={
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setReportOpen(true)}>
              Betegállomány bejelentése
            </Button>
            <Button onClick={() => setOpen(true)}>Új igénylés</Button>
          </div>
        }
      />

      <div className="mb-4 max-w-xs">
        <Select value={status} onValueChange={(v) => setStatus(v as LeaveStatus | "all")}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Összes állapot</SelectItem>
            <SelectItem value="draft">Piszkozat</SelectItem>
            <SelectItem value="pending">Függőben</SelectItem>
            <SelectItem value="approved">Jóváhagyva</SelectItem>
            <SelectItem value="rejected">Elutasítva</SelectItem>
            <SelectItem value="withdrawn">Visszavonva</SelectItem>
            <SelectItem value="reported">Bejelentve</SelectItem>
            <SelectItem value="closed">Lezárva</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {requests.isLoading && <LoadingState />}
      {!requests.isLoading && (requests.data ?? []).length === 0 && (
        <EmptyState
          title="Nincs kérelem"
          description="Ebben a szűrésben nincs kérelmed."
          action={<Button onClick={() => setOpen(true)}>Új kérelem</Button>}
        />
      )}

      <div className="space-y-2">
        {(requests.data ?? []).map((r) => (
          <Card key={r.id}>
            <CardContent className="p-4">
              <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-3 items-start">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="font-semibold">{leaveTypeLabel(r.type)}</p>
                    <StatusBadge status={r.status} />
                  </div>
                  <p className="text-sm text-muted-foreground mt-1">
                    {fmtDate(r.startDate)}
                    {r.startDate !== r.endDate && ` – ${fmtDate(r.endDate)}`}
                    {!r.fullDay && r.startTime && ` · ${r.startTime}–${r.endTime}`}
                  </p>
                  {r.note && <p className="text-sm mt-2">{r.note}</p>}
                  {r.decisionNote && (
                    <p className="text-xs text-muted-foreground mt-1">Indoklás: {r.decisionNote}</p>
                  )}
                  <details className="mt-2">
                    <summary className="text-xs text-muted-foreground cursor-pointer">
                      Előzmények ({r.history.length})
                    </summary>
                    <ul className="text-xs text-muted-foreground mt-1 space-y-0.5">
                      {r.history.map((h, i) => (
                        <li key={i}>
                          {fmtRelative(h.at)} — {leaveActionLabel(h.action)}
                          {h.note ? `: ${h.note}` : ""}
                        </li>
                      ))}
                    </ul>
                  </details>
                </div>
                {r.status === "pending" && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setCancelReq({ id: r.id, version: r.version })}
                  >
                    Visszavonás
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <RequestDialog open={open} onOpenChange={setOpen} mode="request" />
      <RequestDialog open={reportOpen} onOpenChange={setReportOpen} mode="sick" />

      <AlertDialog open={cancelReq !== null} onOpenChange={(o) => !o && setCancelReq(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Biztosan visszavonod?</AlertDialogTitle>
            <AlertDialogDescription>
              A kérelem visszavonása után újat kell beadnod, ha mégis szeretnéd.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Mégse</AlertDialogCancel>
            <AlertDialogAction onClick={() => cancelReq && cancelMut.mutate(cancelReq)}>
              Visszavonás
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

// ─── Zod séma cross-field validációval (dátum/idő sorrend) ─────────
export const leaveRequestSchema = z
  .object({
    type: z.enum(["annual_leave", "sick_leave", "unpaid_leave", "parental_leave", "other"]),
    fullDay: z.boolean(),
    startDate: z.string().min(1, "Kezdő dátum kötelező."),
    endDate: z.string().optional().or(z.literal("")),
    startTime: z.string().optional(),
    endTime: z.string().optional(),
    note: z.string().max(500, "Legfeljebb 500 karakter.").optional().or(z.literal("")),
  })
  .superRefine((v, ctx) => {
    const end = v.endDate && v.endDate.length > 0 ? v.endDate : v.startDate;
    if (end < v.startDate) {
      ctx.addIssue({
        code: "custom",
        path: ["endDate"],
        message: "A záró dátum nem lehet korábbi a kezdésnél.",
      });
    }
    if (!v.fullDay) {
      if (!v.startTime)
        ctx.addIssue({ code: "custom", path: ["startTime"], message: "Kezdési idő kötelező." });
      if (!v.endTime)
        ctx.addIssue({ code: "custom", path: ["endTime"], message: "Záró idő kötelező." });
      if (v.startTime && v.endTime && v.endTime <= v.startTime) {
        ctx.addIssue({ code: "custom", path: ["endTime"], message: "A záró idő későbbi legyen." });
      }
    }
  });

type LeaveFormValues = z.infer<typeof leaveRequestSchema>;

function RequestDialog({
  open,
  onOpenChange,
  mode,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  mode: "request" | "sick";
}) {
  const qc = useQueryClient();
  const isSick = mode === "sick";

  const form = useForm<LeaveFormValues>({
    resolver: zodResolver(leaveRequestSchema),
    defaultValues: {
      type: isSick ? "sick_leave" : "annual_leave",
      fullDay: true,
      startDate: "",
      endDate: "",
      startTime: "08:00",
      endTime: "16:00",
      note: "",
    },
  });

  const createMut = useMutation({
    mutationFn: (v: LeaveFormValues) =>
      services.leaveRequest.createMyRequest({
        type: v.type as LeaveType,
        fullDay: v.fullDay,
        startDate: v.startDate,
        endDate: v.endDate ? v.endDate : undefined,
        startTime: v.fullDay ? undefined : v.startTime,
        endTime: v.fullDay ? undefined : v.endTime,
        note: v.note ? v.note : undefined,
      }),
    onSuccess: () => {
      toast.success(isSick ? "Betegállomány bejelentve" : "Igénylés beadva");
      qc.invalidateQueries({ queryKey: ["myRequests"] });
      qc.invalidateQueries({ queryKey: ["pendingApprovals"] });
      onOpenChange(false);
      form.reset();
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Hiba történt."),
  });

  const fullDay = form.watch("fullDay");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>
            {isSick ? "Betegállomány bejelentése" : "Új szabadság-igénylés"}
          </DialogTitle>
          <p className="text-xs text-muted-foreground pt-1">
            {isSick
              ? "Bejelentés — az admin jóváhagyás nélkül azonnal rögzítésre kerül. Diagnózist ne írj be."
              : "Igénylés — jóváhagyásra kerül."}
          </p>
        </DialogHeader>
        <form onSubmit={form.handleSubmit((v) => createMut.mutate(v))} className="space-y-3">
          {!isSick && (
            <div className="space-y-2">
              <Label>Típus</Label>
              <Controller
                control={form.control}
                name="type"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="annual_leave">Szabadság</SelectItem>
                      <SelectItem value="unpaid_leave">Fizetés nélküli szabadság</SelectItem>
                      <SelectItem value="parental_leave">Szülési/szülői szabadság</SelectItem>
                      <SelectItem value="other">Egyéb</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          )}
          <div className="flex items-center justify-between rounded-md border p-3">
            <Label htmlFor="fullday" className="cursor-pointer">
              Egész napos
            </Label>
            <Controller
              control={form.control}
              name="fullDay"
              render={({ field }) => (
                <Switch id="fullday" checked={field.value} onCheckedChange={field.onChange} />
              )}
            />
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1">
              <Label>Kezdés</Label>
              <Input type="date" {...form.register("startDate")} />
              {form.formState.errors.startDate && (
                <p className="text-xs text-destructive">
                  {form.formState.errors.startDate.message}
                </p>
              )}
            </div>
            <div className="space-y-1">
              <Label>
                Vége {isSick && <span className="text-xs text-muted-foreground">(opcionális)</span>}
              </Label>
              <Input type="date" {...form.register("endDate")} />
              {form.formState.errors.endDate && (
                <p className="text-xs text-destructive">{form.formState.errors.endDate.message}</p>
              )}
            </div>
          </div>
          {!fullDay && (
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label>Kezdés idő</Label>
                <Input type="time" {...form.register("startTime")} />
                {form.formState.errors.startTime && (
                  <p className="text-xs text-destructive">
                    {form.formState.errors.startTime.message}
                  </p>
                )}
              </div>
              <div className="space-y-1">
                <Label>Vége idő</Label>
                <Input type="time" {...form.register("endTime")} />
                {form.formState.errors.endTime && (
                  <p className="text-xs text-destructive">
                    {form.formState.errors.endTime.message}
                  </p>
                )}
              </div>
            </div>
          )}
          <div className="space-y-1">
            <Label>Megjegyzés</Label>
            <Textarea {...form.register("note")} placeholder="Opcionális" />
          </div>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Mégse
            </Button>
            <Button type="submit" disabled={createMut.isPending}>
              {isSick ? "Bejelentés" : "Beadás"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
