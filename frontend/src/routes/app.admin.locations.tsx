import { createFileRoute } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { LoadingState } from "@/components/common/states";
import { useState } from "react";
import type { Location } from "@/services/types";
import type { CreateLocationInput } from "@/services/interfaces";
import { toast } from "sonner";
import { Plus } from "lucide-react";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { LocationOpeningHoursTab } from "@/components/locations/LocationOpeningHoursTab";
import { ShiftTemplatesEditor } from "@/components/locations/ShiftTemplatesEditor";

export const Route = createFileRoute("/app/admin/locations")({
  head: () => ({
    meta: [
      { title: "Telephelyek — Patika Beosztás" },
      {
        name: "description",
        content: "Patikai telephelyek, heti nyitvatartás és műszaksablonok kezelése.",
      },
    ],
  }),
  component: LocationsPage,
});

const PAGE_SIZE = 20;

function emptyDraft(): CreateLocationInput {
  return { name: "", kind: "branch", address: "", active: true };
}

function LocationsPage() {
  const denied = useRequirePermission(["ManageLocations"]);
  const qc = useQueryClient();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(true);
  const [editing, setEditing] = useState<Location | null>(null);
  const [creating, setCreating] = useState<CreateLocationInput | null>(null);
  const [basic, setBasic] = useState<CreateLocationInput>(emptyDraft());

  const listQ = useQuery({
    queryKey: ["locations", { page, search, includeInactive }],
    queryFn: () =>
      services.location.listPaged({ page, pageSize: PAGE_SIZE, search, includeInactive }),
  });

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey: ["locations"] });
    void qc.invalidateQueries({ queryKey: ["locations-all"] });
  };

  const createMut = useMutation({
    mutationFn: (input: CreateLocationInput) => services.location.create(input),
    onSuccess: (loc) => {
      toast.success("Telephely létrehozva.");
      setCreating(null);
      setEditing(loc);
      setBasic({
        name: loc.name,
        kind: loc.kind,
        address: loc.address ?? "",
        active: loc.active,
      });
      invalidate();
    },
    onError: (e) =>
      toast.error("A telephely létrehozása nem sikerült.", { description: (e as Error).message }),
  });

  const updateMut = useMutation({
    mutationFn: (args: { id: string; input: CreateLocationInput; version: number }) =>
      services.location.update(args.id, args.input, args.version),
    onSuccess: (loc) => {
      toast.success("Alapadat mentve.");
      setEditing(loc);
      invalidate();
    },
    onError: (e) =>
      toast.error("Az alapadat mentése nem sikerült.", { description: (e as Error).message }),
  });

  if (denied) return denied;

  const paged = listQ.data;
  const totalPages = paged ? Math.max(1, Math.ceil(paged.total / paged.pageSize)) : 1;

  const openEdit = (l: Location) => {
    setEditing(l);
    setBasic({ name: l.name, kind: l.kind, address: l.address ?? "", active: l.active });
  };

  return (
    <div>
      <PageHeader
        title="Telephelyek"
        description="Patikai telephelyek kezelése."
        action={
          <Button onClick={() => setCreating(emptyDraft())}>
            <Plus className="h-4 w-4 mr-1" />
            Új telephely
          </Button>
        }
      />

      <div className="flex flex-wrap items-end gap-3 mb-3">
        <div className="space-y-1">
          <Label>Keresés</Label>
          <Input
            value={search}
            placeholder="Név…"
            onChange={(e) => {
              setPage(1);
              setSearch(e.target.value);
            }}
          />
        </div>
        <label className="flex items-center gap-2 text-sm">
          <Switch
            checked={includeInactive}
            onCheckedChange={(v) => {
              setPage(1);
              setIncludeInactive(v);
            }}
          />
          Inaktívak is
        </label>
      </div>

      {listQ.isLoading && <LoadingState />}
      {listQ.isError && (
        <p className="text-sm text-destructive">
          A telephelyek betöltése nem sikerült: {(listQ.error as Error).message}
        </p>
      )}
      {paged && paged.items.length === 0 && (
        <p className="text-sm text-muted-foreground italic">Nincs találat.</p>
      )}

      <div className="space-y-2">
        {(paged?.items ?? []).map((l) => (
          <Card key={l.id}>
            <CardContent className="p-4 flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="font-semibold truncate">{l.name}</p>
                <p className="text-sm text-muted-foreground">
                  {l.kind === "headquarters" ? "Központ" : "Fiók"} ·{" "}
                  {l.active ? "aktív" : "inaktív"}
                  {l.address ? ` · ${l.address}` : ""}
                </p>
              </div>
              <Button variant="outline" size="sm" onClick={() => openEdit(l)}>
                Szerkesztés
              </Button>
            </CardContent>
          </Card>
        ))}
      </div>

      {paged && paged.total > paged.pageSize && (
        <div className="flex items-center justify-between gap-2 mt-3">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Előző
          </Button>
          <span className="text-xs text-muted-foreground">
            {page} / {totalPages} oldal · {paged.total} telephely
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Következő
          </Button>
        </div>
      )}

      {/* Létrehozás — csak alapadat, utána nyílik a teljes szerkesztő. */}
      <Dialog open={creating !== null} onOpenChange={(o) => !o && setCreating(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Új telephely</DialogTitle>
          </DialogHeader>
          {creating && (
            <form
              className="space-y-4"
              onSubmit={(e) => {
                e.preventDefault();
                createMut.mutate(creating);
              }}
            >
              <BasicFields value={creating} onChange={setCreating} />
              <DialogFooter>
                <Button type="button" variant="ghost" onClick={() => setCreating(null)}>
                  Mégse
                </Button>
                <Button type="submit" disabled={createMut.isPending}>
                  Létrehozás
                </Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>

      {/* Szerkesztés — tabonként külön mentés. */}
      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editing?.name || "Telephely"}</DialogTitle>
          </DialogHeader>
          {editing && (
            <Tabs defaultValue="basic">
              <TabsList>
                <TabsTrigger value="basic">Adatlap</TabsTrigger>
                <TabsTrigger value="hours">Nyitvatartás</TabsTrigger>
                <TabsTrigger value="templates">Sablonok</TabsTrigger>
              </TabsList>
              <TabsContent value="basic" className="space-y-4 pt-3">
                <BasicFields value={basic} onChange={setBasic} />
                <div className="flex justify-end">
                  <Button
                    type="button"
                    disabled={updateMut.isPending}
                    onClick={() => {
                      if (editing.version === undefined) {
                        toast.error("Hiányzó verziószám — töltsd újra a listát.");
                        return;
                      }
                      updateMut.mutate({
                        id: editing.id,
                        input: basic,
                        version: editing.version,
                      });
                    }}
                  >
                    Alapadat mentése
                  </Button>
                </div>
              </TabsContent>
              <TabsContent value="hours" className="pt-3 max-h-[60vh] overflow-y-auto pr-1">
                <LocationOpeningHoursTab locationId={editing.id} canEdit />
              </TabsContent>
              <TabsContent value="templates" className="pt-3 max-h-[60vh] overflow-y-auto pr-1">
                <ShiftTemplatesEditor locationId={editing.id} canEdit />
              </TabsContent>
            </Tabs>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function BasicFields({
  value,
  onChange,
}: {
  value: CreateLocationInput;
  onChange: (v: CreateLocationInput) => void;
}) {
  return (
    <div className="space-y-3">
      <div className="space-y-2">
        <Label>Név</Label>
        <Input
          value={value.name}
          onChange={(e) => onChange({ ...value, name: e.target.value })}
          required
        />
      </div>
      <div className="space-y-2">
        <Label>Cím</Label>
        <Input
          value={value.address ?? ""}
          onChange={(e) => onChange({ ...value, address: e.target.value })}
        />
      </div>
      <div className="space-y-2">
        <Label>Típus</Label>
        <Select
          value={value.kind}
          onValueChange={(v) => onChange({ ...value, kind: v as Location["kind"] })}
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="headquarters">Központ</SelectItem>
            <SelectItem value="branch">Fiók</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="flex items-center justify-between">
        <Label>Aktív</Label>
        <Switch checked={value.active} onCheckedChange={(v) => onChange({ ...value, active: v })} />
      </div>
    </div>
  );
}
