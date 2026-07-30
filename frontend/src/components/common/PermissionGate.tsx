import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { ShieldAlert } from "lucide-react";
import { useHasAnyPermission } from "@/hooks/use-auth";
import { Button } from "@/components/ui/button";
import type { AppPermission } from "@/services/types";

interface PermissionGateProps {
  required: readonly AppPermission[];
  children: ReactNode;
}

/** Per-oldal jogosultsági gate az admin al-oldalakhoz. A backend a végső
 *  döntéshozó; ez UX-védelem és világos hibaüzenet. */
export function PermissionGate({ required, children }: PermissionGateProps) {
  const allowed = useHasAnyPermission(required);
  if (allowed) return <>{children}</>;
  return <NoPermission />;
}

/** Hook-forma az admin al-oldalakhoz: ha van jogosultság, `null`-t ad
 *  vissza, egyébként a tiltó nézetet — a hívó `if (denied) return denied;`. */
export function useRequirePermission(required: readonly AppPermission[]): ReactNode | null {
  const allowed = useHasAnyPermission(required);
  if (allowed) return null;
  return <NoPermission />;
}

function NoPermission() {
  return (
    <div className="flex flex-col items-center justify-center gap-4 rounded-lg border bg-card py-12 px-4 text-center">
      <ShieldAlert className="h-8 w-8 text-destructive" />
      <div>
        <h2 className="text-base font-semibold">Ehhez az oldalhoz nincs jogosultságod</h2>
        <p className="text-sm text-muted-foreground mt-1">
          Fordulj az adminisztrátorhoz, ha úgy gondolod, hogy hozzáférést kellene kapnod.
        </p>
      </div>
      <Button asChild variant="outline" size="sm">
        <Link to="/app">Vissza a kezdőlapra</Link>
      </Button>
    </div>
  );
}
