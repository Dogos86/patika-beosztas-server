/**
 * Adószám / adóazonosító maszkolás megjelenítéshez.
 * A backend `maskedTaxIdentificationNumber`-t is szállít; ez akkor kell,
 * ha lokálisan gépelés közben szeretnénk maszkolt vizuális összegzést mutatni.
 * Csak az utolsó 3 karakter marad látható, a többi „•".
 */
export function maskTaxId(raw: string | null | undefined): string {
  if (!raw) return "";
  const trimmed = raw.trim();
  if (trimmed.length <= 3) return trimmed;
  const visible = trimmed.slice(-3);
  const hidden = "•".repeat(Math.max(0, trimmed.length - 3));
  return `${hidden}${visible}`;
}

/** Rövid, „kb. 3 karakter" formátum a listákhoz. */
export function maskTaxIdCompact(raw: string | null | undefined): string {
  if (!raw) return "";
  const t = raw.trim();
  if (t.length <= 3) return t;
  return `•••${t.slice(-3)}`;
}
