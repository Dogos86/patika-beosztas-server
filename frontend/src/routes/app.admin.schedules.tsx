import { createFileRoute, Outlet } from "@tanstack/react-router";

export const Route = createFileRoute("/app/admin/schedules")({
  head: () => ({ meta: [{ title: "Beosztások — Patika Beosztás" }] }),
  component: () => <Outlet />,
});
