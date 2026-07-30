import { createFileRoute, Link } from "@tanstack/react-router";
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { LoadingState } from "@/components/common/states";
import { professionalRoleLabel } from "@/lib/format";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { useState } from "react";
import { Plus, ChevronLeft, ChevronRight } from "lucide-react";

export const Route = createFileRoute("/app/admin/employees/")({
  head: () => ({ meta: [{ title: "Dolgozók — Patika Beosztás" }] }),
  component: EmployeesPage,
});

function EmployeesPage() {
  const denied = useRequirePermission(["ManageEmployees"]);
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const employees = useQuery({
    queryKey: ["employees", "paged", { search, includeInactive, page, pageSize }],
    queryFn: () => services.employee.listPaged({ search, includeInactive, page, pageSize }),
    placeholderData: keepPreviousData,
  });
  if (denied) return denied;
  const data = employees.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  return (
    <div>
      <PageHeader
        title="Dolgozók"
        description="Törzsadatok és képességek szerkesztése."
        action={
          <Button asChild>
            <Link to="/app/admin/employees/new">
              <Plus className="h-4 w-4 mr-1" />
              Új dolgozó
            </Link>
          </Button>
        }
      />
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <Input
          placeholder="Keresés név szerint…"
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
      </div>
      {employees.isLoading && <LoadingState />}
      <div className="space-y-2">
        {(data?.items ?? []).map((e) => (
          <Link key={e.id} to="/app/admin/employees/$id" params={{ id: e.id }}>
            <Card className="hover:border-primary/40 transition-colors">
              <CardContent className="p-4 flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-semibold truncate">{e.fullName}</p>
                  <p className="text-sm text-muted-foreground">
                    {professionalRoleLabel(e.professionalRole)} · {e.displayName}
                  </p>
                </div>
                <div className="flex gap-1 flex-wrap justify-end">
                  {e.linkedUser && (
                    <Badge variant="outline" className="bg-emerald-50 text-emerald-700">
                      Van fiók
                    </Badge>
                  )}
                  {!e.active && (
                    <Badge variant="outline" className="bg-slate-100">
                      Inaktív
                    </Badge>
                  )}
                  {!e.schedulable && (
                    <Badge variant="outline" className="bg-slate-100">
                      Nem beosztható
                    </Badge>
                  )}
                  {e.countsAsPharmacist && (
                    <Badge variant="secondary">Gyógyszerésznek számít</Badge>
                  )}
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
        {data && data.items.length === 0 && !employees.isLoading && (
          <p className="text-sm text-muted-foreground py-8 text-center">
            Nincs a szűrésnek megfelelő dolgozó.
          </p>
        )}
      </div>
      {data && data.total > pageSize && (
        <div className="flex items-center justify-between gap-2 mt-4">
          <p className="text-sm text-muted-foreground">
            Összesen {data.total} dolgozó · {page}. / {totalPages}. oldal
          </p>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              <ChevronLeft className="h-4 w-4" />
              Előző
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              Következő
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
