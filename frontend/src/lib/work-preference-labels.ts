import type { WorkPreferenceType } from "@/services/types";

/** Magyar címkék a generátor szempontjából megkülönböztethető típusokhoz. */
export const WORK_PREFERENCE_TYPE_LABELS: Record<WorkPreferenceType, string> = {
  Available: "Elérhető",
  Preferred: "Preferált",
  Avoid: "Kerülendő",
  Unavailable: "Nem elérhető",
  Fixed: "Rögzített",
};

/** Rövid magyarázat: kívánság vagy kötött generátori bemenet. */
export const WORK_PREFERENCE_TYPE_HINTS: Record<WorkPreferenceType, string> = {
  Available: "Beosztható időszak.",
  Preferred: "Optimalizálási kívánság — a generátor törekszik rá.",
  Avoid: "Optimalizálási kívánság — a generátor kerüli.",
  Unavailable: "Erős generátori bemenet — nem osztható be.",
  Fixed: "Erős generátori bemenet — kötött műszak.",
};

export const WORK_PREFERENCE_TYPES: WorkPreferenceType[] = [
  "Available",
  "Preferred",
  "Avoid",
  "Unavailable",
  "Fixed",
];

export function workPreferenceTypeLabel(t: WorkPreferenceType): string {
  return WORK_PREFERENCE_TYPE_LABELS[t];
}
