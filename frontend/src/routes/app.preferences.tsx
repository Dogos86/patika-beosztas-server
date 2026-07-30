import { createFileRoute } from "@tanstack/react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent } from "@/components/ui/card";
import { WorkPreferencesCard } from "@/components/work-preferences/WorkPreferencesCard";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { useAuth } from "@/hooks/use-auth";

export const Route = createFileRoute("/app/preferences")({
  head: () => ({
    meta: [
      { title: "Munkavégzési kéréseim — Patika Beosztás" },
      {
        name: "description",
        content:
          "Saját munkavégzési kérések és visszatérő szabályok: elérhetőség, preferencia, rögzített műszak.",
      },
      { property: "og:title", content: "Munkavégzési kéréseim — Patika Beosztás" },
      {
        property: "og:description",
        content: "Rögzítsd saját elérhetőségi és preferencia szabályaidat a beosztás tervezéséhez.",
      },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary" },
    ],
  }),
  component: MyWorkPreferencesPage,
});

function MyWorkPreferencesPage() {
  const denied = useRequirePermission(["ManageWorkPreferences"]);
  const { user } = useAuth();
  if (denied) return denied;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Munkavégzési kéréseim"
        description="Saját elérhetőségi, preferencia és rögzített műszak szabályaid a beosztás tervezéséhez."
      />
      {!user?.linkedEmployee ? (
        <Card>
          <CardContent className="py-6 text-sm text-muted-foreground">
            Nincs kapcsolt dolgozói profilod — fordulj az adminisztrátorhoz.
          </CardContent>
        </Card>
      ) : (
        <WorkPreferencesCard mode="self" />
      )}
    </div>
  );
}
