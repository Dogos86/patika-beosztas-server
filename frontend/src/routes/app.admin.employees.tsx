import { Outlet, createFileRoute } from "@tanstack/react-router";

/** Pathless layout az /app/admin/employees alá — a lista (index) és a
 *  szerkesztő ($id) is ide renderel. Nélküle az /app/admin/employees/$id
 *  route mount-olna, de nem jelenne meg semmi. */
export const Route = createFileRoute("/app/admin/employees")({
  component: () => <Outlet />,
});
