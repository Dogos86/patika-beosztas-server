import { Link, useRouterState } from "@tanstack/react-router";
import {
  Home,
  CalendarDays,
  CalendarClock,
  ClipboardList,
  Bell,
  Settings,
  Sparkles,
  CheckSquare,
  Users,
  MapPin,
  Shield,
  LayoutGrid,
  LogOut,
  Menu,
  Wallet,
  Receipt,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTrigger } from "@/components/ui/sheet";
import { useAuth } from "@/hooks/use-auth";
import { useState, type ReactNode } from "react";
import type { AppPermission } from "@/services/types";
import { UserCog } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { dataSource } from "@/services";
import { frontendFeatures } from "@/config/features";

type NavItem = {
  to: string;
  label: string;
  icon: typeof Home;
  exact?: boolean;
  /** Menüpont csak akkor látszik, ha a felhasználó rendelkezik EGY IS ezekből. */
  anyPermission?: AppPermission[];
  /** Menüpont csak akkor látszik, ha a felhasználóhoz kapcsolt dolgozó van. */
  needsLinkedEmployee?: boolean;
  feature?: "ai" | "notifications";
};

const employeeNav: NavItem[] = [
  { to: "/app", label: "Kezdőlap", icon: Home, exact: true },
  {
    to: "/app/schedule",
    label: "Beosztás",
    icon: CalendarDays,
    anyPermission: ["ViewOwnSchedule"],
    needsLinkedEmployee: true,
  },
  {
    to: "/app/requests",
    label: "Kérelmek",
    icon: ClipboardList,
    anyPermission: ["ManageOwnLeaveRequests"],
    needsLinkedEmployee: true,
  },
  {
    to: "/app/preferences",
    label: "Munkavégzési kéréseim",
    icon: CalendarClock,
    anyPermission: ["ManageWorkPreferences"],
    needsLinkedEmployee: true,
  },
  { to: "/app/notifications", label: "Értesítések", icon: Bell, feature: "notifications" },
  {
    to: "/app/ai",
    label: "AI",
    icon: Sparkles,
    anyPermission: ["UseAiAssistant"],
    feature: "ai",
  },
  { to: "/app/payroll", label: "Bérszámfejtés", icon: Wallet, needsLinkedEmployee: true },
];

const adminNav: NavItem[] = [
  {
    to: "/app/admin/approvals",
    label: "Jóváhagyások",
    icon: CheckSquare,
    anyPermission: ["ApproveLeaveRequests", "ManageAllLeaveRequests"],
  },
  {
    to: "/app/admin/schedules",
    label: "Beosztás",
    icon: CalendarDays,
    anyPermission: ["ManageSchedules", "RunAutoFill", "ApproveSchedules", "PublishSchedules"],
  },
  {
    to: "/app/admin/employees",
    label: "Dolgozók",
    icon: Users,
    anyPermission: ["ManageEmployees"],
  },
  {
    to: "/app/admin/locations",
    label: "Telephelyek",
    icon: MapPin,
    anyPermission: ["ManageLocations"],
  },
  {
    to: "/app/admin/coverage",
    label: "Lefedettség",
    icon: Shield,
    anyPermission: ["ManageCoverageRules"],
  },
  { to: "/app/admin/users", label: "Felhasználók", icon: UserCog, anyPermission: ["ManageUsers"] },
  {
    to: "/app/admin/payroll",
    label: "Bérszámfejtés",
    icon: Receipt,
    anyPermission: [
      "ManagePayrollOnboarding",
      "ReviewTaxAllowanceSurvey",
      "ViewPayrollSensitiveData",
      "ExportPayrollData",
    ],
  },
];

function filterNav(items: NavItem[], perms: AppPermission[], hasEmp: boolean): NavItem[] {
  return items.filter((it) => {
    if (it.feature === "ai" && !frontendFeatures.aiEnabled) return false;
    if (it.feature === "notifications" && !frontendFeatures.notificationsEnabled) return false;
    if (it.needsLinkedEmployee && !hasEmp) return false;
    if (!it.anyPermission || it.anyPermission.length === 0) return true;
    return it.anyPermission.some((p) => perms.includes(p));
  });
}

