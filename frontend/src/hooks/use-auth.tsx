import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { services } from "@/services";
import type { AppPermission, User } from "@/services/types";

interface AuthContextValue {
  user: User | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<User>;
  logout: () => Promise<void>;
}

/** Minden „adminos" felület legalább ezek egyikét igényli. */
export const ADMIN_PERMISSIONS: AppPermission[] = [
  "ApproveLeaveRequests",
  "ManageAllLeaveRequests",
  "ManageEmployees",
  "ManageLocations",
  "ManageCoverageRules",
  "ManageSchedules",
  "RunAutoFill",
  "ApproveSchedules",
  "PublishSchedules",
  "ManageUsers",
];

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    services.auth.getCurrentUser().then((u) => {
      setUser(u);
      setLoading(false);
    });
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const u = await services.auth.login(email, password);
    setUser(u);
    return u;
  }, []);

  const logout = useCallback(async () => {
    await services.auth.logout();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, login, logout }}>{children}</AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth: AuthProvider hiányzik");
  return ctx;
}

export function useHasPermission(permission: AppPermission | null | undefined) {
  const { user } = useAuth();
  if (!permission) return false;
  return user?.permissions.includes(permission) ?? false;
}

export function useHasAnyPermission(permissions: readonly AppPermission[]) {
  const { user } = useAuth();
  return useMemo(() => {
    if (!user) return false;
    return permissions.some((p) => user.permissions.includes(p));
  }, [user, permissions]);
}

/** Kompatibilitási helper: „bármelyik admin jogosultsággal rendelkezik". */
export function useIsAdmin() {
  return useHasAnyPermission(ADMIN_PERMISSIONS);
}

/** `null`, ha a felhasználóhoz nincs kapcsolt dolgozói profil. */
export function useMyEmployeeId() {
  const { user } = useAuth();
  return user?.linkedEmployee?.id ?? null;
}
