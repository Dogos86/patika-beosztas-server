import { createFileRoute } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useHasPermission } from "@/hooks/use-auth";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge } from "@/components/ui/badge";
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
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "@/components/ui/select";
import { LoadingState, EmptyState } from "@/components/common/states";
import type { AdminUserSummary, AppPermission } from "@/services/types";
import { toast } from "sonner";
import { Plus, ShieldAlert, UserCog, Link2, Power } from "lucide-react";
import { useRequirePermission } from "@/components/common/PermissionGate";

export const Route = createFileRoute("/app/admin/users")({
  head: () => ({ meta: [{ title: "Felhasználók — Patika Beosztás" }] }),
  component: UsersPage,
});

const ALL_PERMISSIONS: AppPermission[] = [
  "ViewOwnSchedule",
  "ManageOwnLeaveRequests",
  "ManageWorkPreferences",
  "ManageAllLeaveRequests",
  "ApproveLeaveRequests",
  "RecordLeaveForOthers",
  "ManageEmployees",
  "ManageLocations",
  "ManageCoverageRules",
  "ManageSchedules",
  "RunAutoFill",
  "ApproveSchedules",
  "PublishSchedules",
  "UseAiAssistant",
  "ManageUsers",
  "ManagePayrollOnboarding",
  "ViewPayrollSensitiveData",
  "ReviewTaxAllowanceSurvey",
  "ExportPayrollData",
];

const PERM_LABEL: Record<AppPermission, string> = {
  ViewOwnSchedule: "Saját beosztás megtekintése",
  ManageOwnLeaveRequests: "Saját kérelmek kezelése",
  ManageWorkPreferences: "Munkapreferenciák kezelése",
  ManageAllLeaveRequests: "Minden kérelem kezelése",
  ApproveLeaveRequests: "Kérelmek jóváhagyása",
  RecordLeaveForOthers: "Távollét rögzítése mások nevében",
  ManageEmployees: "Dolgozók kezelése",
  ManageLocations: "Telephelyek kezelése",
  ManageCoverageRules: "Lefedettségi szabályok",
  ManageSchedules: "Beosztás szerkesztése",
  RunAutoFill: "Automatikus kitöltés futtatása",
  ApproveSchedules: "Beosztás jóváhagyása",
  PublishSchedules: "Beosztás publikálása",
  UseAiAssistant: "AI asszisztens használata",
  ManageUsers: "Felhasználók kezelése",
  ManagePayrollOnboarding: "Bérszámfejtési belépés kezelése",
  ViewPayrollSensitiveData: "Bérszámfejtési adatok megtekintése",
  ReviewTaxAllowanceSurvey: "Adókedvezmény-felmérő ellenőrzése",
  ExportPayrollData: "Bérszámfejtési adatok exportja",
};

