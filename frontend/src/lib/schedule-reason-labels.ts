// A generátor magyarázat- és probléma-kódok magyar címkéi. Ismeretlen
// kódnál a nyers kódot mutatjuk (ne mondjuk el a felhasználónak, hogy
// "unknown"), így az új backend-kódok is olvashatóan jelennek meg.

const REASON: Record<string, string> = {
  PreferredWindowMatch: "Preferált időablak illeszkedik",
  AvoidWindowRespected: "Kerülendő időablak figyelembe véve",
  TargetHoursOnTrack: "Havi óracél teljesül",
  QuotaTargetMet: "Kvóta cél teljesül",
  LongShiftPreference: "Hosszú műszak preferálva",
  LeaveOverlap: "Szabadság ütközés",
  BlockingCoverageShortage: "Blokkoló lefedettségi hiány",
  WarningCoverageShortage: "Figyelmeztető lefedettségi hiány",
  MultiLocationConflict: "Több telephely ütközés",
  BlockedWindowViolation: "Kerülendő időablak sérült",
  OvertimeCap: "Túlóra plafon",
  WeekendFairness: "Hétvégi terhelés kiegyensúlyozva",
  EveningFairness: "Esti terhelés kiegyensúlyozva",
  LocationChangePenalty: "Telephelyváltás büntetés",
  PendingLeaveOverlap: "Függő szabadság átfedés",
  PreserveAcceptedDecision: "Elfogadott döntés megőrzése",
  PreviousScheduleChange: "Előző beosztáshoz képest változás",
};

export function reasonLabel(code: string): string {
  return REASON[code] ?? code;
}

const ISSUE: Record<string, string> = {
  MissingPharmacist: "Hiányzó gyógyszerész",
  MissingSpecialistAssistant: "Hiányzó szakasszisztens",
  MissingAssistant: "Hiányzó asszisztens",
  MultiLocationConflict: "Több telephely ütközés",
  LeaveConflict: "Szabadság ütközés",
  DailyCapExceeded: "Napi keret túllépve",
  MonthlyCapExceeded: "Havi keret túllépve",
  BlockedWindowViolation: "Kerülendő időablak sérült",
  PreferenceMissed: "Preferencia figyelmen kívül hagyva",
  PendingRequestOverlap: "Függő kérelem átfedés",
  InactiveLocationUsed: "Inaktív telephely használva",
};

export function issueLabel(code: string): string {
  return ISSUE[code] ?? code;
}
