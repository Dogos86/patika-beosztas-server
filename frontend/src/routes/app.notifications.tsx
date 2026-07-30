import { createFileRoute } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { services, dataSource } from "@/services";
import { frontendFeatures } from "@/config/features";
import { ModuleUnavailableNotice } from "@/components/common/ModuleNotice";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { EmptyState, LoadingState } from "@/components/common/states";
import { fmtRelative, notificationKindLabel } from "@/lib/format";
import { Bell } from "lucide-react";

export const Route = createFileRoute("/app/notifications")({
  head: () => ({ meta: [{ title: "Értesítések — Patika Beosztás" }] }),
  component: NotificationsPage,
});

function NotificationsPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const apiPending = !frontendFeatures.notificationsEnabled || dataSource === "api";
  const query = useQuery({
    queryKey: ["notifications", user!.id],
    queryFn: () => services.notification.listForUser(user!.id),
    enabled: !apiPending,
  });
  const markRead = useMutation({
    mutationFn: (id: string) => services.notification.markRead(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["notifications"] }),
  });

  if (apiPending) {
    return (
      <div className="space-y-4">
        <PageHeader
          title="Értesítések"
          description="Fontos történések a beosztásoddal kapcsolatban."
        />
        <ModuleUnavailableNotice title="Az értesítések a zárt pilotban ki vannak kapcsolva">
          A pilotban nem fut értesítési szolgáltató, és demóértesítéseket sem jelenítünk meg.
        </ModuleUnavailableNotice>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="Értesítések"
        description="Fontos történések a beosztásoddal kapcsolatban."
      />
      {query.isLoading && <LoadingState />}
      {!query.isLoading && (query.data ?? []).length === 0 && (
        <EmptyState
          title="Nincs értesítés"
          description="Ha új esemény történik, itt jelenik meg."
        />
      )}
      <div className="space-y-2">
        {(query.data ?? []).map((n) => (
          <Card key={n.id} className={n.read ? "opacity-70" : ""}>
            <CardContent className="p-4 flex items-start gap-3">
              <div className="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-primary/10 text-primary">
                <Bell className="h-4 w-4" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-xs text-muted-foreground uppercase">
                  {notificationKindLabel(n.kind)}
                </p>
                <p className="font-medium">{n.title}</p>
                <p className="text-sm text-muted-foreground">{n.body}</p>
                <p className="text-xs text-muted-foreground mt-1">{fmtRelative(n.createdAt)}</p>
              </div>
              {!n.read && (
                <Button variant="outline" size="sm" onClick={() => markRead.mutate(n.id)}>
                  Olvasva
                </Button>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
