import type {
  DeclarationRequirementStatus,
  DeclarationType,
  FamilyAllowanceClaimMode,
  ForeignTaxResidencyOrSimilarForeignBenefit,
  MaritalStatus,
  MonthlyAllowancePreference,
  MotherAllowanceQualifyingChildrenCount,
  PayrollProfileStatus,
  SurveyAnswer,
  SurveyStatus,
  Under25AllowanceOptOut,
} from "@/services/types";

export const payrollProfileStatusLabel = (s: PayrollProfileStatus): string =>
  ({
    Draft: "Piszkozat",
    UnderReview: "Ellenőrzés alatt",
    Complete: "Kész",
    Archived: "Archivált",
  })[s] ?? s;

export const surveyStatusLabel = (s: SurveyStatus): string =>
  ({
    Draft: "Piszkozat",
    Submitted: "Beadva",
    NeedsClarification: "Pontosításra vár",
    Reviewed: "Ellenőrizve",
    Completed: "Lezárva",
    Cancelled: "Törölve",
  })[s] ?? s;

export const surveyAnswerLabel = (a: SurveyAnswer): string =>
  ({ Yes: "Igen", No: "Nem", Unknown: "Nem tudom" })[a] ?? a;

export const monthlyAllowancePreferenceLabel = (m: MonthlyAllowancePreference): string =>
  ({
    ApplyMonthly: "Havi érvényesítés",
    AnnualReturnOnly: "Csak éves bevallás",
    NeedsConsultation: "Konzultáció szükséges",
  })[m] ?? m;

export const maritalStatusLabel = (m: MaritalStatus): string =>
  ({
    Single: "Egyedülálló",
    Married: "Házas",
    Partnership: "Élettársi kapcsolat",
    Divorced: "Elvált",
    Widowed: "Özvegy",
    Other: "Egyéb",
  })[m] ?? m;

export const familyAllowanceClaimModeLabel = (m: FamilyAllowanceClaimMode): string =>
  ({
    NotRequested: "Nem kér",
    Alone: "Egyedül",
    Shared: "Megosztva",
    Undecided: "Még nem döntött",
  })[m] ?? m;

export const motherChildCountLabel = (m: MotherAllowanceQualifyingChildrenCount): string =>
  ({
    None: "Nincs",
    One: "1",
    Two: "2",
    Three: "3",
    FourPlus: "4 vagy több",
    Unknown: "Nem tudom",
  })[m] ?? m;

export const under25OptOutLabel = (u: Under25AllowanceOptOut): string =>
  ({ No: "Nem", Yes: "Igen (lemond)", NeedsConsultation: "Konzultáció szükséges" })[u] ?? u;

export const foreignTaxLabel = (f: ForeignTaxResidencyOrSimilarForeignBenefit): string =>
  ({ None: "Nincs", PresentNeedsConsultation: "Van — konzultáció szükséges" })[f] ?? f;

export const declarationTypeLabel = (t: DeclarationType): string =>
  ({
    Under25OptOut: "25 alatti — lemondó nyilatkozat",
    Under30Mother: "30 alatti anyák kedvezménye",
    Anyacska: "Anyák kedvezménye",
    MultiChildMotherAllowance: "Négy vagy több gyermeket nevelő anyák",
    FamilyAllowance: "Családi kedvezmény",
    FirstMarriage: "Első házasok kedvezménye",
    PersonalAllowance: "Személyi kedvezmény",
  })[t] ?? t;

export const declarationStatusLabel = (s: DeclarationRequirementStatus): string =>
  ({
    NotRequired: "Nem szükséges",
    Required: "Szükséges",
    ToSend: "Küldendő",
    Sent: "Elküldve",
    ReceivedOnya: "Beérkezett (ONYA)",
    ReceivedPaper: "Beérkezett (papír)",
    Verified: "Ellenőrizve",
    Applied: "Érvényesítve",
    Rejected: "Elutasítva",
    Expired: "Lejárt",
  })[s] ?? s;
