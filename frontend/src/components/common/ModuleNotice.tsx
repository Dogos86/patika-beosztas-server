import type { ReactNode } from "react";
import { AlertTriangle, Info } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";

/**
 * Egységes, jól látható jelzés arra, hogy egy modul API-módban még nem
 * valódi backendről működik. Cél: a felhasználó soha ne higgye azt, hogy
 * valódi adatot mentett, miközben csak demóadatokat lát.
 */
export function DemoOnlyNotice({
  title = "Ez a modul még demóadatokkal működik",
  children,
}: {
  title?: string;
  children?: ReactNode;
}) {
  return (
    <Card className="border-amber-500/40 bg-amber-500/5">
      <CardContent className="flex items-start gap-3 py-4">
        <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
        <div className="text-sm">
          <p className="font-medium">{title}</p>
          {children ? <p className="text-muted-foreground mt-1">{children}</p> : null}
        </div>
      </CardContent>
    </Card>
  );
}

/** Későbbi fázisra halasztott vagy kiváltott (legacy) modul jelzése. */
export function ModuleUnavailableNotice({
  title,
  children,
}: {
  title: string;
  children?: ReactNode;
}) {
  return (
    <Card>
      <CardContent className="flex items-start gap-3 py-6">
        <Info className="h-4 w-4 shrink-0 text-muted-foreground mt-0.5" />
        <div className="text-sm">
          <p className="font-medium">{title}</p>
          {children ? <div className="text-muted-foreground mt-1">{children}</div> : null}
        </div>
      </CardContent>
    </Card>
  );
}
