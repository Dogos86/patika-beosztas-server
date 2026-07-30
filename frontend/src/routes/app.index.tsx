import { createFileRoute, Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { useAuth, useIsAdmin, useMyEmployeeId } from "@/hooks/use-auth";
import { EmptyState } from "@/components/common/states";
import { services } from "@/services";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/common/PageHeader";
import {
  fmtDate,
  fmtRelative,
  leaveTypeLabel,
  todayISO,
  weekStartISO,
  addDaysISO,
} from "@/lib/format";
import {
  CalendarDays,
  ClipboardList,
  Sparkles,
  Stethoscope,
  CheckSquare,
  AlertTriangle,
  Users,
} from "lucide-react";
import { StatusBadge } from "@/components/common/StatusBadge";
import { frontendFeatures } from "@/config/features";

export const Route = createFileRoute("/app/")({
  head: () => ({ meta: [{ title: "Kezdőlap — Patika Beosztás" }] }),
  component: Home,
});

function Home() {
  const isAdmin = useIsAdmin();
  return isAdmin ? <AdminHome /> : <EmployeeHome />;
}

function EmployeeHome() {
  const { user } = useAuth();
  const employeeId = useMyEmployeeId();
  const weekStart = weekStartISO();
  const weekEnd = addDaysISO(weekStart, 6);

  const shifts = useQuery({
    enabled: !!employeeId,
    queryKey: ["myShifts", employeeId, weekStart],
    queryFn: () => services.schedule.getMySchedule({ from: weekStart, to: weekEnd }),
  });
  const requests = useQuery({
    enabled: !!employeeId,
    queryKey: ["myRequests", employeeId],
    queryFn: () => services.leaveRequest.listMyRequests(),
  });

  const upcoming = shifts.data
    ?.filter((s) => s.date >= todayISO())
    .sort((a, b) => a.date.localeCompare(b.date))[0];
  const totalMinutes =
    shifts.data?.reduce((sum, s) => {
      const [sh, sm] = s.start.split(":").map(Number);
      const [eh, em] = s.end.split(":").map(Number);
      return sum + (eh * 60 + em) - (sh * 60 + sm);
    }, 0) ?? 0;
  const pending = requests.data?.filter((r) => r.status === "pending") ?? [];

  if (!employeeId) {
    return (
      <div>
        <PageHeader
          title={`Szia, ${user!.displayName}!`}
          description="Nincs dolgozói profil a fiókodhoz kapcsolva."
        />
        <EmptyState
          title="Nincs kapcsolt dolgozói profil"
          description="Az admin még nem kötötte a fiókodat egy Employee rekordhoz. Amint ez megtörténik, itt megjelenik a beosztásod és a kérelmek felülete."
        />
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title={`Szia, ${user!.displayName}!`}
        description="Áttekintés a hétről és a kérelmekről."
      />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm text-muted-foreground font-normal flex items-center gap-2">
              <CalendarDays className="h-4 w-4" /> Következő műszak
            </CardTitle>
          </CardHeader>
          <CardContent>
            {upcoming ? (
              <div>
                <p className="text-lg font-semibold">{fmtDate(upcoming.date)}</p>
                <p className="text-sm text-muted-foreground">
                  {upcoming.start}–{upcoming.end}
                </p>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">Nincs beütemezett műszak.</p>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm text-muted-foreground font-normal">Heti óráim</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">{(totalMinutes / 60).toFixed(1)} óra</p>
            <p className="text-sm text-muted-foreground">{shifts.data?.length ?? 0} műszak</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm text-muted-foreground font-normal">
              Függő kérelmek
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">{pending.length}</p>
            <p className="text-sm text-muted-foreground">jóváhagyásra vár</p>
          </CardContent>
        </Card>
      </div>

      <div className="mt-6">
        <h2 className="text-sm font-medium text-muted-foreground mb-3">Gyors műveletek</h2>
        <div className="grid gap-3 grid-cols-1 sm:grid-cols-3">
          <QuickAction
            to="/app/requests"
            icon={ClipboardList}
            title="Szabadságigény"
            description="Új szabadság kérelem"
          />
          <QuickAction
            to="/app/requests"
            icon={Stethoscope}
            title="Betegállomány"
            description="Betegállomány rögzítése"
          />
          {frontendFeatures.aiEnabled && (
            <QuickAction
              to="/app/ai"
              icon={Sparkles}
              title="AI diktálás"
              description="Írd le vagy mondd el"
            />
          )}
        </div>
      </div>

      <div className="mt-6">
        <h2 className="text-sm font-medium text-muted-foreground mb-3">Legutóbbi kérelmek</h2>
        <Card>
          <CardContent className="p-0 divide-y">
            {(requests.data ?? []).slice(0, 5).map((r) => (
              <div key={r.id} className="p-4 flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium truncate">{leaveTypeLabel(r.type)}</p>
                  <p className="text-xs text-muted-foreground">
                    {fmtDate(r.startDate)} – {fmtDate(r.endDate)} · {fmtRelative(r.createdAt)}
                  </p>
                </div>
                <StatusBadge status={r.status} />
              </div>
            ))}
            {(!requests.data || requests.data.length === 0) && (
              <p className="p-6 text-sm text-muted-foreground text-center">Még nincs kérelem.</p>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function AdminHome() {
  const pending = useQuery({
    queryKey: ["pendingApprovals"],
    queryFn: () => services.adminLeaveRequest.listRequests({ status: "pending" }),
  });
  const employees = useQuery({
    queryKey: ["employees"],
    queryFn: () => services.employee.listAll(),
  });

  return (
    <div>
      <PageHeader
        title="Admin áttekintés"
        description="Jóváhagyások, létszám és beosztási hibák."
      />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm text-muted-foreground font-normal flex items-center gap-2">
              <CheckSquare className="h-4 w-4" /> Jóváhagyásra vár
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">{pending.data?.length ?? 0}</p>
            <Button asChild variant="link" size="sm" className="px-0">
              <Link to="/app/admin/approvals">Megnyitás →</Link>
            </Button>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm text-muted-foreground font-normal flex items-center gap-2">
              <AlertTriangle className="h-4 w-4" /> Mai létszámhiány
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">1</p>
            <p className="text-sm text-muted-foreground">Északi fiók — gyógyszerész</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm text-muted-foreground font-normal flex items-center gap-2">
              <Users className="h-4 w-4" /> Aktív dolgozók
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">
              {employees.data?.filter((e) => e.active).length ?? 0}
            </p>
          </CardContent>
        </Card>
      </div>

      <div className="mt-6">
        <h2 className="text-sm font-medium text-muted-foreground mb-3">Gyors műveletek</h2>
        <div className="grid gap-3 grid-cols-1 sm:grid-cols-3">
          <QuickAction
            to="/app/admin/scheduler"
            icon={CalendarDays}
            title="Beosztásszerkesztő"
            description="Heti/havi beosztás"
          />
          <QuickAction
            to="/app/admin/approvals"
            icon={CheckSquare}
            title="Jóváhagyások"
            description="Függő kérelmek"
          />
          <QuickAction
            to="/app/admin/employees"
            icon={Users}
            title="Dolgozók"
            description="Törzsadatok"
          />
        </div>
      </div>

      <div className="mt-6">
        <h2 className="text-sm font-medium text-muted-foreground mb-3">
          Következő hét lehetséges hibái
        </h2>
        <Card>
          <CardContent className="p-4 text-sm text-muted-foreground">
            <ul className="list-disc pl-5 space-y-1">
              <li>Északi fiók, kedd — gyógyszerész hiányzik 09:00–17:00.</li>
              <li>Központi patika, csütörtök — 1 asszisztens javasolt, jelenleg 0.</li>
            </ul>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function QuickAction({
  to,
  icon: Icon,
  title,
  description,
}: {
  to: string;
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  description: string;
}) {
  return (
    <Link to={to} className="group">
      <Card className="transition-colors group-hover:border-primary/40 group-hover:bg-accent/30">
        <CardContent className="p-4 flex items-center gap-3">
          <div className="grid h-10 w-10 shrink-0 place-items-center rounded-md bg-primary/10 text-primary">
            <Icon className="h-5 w-5" />
          </div>
          <div className="min-w-0">
            <p className="font-medium truncate">{title}</p>
            <p className="text-xs text-muted-foreground truncate">{description}</p>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}
