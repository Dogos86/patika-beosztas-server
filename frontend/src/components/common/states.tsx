import { Loader2, InboxIcon, AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { ReactNode } from "react";

export function LoadingState({ label = "Betöltés..." }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-12 text-muted-foreground">
      <Loader2 className="h-4 w-4 animate-spin" />
      <span className="text-sm">{label}</span>
    </div>
  );
}

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed py-12 px-4 text-center">
      <InboxIcon className="h-8 w-8 text-muted-foreground" />
      <div>
        <p className="font-medium">{title}</p>
        {description && <p className="text-sm text-muted-foreground mt-1">{description}</p>}
      </div>
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-destructive/30 bg-destructive/5 py-12 px-4 text-center">
      <AlertTriangle className="h-8 w-8 text-destructive" />
      <p className="text-sm">{message}</p>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          Újrapróbálás
        </Button>
      )}
    </div>
  );
}
