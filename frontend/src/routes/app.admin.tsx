import { createFileRoute, Outlet, Link, useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";
import { useAuth, useIsAdmin } from "@/hooks/use-auth";
import { LoadingState } from "@/components/common/states";
import { Button } from "@/components/ui/button";
import { ShieldAlert } from "lucide-react";

export const Route = createFileRoute("/app/admin")({
  head: () => ({ meta: [{ title: "Adminisztráció — Patika Beosztás" }] }),
  component: AdminGuard,
});

/**
 * UX-védelem: minden /app/admin/* útvonalat admin jogosultsághoz köt.
 * A menüpont elrejtése önmagában nem elég — közvetlen URL-lel se
 * legyen elérhető. A valódi biztonsági döntést a backend hozza majd meg.
 */
function AdminGuard() {
  const { user, loading } = useAuth();
  const hasAdminAccess = useIsAdmin();
  const navigate = useNavigate();

  useEffect(() => {
    if (!loading && !user) navigate({ to: "/login" });
  }, [user, loading, navigate]);

  if (loading || !user) return <LoadingState />;

  if (!hasAdminAccess) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 rounded-lg border bg-card py-16 px-4 text-center">
        <ShieldAlert className="h-10 w-10 text-destructive" />
        <div>
          <h1 className="text-lg font-semibold">Nincs jogosultságod ehhez az oldalhoz</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Ehhez az oldalhoz nincs megfelelő jogosultságod. Fordulj az adminisztrátorhoz.
          </p>
        </div>
        <Button asChild variant="outline">
          <Link to="/app">Vissza a kezdőlapra</Link>
        </Button>
      </div>
    );
  }

  return <Outlet />;
}
