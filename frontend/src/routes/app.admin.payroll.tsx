import { createFileRoute, Outlet } from "@tanstack/react-router";

export const Route = createFileRoute("/app/admin/payroll")({
  head: () => ({ meta: [{ title: "Bérszámfejtés — Patika Beosztás" }] }),
  component: () => <Outlet />,
});
