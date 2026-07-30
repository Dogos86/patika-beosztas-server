import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { services } from "@/services";
import { useRequirePermission } from "@/components/common/PermissionGate";
import { ModuleUnavailableNotice } from "@/components/common/ModuleNotice";
import { frontendFeatures } from "@/config/features";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Mic, MicOff, Sparkles, AlertTriangle, Check } from "lucide-react";
import type { AiCommandPreview } from "@/services/types";
import { toast } from "sonner";

export const Route = createFileRoute("/app/ai")({
  head: () => ({ meta: [{ title: "AI asszisztens — Patika Beosztás" }] }),
  component: AiPage,
});

const examples = [
  "Szeretnék szabadságot jövő héten csütörtök-péntek",
  "Betegállományba megyek ma és holnap",
  "Cseréljük meg a csütörtöki műszakot Eszterrel",
];

function AiPage() {
  const denied = useRequirePermission(["UseAiAssistant"]);
  const [text, setText] = useState("");
  const [recording, setRecording] = useState(false);
  const [preview, setPreview] = useState<AiCommandPreview | null>(null);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const expired = preview ? new Date(preview.expiresAt).getTime() < Date.now() : false;

  async function interpret() {
    if (!text.trim()) return;
    setLoading(true);
    try {
      const result = await services.ai.interpretCommand({ text });
      setPreview(result);
      setAnswers({});
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Nem sikerült az értelmezés.");
    } finally {
      setLoading(false);
    }
  }

  async function sendClarification(id: string) {
    if (!preview || !answers[id]) return;
    setLoading(true);
    try {
      const updated = await services.ai.answerClarification(preview.previewId, id, answers[id]);
      setPreview(updated);
    } finally {
      setLoading(false);
    }
  }

  async function execute() {
    if (!preview || !preview.canExecute) return;
    if (expired) {
      toast.error("Az előnézet lejárt. Kérj újat.");
      return;
    }
    setLoading(true);
    try {
      const res = await services.ai.executeCommand(preview.previewId, preview.confirmationToken);
      toast.success(`Végrehajtva (audit: ${res.auditId})`);
      setPreview(null);
      setText("");
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "A végrehajtás nem sikerült.");
    } finally {
      setLoading(false);
      setConfirmOpen(false);
    }
  }

  if (denied) return denied;

  if (!frontendFeatures.aiEnabled) {
    return (
      <div className="space-y-4">
        <PageHeader title="AI asszisztens" description="Szöveges és diktált műveleti javaslatok." />
        <ModuleUnavailableNotice title="Az AI asszisztens a zárt pilotban ki van kapcsolva">
          A pilotban nem fut AI- vagy beszédszolgáltató, és demóválaszokat sem jelenítünk meg.
        </ModuleUnavailableNotice>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="AI asszisztens"
        description="Írd le vagy diktáld — az AI javasol műveleteket, alkalmazás előtt megnézed."
      />
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-normal text-muted-foreground">Kérésed</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <Textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Például: Szeretnék szabadságot jövő héten csütörtök-péntek."
            rows={4}
            className="resize-none"
          />
          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              onClick={() => {
                setRecording((r) => !r);
                if (!recording) toast.info("Felvétel elindult (mock)");
                else {
                  toast.success("Felvétel átírva (mock)");
                  setText((t) => t || "Szeretnék szabadságot jövő héten csütörtök-péntek.");
                }
              }}
            >
              {recording ? <MicOff className="h-4 w-4 mr-2" /> : <Mic className="h-4 w-4 mr-2" />}
              {recording ? "Felvétel leállítása" : "Diktálás"}
            </Button>
            <Button onClick={interpret} disabled={loading || !text.trim()}>
              <Sparkles className="h-4 w-4 mr-2" />
              {loading ? "Értelmezés..." : "Értelmezés"}
            </Button>
          </div>
          <div className="pt-2">
            <p className="text-xs text-muted-foreground mb-2">Példamondatok:</p>
            <div className="flex flex-wrap gap-2">
              {examples.map((ex) => (
                <button
                  key={ex}
                  onClick={() => setText(ex)}
                  className="text-xs rounded-full border px-3 py-1 hover:bg-accent"
                >
                  {ex}
                </button>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>

      {preview && (
        <div className="mt-4 space-y-3">
          <h2 className="text-sm font-medium text-muted-foreground">Előnézet</h2>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">{preview.summary}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {preview.resolvedActions.map((a, i) => (
                <div key={i} className="rounded-md border p-3">
                  <p className="font-medium text-sm">{a.summary}</p>
                  <ul className="text-sm text-muted-foreground list-disc pl-5 mt-1">
                    {a.details.map((d, j) => (
                      <li key={j}>{d}</li>
                    ))}
                  </ul>
                </div>
              ))}

              {preview.clarifications.filter((c) => !c.answered).length > 0 && (
                <div className="space-y-2">
                  <p className="text-sm font-medium">Pontosítás szükséges</p>
                  {preview.clarifications
                    .filter((c) => !c.answered)
                    .map((c) => (
                      <div key={c.id} className="rounded-md border p-3 space-y-2">
                        <p className="text-sm">{c.question}</p>
                        <div className="flex gap-2">
                          <Input
                            value={answers[c.id] ?? ""}
                            onChange={(e) => setAnswers((a) => ({ ...a, [c.id]: e.target.value }))}
                            placeholder="Válaszod"
                          />
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => sendClarification(c.id)}
                            disabled={loading || !answers[c.id]}
                          >
                            Küldés
                          </Button>
                        </div>
                      </div>
                    ))}
                </div>
              )}

              {preview.warnings.length > 0 && (
                <div className="rounded-md border border-amber-200 bg-amber-50 p-3">
                  {preview.warnings.map((w, i) => (
                    <p key={i} className="text-sm text-amber-800 flex items-start gap-2">
                      <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" /> {w}
                    </p>
                  ))}
                </div>
              )}

              {expired && (
                <div className="rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">
                  Az előnézet lejárt. Futtasd újra az értelmezést.
                </div>
              )}

              <p className="text-xs text-muted-foreground">
                Preview azonosító: <code>{preview.previewId}</code> · lejár:{" "}
                {new Date(preview.expiresAt).toLocaleTimeString("hu")}
              </p>

              <div className="flex gap-2 justify-end">
                <Button variant="ghost" size="sm" onClick={() => setPreview(null)}>
                  Elvetés
                </Button>
                {expired ? (
                  <Button size="sm" onClick={interpret} disabled={loading}>
                    Új értelmezés
                  </Button>
                ) : (
                  <Button
                    size="sm"
                    disabled={!preview.canExecute || loading}
                    onClick={() => setConfirmOpen(true)}
                  >
                    <Check className="h-4 w-4 mr-1" /> Végrehajtás
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Biztosan végrehajtod?</AlertDialogTitle>
            <AlertDialogDescription>
              Az AI művelet a fenti előnézet szerint kerül rögzítésre. Az audit napló megőrzi a
              lépéseket.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Mégse</AlertDialogCancel>
            <AlertDialogAction onClick={execute}>Végrehajtás</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
