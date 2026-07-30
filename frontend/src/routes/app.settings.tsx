import { createFileRoute } from "@tanstack/react-router";
import { useAuth, useIsAdmin } from "@/hooks/use-auth";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { useState } from "react";
import { frontendFeatures } from "@/config/features";

export const Route = createFileRoute("/app/settings")({
  head: () => ({ meta: [{ title: "Beállítások — Patika Beosztás" }] }),
  component: SettingsPage,
});

function SettingsPage() {
  const { user, logout } = useAuth();
  const isAdmin = useIsAdmin();
  const [notifEmail, setNotifEmail] = useState(true);
  const [notifPush, setNotifPush] = useState(true);
  const [selfApprove, setSelfApprove] = useState(false);

  return (
    <div>
      <PageHeader title="Beállítások" description="Profil, értesítések és szervezeti kapcsolók." />

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Profil</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2">
              <Label>Megjelenítési név</Label>
              <Input defaultValue={user?.displayName} />
            </div>
            <div className="space-y-2">
              <Label>Email</Label>
              <Input type="email" defaultValue={user?.email} disabled />
            </div>
            {frontendFeatures.isPilot ? (
              <p className="text-xs text-muted-foreground">
                A profiladatokat a zárt pilotban az adminisztrátor kezeli.
              </p>
            ) : (
              <Button onClick={() => toast.success("Mentve (mock)")}>Mentés</Button>
            )}
          </CardContent>
        </Card>

        {frontendFeatures.notificationsEnabled && (
          <Card>
            <CardHeader>
              <CardTitle>Értesítések</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-center justify-between">
                <Label htmlFor="e1">Email értesítések</Label>
                <Switch id="e1" checked={notifEmail} onCheckedChange={setNotifEmail} />
              </div>
              <div className="flex items-center justify-between">
                <Label htmlFor="e2">Push értesítések</Label>
                <Switch id="e2" checked={notifPush} onCheckedChange={setNotifPush} />
              </div>
            </CardContent>
          </Card>
        )}

        {isAdmin && !frontendFeatures.isPilot && (
          <>
            {frontendFeatures.aiEnabled && (
              <Card>
                <CardHeader>
                  <CardTitle>Szervezeti beállítások</CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  <div className="space-y-2">
                    <Label>Szervezet neve</Label>
                    <Input defaultValue="Központi Patika Kft." />
                  </div>
                  <div className="flex items-center justify-between">
                    <div>
                      <Label htmlFor="sa">Önjóváhagyás engedélyezése</Label>
                      <p className="text-xs text-muted-foreground">
                        Adminok saját kérelmüket jóváhagyhatják.
                      </p>
                    </div>
                    <Switch id="sa" checked={selfApprove} onCheckedChange={setSelfApprove} />
                  </div>
                </CardContent>
              </Card>
            )}
            <Card>
              <CardHeader>
                <CardTitle>AI szolgáltatás</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-center gap-2">
                  <span className="h-2 w-2 rounded-full bg-emerald-500" />
                  <p className="text-sm">Elérhető (mock)</p>
                </div>
                <p className="text-xs text-muted-foreground mt-2">
                  Az éles AI szolgáltatás állapota itt jelenik meg.
                </p>
              </CardContent>
            </Card>
          </>
        )}

        <Card className="md:col-span-2">
          <CardContent className="p-4 flex items-center justify-between">
            <div>
              <p className="font-medium">Kijelentkezés</p>
              <p className="text-sm text-muted-foreground">Vissza a bejelentkezési oldalra.</p>
            </div>
            <Button
              variant="outline"
              onClick={() => {
                void logout();
              }}
            >
              Kijelentkezés
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
