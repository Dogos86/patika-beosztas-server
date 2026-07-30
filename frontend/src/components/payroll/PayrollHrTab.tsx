import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { services } from "@/services";
import { LoadingState } from "@/components/common/states";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { PayrollProfileCard } from "./PayrollProfileCard";
import { TaxAllowanceSurveyForm } from "./TaxAllowanceSurveyForm";
import { DeclarationRequirementsList } from "./DeclarationRequirementsList";
import { useHasAnyPermission } from "@/hooks/use-auth";

interface Props {
  employeeId: string;
  taxYear?: number;
}

export function PayrollHrTab({ employeeId, taxYear = new Date().getFullYear() }: Props) {
  const qc = useQueryClient();
  const canManage = useHasAnyPermission(["ManagePayrollOnboarding"]);
  const canExport = useHasAnyPermission(["ExportPayrollData"]);
  const summaryQ = useQuery({
    queryKey: ["payroll-summary", employeeId],
    queryFn: () => services.payroll.getSummary(employeeId),
  });

  const completeMut = useMutation({
    mutationFn: async () => {
      const profile = summaryQ.data?.payrollProfile;
      if (!profile) throw new Error("Nincs profil.");
      return services.payroll.completeOnboarding(employeeId, profile.version);
    },
    onSuccess: () => {
      toast.success("Onboarding lezárva.");
      qc.invalidateQueries({ queryKey: ["payroll-summary", employeeId] });
    },
    onError: (e) => toast.error((e as Error).message),
  });

  async function doExport(format: "json" | "csv") {
    try {
      const blob = await services.payroll.exportOnboarding(employeeId, format);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `payroll-onboarding-${employeeId}.${format}`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      toast.error("Export sikertelen.", { description: (e as Error).message });
    }
  }

  if (summaryQ.isLoading) return <LoadingState />;
  const summary = summaryQ.data;
  if (!summary) return null;

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between flex-wrap gap-2">
          <CardTitle>Onboarding összefoglaló</CardTitle>
          <div className="flex items-center gap-2 flex-wrap">
            <Badge variant={summary.isComplete ? "default" : "outline"}>
              {summary.isComplete ? "Lezárva" : "Folyamatban"}
            </Badge>
            <span className="text-xs text-muted-foreground">
              {summary.outstandingDeclarationCount} / {summary.requiredDeclarationCount} nyilatkozat
              hátra
            </span>
          </div>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2 justify-end">
          {canExport && (
            <>
              <Button variant="outline" size="sm" onClick={() => doExport("json")}>
                Export JSON
              </Button>
              <Button variant="outline" size="sm" onClick={() => doExport("csv")}>
                Export CSV
              </Button>
            </>
          )}
          {canManage && !summary.isComplete && (
            <Button
              onClick={() => completeMut.mutate()}
              disabled={completeMut.isPending || !summary.payrollProfile}
            >
              Onboarding lezárása
            </Button>
          )}
        </CardContent>
      </Card>

      <PayrollProfileCard
        employeeId={employeeId}
        profile={summary.payrollProfile}
        canEdit={canManage}
      />

      <TaxAllowanceSurveyForm
        mode="admin"
        employeeId={employeeId}
        taxYear={taxYear}
        survey={summary.latestSurvey}
      />

      <DeclarationRequirementsList
        employeeId={employeeId}
        requirements={summary.latestSurvey?.declarationRequirements ?? []}
        canEdit={canManage}
      />
    </div>
  );
}
