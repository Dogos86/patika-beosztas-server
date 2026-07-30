import { createFileRoute, Link } from "@tanstack/react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { Button } from "@/components/ui/button";
import { PayrollHrTab } from "@/components/payroll/PayrollHrTab";
import { useRequirePermission } from "@/components/common/PermissionGate";

export const Route = createFileRoute("/app/admin/payroll/$employeeId")({
  head: () => ({ meta: [{ title: "Dolgozó bérszámfejtése — Patika Beosztás" }] }),
  component: PayrollEmployeePage,
});

function PayrollEmployeePage() {
  const denied = useRequirePermission([
    "ManagePayrollOnboarding",
    "ReviewTaxAllowanceSurvey",
    "ViewPayrollSensitiveData",
  ]);
  const { employeeId } = Route.useParams();
  if (denied) return denied;
  return (
    <div className="space-y-4">
      <PageHeader
        title="Bérszámfejtési onboarding"
        description="Profil, adókedvezmény-nyilatkozat és követelmények kezelése."
        action={
          <Button asChild variant="ghost">
            <Link to="/app/admin/payroll">Vissza</Link>
          </Button>
        }
      />
      <PayrollHrTab employeeId={employeeId} />
    </div>
  );
}
