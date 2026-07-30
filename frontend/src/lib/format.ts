import { format, parseISO } from "date-fns";
import { hu } from "date-fns/locale";

// ─── Helyi (Europe/Budapest szemléletű) dátum-segédek ─────────────
// Fontos: soha ne használj toISOString().slice(0,10) helyi dátumhoz —
// az UTC-vé konvertál és nyáron egy nappal elcsúszhat.
function pad(n: number) {
  return n < 10 ? `0${n}` : String(n);
}

/** Local `YYYY-MM-DD` a megadott Date-ből (helyi időzóna szerint). */
export function toLocalDateISO(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export const fmtDate = (iso: string) => format(parseISO(iso), "yyyy. MMM d.", { locale: hu });
export const fmtDateShort = (iso: string) => format(parseISO(iso), "MM. dd.", { locale: hu });
export const fmtWeekday = (iso: string) => format(parseISO(iso), "EEEE", { locale: hu });
export const fmtDateTime = (iso: string) =>
  format(parseISO(iso), "yyyy. MMM d. HH:mm", { locale: hu });
export const fmtRelative = (iso: string) => {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.round(diff / 60000);
  if (m < 1) return "most";
  if (m < 60) return `${m} perce`;
  const h = Math.round(m / 60);
  if (h < 24) return `${h} órája`;
  const d = Math.round(h / 24);
  return `${d} napja`;
};

export const leaveTypeLabel = (t: string) => {
  switch (t) {
    case "annual_leave":
      return "Szabadság";
    case "sick_leave":
      return "Betegállomány";
    case "unpaid_leave":
      return "Fizetés nélküli szabadság";
    case "parental_leave":
      return "Szülési/szülői szabadság";
    case "other":
      return "Egyéb";
    default:
      return t;
  }
};

export const leaveStatusLabel = (s: string) => {
  switch (s) {
    case "draft":
      return "Piszkozat";
    case "pending":
      return "Függőben";
    case "approved":
      return "Jóváhagyva";
    case "rejected":
      return "Elutasítva";
    case "withdrawn":
      return "Visszavonva";
    case "cancelled":
      return "Törölve";
    case "reported":
      return "Bejelentve";
    case "recorded":
      return "Rögzítve";
    case "closed":
      return "Lezárva";
    default:
      return s;
  }
};

export const leaveActionLabel = (a: string) => {
  switch (a) {
    case "created":
      return "Létrehozva";
    case "approved":
      return "Jóváhagyva";
    case "rejected":
      return "Elutasítva";
    case "withdrawn":
      return "Visszavonva";
    case "cancelled":
      return "Törölve";
    case "reported":
      return "Bejelentve";
    default:
      return a;
  }
};

export const professionalRoleLabel = (r: string) => {
  switch (r) {
    case "pharmacy_manager":
      return "Gyógyszertárvezető";
    case "pharmacist":
      return "Gyógyszerész";
    case "specialist_assistant":
      return "Szakasszisztens";
    case "assistant":
      return "Asszisztens";
    case "pharmacist_trainee":
      return "Gyógyszerészgyakornok";
    case "assistant_trainee":
      return "Asszisztensgyakornok";
    case "cleaner":
      return "Takarító";
    case "finance_helper":
      return "Pénzügyi kisegítő";
    case "other":
      return "Egyéb";
    default:
      return r;
  }
};

export const capabilityLabel = (c: string) => {
  switch (c) {
    case "pharmacist":
      return "Gyógyszerész";
    case "specialist_pharmacist":
      return "Szakgyógyszerész";
    case "senior_assistant":
      return "Szakasszisztens";
    case "assistant":
      return "Asszisztens";
    case "cleaner":
      return "Takarító";
    case "finance":
      return "Pénzügyi";
    case "other":
      return "Egyéb";
    default:
      return c;
  }
};

export const timeTypeLabel = (t: string) => {
  switch (t) {
    case "work":
      return "Munkaidő";
    case "overtime":
      return "Túlóra";
    case "on_call":
      return "Ügyelet";
    case "standby":
      return "Készenlét";
    case "vacation":
      return "Szabadság";
    case "sick":
      return "Betegszabadság";
    case "unpaid":
      return "Fizetés nélküli szabadság";
    case "parental":
      return "Szülői szabadság";
    case "other":
      return "Egyéb";
    default:
      return t;
  }
};

export const openingModeLabel = (m: string) => {
  switch (m) {
    case "closed":
      return "Zárva";
    case "twentyFour":
      return "24 órás";
    case "custom":
      return "Egyedi";
    default:
      return m;
  }
};

export const shiftTemplateCategoryLabel = (c: string) => {
  switch (c) {
    case "AM":
      return "Délelőtt";
    case "PM":
      return "Délután";
    case "Long":
      return "Hosszú";
    case "Custom":
      return "Egyedi";
    default:
      return c;
  }
};

export const recurringRuleKindLabel = (k: string) => {
  switch (k) {
    case "Available":
      return "Elérhető";
    case "Preferred":
      return "Preferált";
    case "Avoid":
      return "Kerülendő";
    case "Unavailable":
      return "Nem elérhető";
    case "FixedTemplate":
      return "Rögzített alapminta";
    default:
      return k;
  }
};

export const quotaDimensionLabel = (d: string) => {
  switch (d) {
    case "AM":
      return "Délelőtt";
    case "PM":
      return "Délután";
    case "Sat":
      return "Szombat";
    case "Sun":
      return "Vasárnap";
    case "OnCall":
      return "Ügyelet";
    case "Standby":
      return "Készenlét";
    case "Long":
      return "Hosszú műszak";
    default:
      return d;
  }
};

export const weekdayLabel = (w: string) => {
  switch (w) {
    case "every":
      return "Minden nap";
    case "mon":
      return "Hétfő";
    case "tue":
      return "Kedd";
    case "wed":
      return "Szerda";
    case "thu":
      return "Csütörtök";
    case "fri":
      return "Péntek";
    case "sat":
      return "Szombat";
    case "sun":
      return "Vasárnap";
    default:
      return w;
  }
};

export const shiftTypeLabel = (t: string) => {
  switch (t) {
    case "work":
      return "Munka";
    case "on_call":
      return "Ügyelet";
    case "training":
      return "Képzés";
    case "meeting":
      return "Értekezlet";
    default:
      return t;
  }
};

export const notificationKindLabel = (k: string) => {
  switch (k) {
    case "shift_changed":
      return "Műszakváltozás";
    case "request_approved":
      return "Kérelem jóváhagyva";
    case "request_rejected":
      return "Kérelem elutasítva";
    case "approval_pending":
      return "Új jóváhagyási feladat";
    default:
      return k;
  }
};

export const scheduleRunStatusLabel = (s: string) => {
  switch (s) {
    case "Generating":
      return "Generálás folyamatban";
    case "Draft":
      return "Piszkozat";
    case "UnderReview":
      return "Ellenőrzés alatt";
    case "Approved":
      return "Jóváhagyva";
    case "Published":
      return "Közzétéve";
    case "Archived":
      return "Archivált";
    default:
      return s;
  }
};

export const issueKindLabel = (k: string) => {
  switch (k) {
    case "missing_pharmacist":
      return "Hiányzó gyógyszerész";
    case "missing_specialist_assistant":
      return "Hiányzó szakasszisztens";
    case "missing_assistant":
      return "Hiányzó asszisztens";
    case "multi_location_conflict":
      return "Több telephelyes ütközés";
    case "leave_conflict":
      return "Távollét-ütközés";
    case "daily_cap_exceeded":
      return "Napi keret túllépés";
    case "monthly_cap_exceeded":
      return "Havi keret túllépés";
    case "blocked_window_violation":
      return "Elérhetetlenség megsértése";
    case "preference_missed":
      return "Preferencia nem teljesült";
    case "pending_request_overlap":
      return "Függő kérelem érintett";
    case "inactive_location_used":
      return "Inaktív telephely";
    default:
      return "Egyéb figyelmeztetés";
  }
};

export const workspaceViewLabel = (v: string) => {
  switch (v) {
    case "employee":
      return "Dolgozói beosztás";
    case "coverage":
      return "Telephelyi lefedettség";
    case "issues":
      return "Problémák";
    default:
      return v;
  }
};

export const periodKindLabel = (p: string) => {
  switch (p) {
    case "week":
      return "Hét";
    case "biweek":
      return "Két hét";
    case "month":
      return "Hónap";
    default:
      return p;
  }
};

export function periodRange(
  anchor: string,
  kind: "week" | "biweek" | "month",
): { from: string; to: string } {
  if (kind === "month") {
    const d = parseISO(anchor);
    const from = new Date(d.getFullYear(), d.getMonth(), 1);
    const to = new Date(d.getFullYear(), d.getMonth() + 1, 0);
    return { from: toLocalDateISO(from), to: toLocalDateISO(to) };
  }
  const from = weekStartISO(parseISO(anchor));
  const days = kind === "biweek" ? 13 : 6;
  return { from, to: addDaysISO(from, days) };
}

export function shiftPeriod(
  anchor: string,
  kind: "week" | "biweek" | "month",
  direction: 1 | -1,
): string {
  if (kind === "month") {
    const d = parseISO(anchor);
    const next = new Date(d.getFullYear(), d.getMonth() + direction, 1);
    return toLocalDateISO(next);
  }
  const days = (kind === "biweek" ? 14 : 7) * direction;
  return addDaysISO(anchor, days);
}

export function eachDayISO(from: string, to: string): string[] {
  const out: string[] = [];
  let cur = from;
  while (cur <= to) {
    out.push(cur);
    cur = addDaysISO(cur, 1);
  }
  return out;
}

export function todayISO(offset = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  return toLocalDateISO(d);
}

export function weekStartISO(base = new Date()): string {
  const d = new Date(base);
  const day = (d.getDay() + 6) % 7; // 0 = hétfő
  d.setDate(d.getDate() - day);
  return toLocalDateISO(d);
}

export function addDaysISO(iso: string, days: number): string {
  const d = parseISO(iso);
  d.setDate(d.getDate() + days);
  return toLocalDateISO(d);
}

/** minutes → "H ó P p" ember-olvasható */
export function minutesToHuman(min: number): string {
  const h = Math.floor(min / 60);
  const m = min % 60;
  if (h && m) return `${h} ó ${m} p`;
  if (h) return `${h} ó`;
  return `${m} p`;
}
