import { createFileRoute, Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { LoadingState } from "@/components/common/states";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { useState } from "react";
import { mapWithConcurrency } from "@/lib/concurrency";
import type { PayrollOnboardingSummary } from "@/services/types";

export const Route = createFileRoute("/app/admin/payroll/")({
  head: () => ({
    meta: [
      { title: "Bérszámfejtési onboarding — Patika Beosztás" },
      {
        name: "description",
        content: "Dolgozónkénti bérszámfejtési onboarding készültség és adóügyi nyilatkozatok.",
      },
    ],
  }),
  component: PayrollList,
});

const PAGE_SIZE = 20;

type SummaryResult = { ok: true; summary: PayrollOnboardingSummary } | { ok: false };

function PayrollList() {
  const denied = useRequirePermission(["ManagePayrollOnboarding", "ReviewTaxAllowanceSurvey"]);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");

  // Csak a látható oldal dolgozóit kérjük le — nincs korlátlan lista.
  const empsQ = useQuery({
    queryKey: ["employees-payroll", { page, search }],
    queryFn: () => services.employee.listPaged({ page, pageSize: PAGE_SIZE, search }),
  });

  const list = empsQ.data?.items ?? [];

  // Egy query az oldalra, limitált párhuzamossággal (max 4 egyidejű kérés).
  // Soronként külön hibaállapot — egy hibás summary nem blokkolja a többit.
  const ids = list.map((e) => e.id);
  const summariesQ = useQuery({
    queryKey: ["payroll-summaries", ids],
    enabled: ids.length > 0,
    staleTime: 60_000,
    retry: 1,
    queryFn: () =>
      mapWithConcurrency<string, SummaryResult>(ids, 4, async (id) => {
        try {
          return { ok: true, summary: await services.payroll.getSummary(id) };
        } catch {
          return { ok: false };
        }
      }),
  });

  if (denied) return denied;
  if (empsQ.isLoading) return <LoadingState />;

  const total = empsQ.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Bérszámfejtési onboarding"
        description="Dolgozónkénti onboarding készültség, adóügyi kérdőívek és nyilatkozatok."
      />

      <div className="space-y-1 max-w-xs">
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

      {empsQ.isError && (
        <p className="text-sm text-destructive">
          A dolgozók betöltése nem sikerült: {(empsQ.error as Error).message}
        </p>
      )}
      {list.length === 0 && <p className="text-sm text-muted-foreground italic">Nincs találat.</p>}

      <div className="grid gap-3">
        {list.map((e, i) => {
          const result = summariesQ.data?.[i];
          const s = result?.ok ? result.summary : undefined;
          const loading = summariesQ.isPending;
          const rowError = result !== undefined && !result.ok;
          return (
            <Card key={e.id}>
              <CardContent className="flex items-center justify-between gap-3 py-3 flex-wrap">
                <div>
                  <p className="font-medium">{e.displayName}</p>
                  <p className="text-xs text-muted-foreground">{e.fullName}</p>
                </div>
                <div className="flex items-center gap-2 flex-wrap">
                  {loading && <span className="text-xs text-muted-foreground">betöltés…</span>}
                  {rowError && (
                    <span className="text-xs text-destructive">Nem sikerült betölteni</span>
                  )}
                  {s && (
                    <>
                      <Badge variant={s.isComplete ? "default" : "outline"}>
                        {s.isComplete ? "Lezárva" : "Folyamatban"}
                      </Badge>
                      <span className="text-xs text-muted-foreground">
                        {s.outstandingDeclarationCount}/{s.requiredDeclarationCount} nyilatkozat
                      </span>
                    </>
                  )}
                  <Button asChild size="sm" variant="outline">
                    <Link to="/app/admin/payroll/$employeeId" params={{ employeeId: e.id }}>
                      Megnyitás
                    </Link>
                  </Button>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      {total > PAGE_SIZE && (
        <div className="flex items-center justify-between gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Előző
          </Button>
          <span className="text-xs text-muted-foreground">
            {page} / {totalPages} oldal · {total} dolgozó
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
    </div>
  );
}
