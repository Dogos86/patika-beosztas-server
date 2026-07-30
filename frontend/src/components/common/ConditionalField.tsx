import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { FIELD_NOT_RELEVANT_HINT } from "@/lib/survey-relevance";

interface Props {
  /** A mező üzletileg releváns-e a jelenlegi válaszok alapján. */
  relevant: boolean;
  /** Külső disabled (read-only, folyamatban lévő mentés). */
  disabled?: boolean;
  /** Egyedi indoklás; alapból a szabvány hint. */
  reason?: string;
  className?: string;
  children: ReactNode;
}

/**
 * Stabil kérdőívlayout wrapper. Mindig ugyanazt a helyet foglalja, csak
 * az aktív/inaktív állapot vált. Irreleváns esetben a gyerekek disabled
 * állapotba kerülnek a `fieldset` révén, és halvány szürke overlayt kapnak.
 */
export function ConditionalField({
  relevant,
  disabled,
  reason = FIELD_NOT_RELEVANT_HINT,
  className,
  children,
}: Props) {
  const inactive = !relevant;
  return (
    <fieldset
      disabled={inactive || disabled}
      aria-disabled={inactive || disabled || undefined}
      data-relevant={relevant ? "true" : "false"}
      className={cn(
        "min-w-0 space-y-2 rounded-md transition-opacity",
        inactive && "bg-muted/40 opacity-70 cursor-not-allowed px-2 py-1 -mx-2 -my-1",
        className,
      )}
    >
      {children}
      {inactive && <p className="text-[11px] text-muted-foreground leading-snug">{reason}</p>}
    </fieldset>
  );
}