function UsersPage() {
  const denied = useRequirePermission(["ManageUsers"]);
  const canManage = useHasPermission("ManageUsers");
  const qc = useQueryClient();
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [editingPerms, setEditingPerms] = useState<AdminUserSummary | null>(null);
  const [editingLink, setEditingLink] = useState<AdminUserSummary | null>(null);
  const [toggling, setToggling] = useState<AdminUserSummary | null>(null);

  const list = useQuery({
    enabled: canManage,
    queryKey: ["users", { search, includeInactive, page }],
    queryFn: () => services.user.list({ search, includeInactive, page, pageSize: 20 }),
  });
  const employees = useQuery({
    enabled: canManage,
    queryKey: ["employees"],
    queryFn: () => services.employee.listAll(),
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ["users"] });
  const onErr = (e: unknown) => {
    const err = e as { code?: string; message?: string };
    if (err?.code === "LAST_ADMIN_REMOVAL") {
      toast.error(
        "Nem hajtható végre: legalább egy aktív, felhasználókat kezelő adminra szükség van.",
      );
    } else {
      toast.error(err?.message ?? "Ismeretlen hiba");
    }
  };

  const permsMut = useMutation({
    mutationFn: (v: { id: string; permissions: AppPermission[]; version: number }) =>
      services.user.updatePermissions(v.id, {
        permissions: v.permissions,
        expectedVersion: v.version,
      }),
    onSuccess: () => {
      toast.success("Jogosultságok frissítve");
      invalidate();
      setEditingPerms(null);
    },
    onError: onErr,
  });
  const linkMut = useMutation({
    mutationFn: (v: { id: string; linkedEmployeeId: string | null; version: number }) =>
      services.user.updateEmployeeLink(v.id, {
        linkedEmployeeId: v.linkedEmployeeId,
        expectedVersion: v.version,
      }),
    onSuccess: () => {
      toast.success("Dolgozói kapcsolat frissítve");
      invalidate();
      setEditingLink(null);
    },
    onError: onErr,
  });
  const statusMut = useMutation({
    mutationFn: (v: { id: string; active: boolean; version: number }) =>
      services.user.setStatus(v.id, { active: v.active, expectedVersion: v.version }),
    onSuccess: () => {
      toast.success("Fiók állapota frissítve");
      invalidate();
      setToggling(null);
    },
    onError: onErr,
  });

  if (!canManage) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 rounded-lg border bg-card py-16 px-4 text-center">
        <ShieldAlert className="h-10 w-10 text-destructive" />
        <h1 className="text-lg font-semibold">Nincs jogosultságod</h1>
        <p className="text-sm text-muted-foreground">
          A felhasználók kezeléséhez „ManageUsers" jogosultság kell.
        </p>
      </div>
    );
  }

  if (denied) return denied;
  return (
    <div>
      <PageHeader
        title="Felhasználók és jogosultságok"
        description="Fiókok, permissionök és dolgozói kapcsolatok kezelése."
        action={
          <Button onClick={() => setCreating(true)}>
            <Plus className="h-4 w-4 mr-1" />
            Új fiók
          </Button>
        }
      />

      <Card className="mb-4">
        <CardContent className="p-3 flex flex-wrap items-center gap-3">
          <Input
            placeholder="Keresés email vagy név szerint"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="max-w-xs"
          />
          <label className="flex items-center gap-2 text-sm">
            <Switch
              checked={includeInactive}
              onCheckedChange={(v) => {
                setIncludeInactive(v);
                setPage(1);
              }}
            />
            Inaktívak is
          </label>
          <div className="ml-auto text-xs text-muted-foreground">
            {list.data ? `${list.data.total} találat` : ""}
          </div>
        </CardContent>
      </Card>

      {list.isLoading && <LoadingState />}
      {!list.isLoading && (list.data?.items.length ?? 0) === 0 && (
        <EmptyState
          title="Nincs találat"
          description="Módosítsd a szűrést vagy hozz létre új fiókot."
        />
      )}

      <div className="space-y-2">
        {(list.data?.items ?? []).map((u) => (
          <Card key={u.id}>
            <CardContent className="p-4">
              <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-semibold truncate">{u.displayName}</p>
                    <span className="text-sm text-muted-foreground">{u.email}</span>
                    {u.active === false && (
                      <Badge variant="outline" className="bg-slate-100">
                        Inaktív
                      </Badge>
                    )}
                    {u.permissions.includes("ManageUsers") && (
                      <Badge variant="secondary">Admin</Badge>
                    )}
                    {!u.linkedEmployee && <Badge variant="outline">Nincs dolgozó</Badge>}
                  </div>
                  <p className="text-xs text-muted-foreground mt-1">
                    {u.linkedEmployee
                      ? `Dolgozó: ${u.linkedEmployee.displayName}`
                      : "Nem kapcsolódik dolgozóhoz"}
                    {" · "}Jogosultságok: {u.permissions.length}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" size="sm" onClick={() => setEditingPerms(u)}>
                    <UserCog className="h-4 w-4 mr-1" />
                    Jogok
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => setEditingLink(u)}>
                    <Link2 className="h-4 w-4 mr-1" />
                    Dolgozó
                  </Button>
                  <Button
                    variant={u.active === false ? "default" : "outline"}
                    size="sm"
                    onClick={() => setToggling(u)}
                  >
                    <Power className="h-4 w-4 mr-1" />
                    {u.active === false ? "Aktiválás" : "Deaktiválás"}
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {list.data && list.data.total > list.data.pageSize && (
        <div className="mt-4 flex items-center justify-between text-sm">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
          >
            Előző
          </Button>
          <span className="text-muted-foreground">
            {page}. oldal / {Math.max(1, Math.ceil(list.data.total / list.data.pageSize))}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page * list.data.pageSize >= list.data.total}
            onClick={() => setPage(page + 1)}
          >
            Következő
          </Button>
        </div>
      )}

      <CreateUserDialog
        open={creating}
        onOpenChange={setCreating}
        employees={employees.data ?? []}
        onCreated={invalidate}
      />

      {/* Jogosultságok szerkesztése */}
      <Dialog open={editingPerms !== null} onOpenChange={(o) => !o && setEditingPerms(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Jogosultságok — {editingPerms?.displayName}</DialogTitle>
          </DialogHeader>
          {editingPerms && (
            <PermissionEditor
              initial={editingPerms.permissions}
              onCancel={() => setEditingPerms(null)}
              onSave={(permissions) =>
                permsMut.mutate({ id: editingPerms.id, permissions, version: editingPerms.version })
              }
              pending={permsMut.isPending}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* Dolgozói kapcsolat */}
      <Dialog open={editingLink !== null} onOpenChange={(o) => !o && setEditingLink(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Dolgozói kapcsolat — {editingLink?.displayName}</DialogTitle>
          </DialogHeader>
          {editingLink && (
            <LinkEditor
              initial={editingLink.linkedEmployee?.id ?? null}
              employees={employees.data ?? []}
              onCancel={() => setEditingLink(null)}
              onSave={(linkedEmployeeId) =>
                linkMut.mutate({
                  id: editingLink.id,
                  linkedEmployeeId,
                  version: editingLink.version,
                })
              }
              pending={linkMut.isPending}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* Aktív/inaktív váltás */}
      <AlertDialog open={toggling !== null} onOpenChange={(o) => !o && setToggling(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {toggling?.active === false ? "Fiók aktiválása" : "Fiók deaktiválása"}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {toggling?.active === false
                ? "A felhasználó ismét be tud jelentkezni és használja az alkalmazást."
                : "A felhasználó nem tud belépni, de a rekordja megmarad az előzményekben."}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Mégse</AlertDialogCancel>
            <AlertDialogAction
              onClick={() =>
                toggling &&
                statusMut.mutate({
                  id: toggling.id,
                  active: !(toggling.active !== false),
                  version: toggling.version,
                })
              }
            >
              Megerősítés
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function PermissionEditor({
  initial,
  onSave,
  onCancel,
  pending,
}: {
  initial: AppPermission[];
  onSave: (p: AppPermission[]) => void;
  onCancel: () => void;
  pending: boolean;
}) {
  const [values, setValues] = useState<AppPermission[]>(initial);
  const toggle = (p: AppPermission, on: boolean) =>
    setValues((prev) => (on ? [...new Set([...prev, p])] : prev.filter((x) => x !== p)));
  return (
    <div className="space-y-3">
      <div className="grid grid-cols-1 gap-2 max-h-80 overflow-auto pr-1">
        {ALL_PERMISSIONS.map((p) => (
          <label key={p} className="flex items-start gap-2 text-sm border rounded-md p-2">
            <Checkbox checked={values.includes(p)} onCheckedChange={(v) => toggle(p, Boolean(v))} />
            <div>
              <p className="font-medium">{PERM_LABEL[p]}</p>
              <p className="text-xs text-muted-foreground">{p}</p>
            </div>
          </label>
        ))}
      </div>
      <DialogFooter>
        <Button variant="ghost" onClick={onCancel}>
          Mégse
        </Button>
        <Button onClick={() => onSave(values)} disabled={pending}>
          Mentés
        </Button>
      </DialogFooter>
    </div>
  );
}

function LinkEditor({
  initial,
  employees,
  onSave,
  onCancel,
  pending,
}: {
  initial: string | null;
  employees: { id: string; displayName: string; fullName: string }[];
  onSave: (id: string | null) => void;
  onCancel: () => void;
  pending: boolean;
}) {
  const [value, setValue] = useState<string>(initial ?? "__none");
  return (
    <div className="space-y-3">
      <Label>Kapcsolt dolgozó</Label>
      <Select value={value} onValueChange={setValue}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="__none">— Nincs kapcsolat —</SelectItem>
          {employees.map((e) => (
            <SelectItem key={e.id} value={e.id}>
              {e.fullName}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <DialogFooter>
        <Button variant="ghost" onClick={onCancel}>
          Mégse
        </Button>
        <Button onClick={() => onSave(value === "__none" ? null : value)} disabled={pending}>
          Mentés
        </Button>
      </DialogFooter>
    </div>
  );
}

function CreateUserDialog({
  open,
  onOpenChange,
  employees,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  employees: { id: string; fullName: string }[];
  onCreated: () => void;
}) {
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [linkedEmployeeId, setLinkedEmployeeId] = useState<string>("__none");
  const [permissions, setPermissions] = useState<AppPermission[]>([
    "ViewOwnSchedule",
    "ManageOwnLeaveRequests",
  ]);

  const createMut = useMutation({
    mutationFn: () =>
      services.user.create({
        email: email.trim(),
        displayName: displayName.trim(),
        initialPassword: password,
        permissions,
        linkedEmployeeId: linkedEmployeeId === "__none" ? null : linkedEmployeeId,
      }),
    onSuccess: () => {
      toast.success("Felhasználó létrehozva");
      onCreated();
      onOpenChange(false);
      setEmail("");
      setDisplayName("");
      setPassword("");
      setLinkedEmployeeId("__none");
      setPermissions(["ViewOwnSchedule", "ManageOwnLeaveRequests"]);
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Hiba"),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Új felhasználó</DialogTitle>
        </DialogHeader>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            createMut.mutate();
          }}
          className="space-y-3"
        >
          <div className="space-y-2">
            <Label>Email</Label>
            <Input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Megjelenítési név</Label>
            <Input required value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Kezdeti jelszó</Label>
            <Input
              type="text"
              required
              minLength={4}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>Kapcsolt dolgozó (opcionális)</Label>
            <Select value={linkedEmployeeId} onValueChange={setLinkedEmployeeId}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none">— Nincs —</SelectItem>
                {employees.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.fullName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Kezdeti jogosultságok</Label>
            <div className="max-h-56 overflow-auto space-y-1 border rounded-md p-2">
              {ALL_PERMISSIONS.map((p) => (
                <label key={p} className="flex items-center gap-2 text-xs">
                  <Checkbox
                    checked={permissions.includes(p)}
                    onCheckedChange={(v) =>
                      setPermissions((prev) =>
                        v ? [...new Set([...prev, p])] : prev.filter((x) => x !== p),
                      )
                    }
                  />
                  <span>{PERM_LABEL[p]}</span>
                </label>
              ))}
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Mégse
            </Button>
            <Button type="submit" disabled={createMut.isPending}>
              Létrehozás
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
