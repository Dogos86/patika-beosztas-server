import { createFileRoute } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { services } from "@/services";
import { PageHeader } from "@/components/common/PageHeader";
import { LoadingState } from "@/components/common/states";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { TaxAllowanceSurveyForm } from "@/components/payroll/TaxAllowanceSurveyForm";
import { DeclarationRequirementsList } from "@/components/payroll/DeclarationRequirementsList";
import { useAuth } from "@/hooks/use-auth";
import { payrollProfileStatusLabel } from "@/lib/payroll-labels";

export const Route = createFileRoute("/app/payroll")({
  head: () => ({ meta: [{ title: "Bérszámfejtés — Patika Beosztás" }] }),
  component: MyPayrollPage,
});

function MyPayrollPage() {
  const { user } = useAuth();
  const taxYear = new Date().getFullYear();
  const summary = useQuery({
    queryKey: ["my-onboarding"],
    queryFn: () => services.payroll.getMyOnboarding(),
    enabled: !!user?.linkedEmployee,
  });

  if (!user?.linkedEmployee) {
    return (
      <div className="space-y-4">
        <PageHeader
          title="Bérszámfejtés"
          description="A saját bérszámfejtési adataid megtekintéséhez dolgozói profil szükséges."
        />
        <Card>
          <CardContent className="py-6 text-sm text-muted-foreground">
            Nincs kapcsolt dolgozói profilod — fordulj az adminisztrátorhoz.
          </CardContent>
        </Card>
      </div>
    );
  }

  if (summary.isLoading || !summary.data) return <LoadingState />;
  const s = summary.data;
  return (
    <div className="space-y-4">
      <PageHeader
        title="Bérszámfejtés"
        description="Saját adókedvezmény-nyilatkozatod és az onboarding állapota."
      />
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Onboarding állapot</CardTitle>
          <Badge variant={s.isComplete ? "default" : "outline"}>
            {s.isComplete ? "Lezárva" : "Folyamatban"}
          </Badge>
        </CardHeader>
        <CardContent className="text-sm space-y-1">
          <p>
            <span className="text-muted-foreground">Profil státusz: </span>
            {s.payrollProfile ? payrollProfileStatusLabel(s.payrollProfile.status) : "nincs"}
          </p>
          <p>
            <span className="text-muted-foreground">Nyilatkozatok: </span>
            {s.outstandingDeclarationCount} / {s.requiredDeclarationCount} hátra
          </p>
        </CardContent>
      </Card>
      <TaxAllowanceSurveyForm
        mode="self"
        employeeId={user.linkedEmployee.id}
        taxYear={taxYear}
        survey={s.latestSurvey}
      />
      <DeclarationRequirementsList
        employeeId={user.linkedEmployee.id}
        requirements={s.latestSurvey?.declarationRequirements ?? []}
        canEdit={false}
      />
    </div>
  );
}