function NavLinks({ items, onClick }: { items: NavItem[]; onClick?: () => void }) {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  return (
    <nav className="flex flex-col gap-1">
      {items.map((item) => {
        const active = item.exact ? pathname === item.to : pathname.startsWith(item.to);
        const Icon = item.icon;
        return (
          <Link
            key={item.to}
            to={item.to}
            onClick={onClick}
            className={`flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors ${
              active ? "bg-primary text-primary-foreground" : "hover:bg-sidebar-accent"
            }`}
          >
            <Icon className="h-4 w-4 shrink-0" />
            <span className="truncate">{item.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const perms = user?.permissions ?? [];
  const hasEmp = !!user?.linkedEmployee;
  const visibleEmployeeNav = filterNav(employeeNav, perms, hasEmp);
  const visibleAdminNav = filterNav(adminNav, perms, hasEmp);

  const sidebar = (
    <div className="flex h-full flex-col gap-4 p-4">
      <div className="flex items-center gap-2 px-2">
        <div className="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-primary text-primary-foreground font-bold">
          Rx
        </div>
        <div className="min-w-0">
          <p className="font-semibold truncate">Patika Beosztás</p>
          <p className="text-xs text-muted-foreground truncate">{user?.displayName}</p>
        </div>
      </div>
      <div className="px-2">
        <Badge variant={dataSource === "api" ? "default" : "outline"} className="text-xs">
          Adatforrás: {dataSource === "api" ? "Valódi API" : "Demóadatok"}
        </Badge>
      </div>
      <div>
        <p className="px-3 text-xs font-medium uppercase tracking-wider text-muted-foreground mb-1">
          Menü
        </p>
        <NavLinks items={visibleEmployeeNav} onClick={() => setOpen(false)} />
      </div>
      {visibleAdminNav.length > 0 && (
        <div>
          <p className="px-3 text-xs font-medium uppercase tracking-wider text-muted-foreground mb-1">
            Admin
          </p>
          <NavLinks items={visibleAdminNav} onClick={() => setOpen(false)} />
        </div>
      )}
      <div className="mt-auto">
        <Link
          to="/app/settings"
          onClick={() => setOpen(false)}
          className="flex items-center gap-3 rounded-md px-3 py-2 text-sm hover:bg-sidebar-accent"
        >
          <Settings className="h-4 w-4" />
          Beállítások
        </Link>
        <button
          onClick={() => {
            void logout();
          }}
          className="w-full flex items-center gap-3 rounded-md px-3 py-2 text-sm hover:bg-sidebar-accent text-left"
        >
          <LogOut className="h-4 w-4" />
          Kijelentkezés
        </button>
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-background">
      {/* Desktop sidebar */}
      <aside className="hidden md:flex fixed inset-y-0 left-0 w-64 border-r bg-sidebar">
        {sidebar}
      </aside>

      {/* Mobile top bar */}
      <header className="md:hidden sticky top-0 z-30 flex items-center gap-2 border-b bg-background/90 backdrop-blur px-3 h-14">
        <Sheet open={open} onOpenChange={setOpen}>
          <SheetTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="Menü">
              <Menu className="h-5 w-5" />
            </Button>
          </SheetTrigger>
          <SheetContent side="left" className="p-0 w-72 bg-sidebar">
            {sidebar}
          </SheetContent>
        </Sheet>
        <div className="flex items-center gap-2 min-w-0">
          <div className="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-primary text-primary-foreground text-sm font-bold">
            Rx
          </div>
          <p className="font-semibold text-sm truncate">Patika Beosztás</p>
        </div>
      </header>

      <main className="md:pl-64 pb-20 md:pb-6">
        <div className="max-w-6xl mx-auto p-4 md:p-6">{children}</div>
      </main>

      {/* Mobile bottom nav */}
      <BottomNav items={visibleEmployeeNav} />
    </div>
  );
}

function BottomNav({ items }: { items: NavItem[] }) {
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  if (items.length === 0) return null;
  return (
    <nav className="md:hidden fixed bottom-0 inset-x-0 z-30 border-t bg-background/95 backdrop-blur">
      <ul
        className="grid"
        style={{ gridTemplateColumns: `repeat(${items.length}, minmax(0,1fr))` }}
      >
        {items.map((item) => {
          const active = item.exact ? pathname === item.to : pathname.startsWith(item.to);
          const Icon = item.icon;
          return (
            <li key={item.to}>
              <Link
                to={item.to}
                className={`flex flex-col items-center justify-center gap-1 py-2 text-[11px] ${
                  active ? "text-primary" : "text-muted-foreground"
                }`}
              >
                <Icon className="h-5 w-5" />
                {item.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
