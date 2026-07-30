import type { TaxAllowanceSurveyAnswers } from "@/services/types";

/**
 * Egy adott kérdőívmező relevancia-számítása.
 * A stabil UI-elrendezéshez: minden mező mindig a DOM-ban marad, de
 * a nem releváns mező disabled + kiszürkített állapotban jelenik meg.
 */
export type ConditionalField =
  | "marriageDate"
  | "fetusEligibilityMonth"
  | "otherEligiblePersonClaimsPart"
  | "motherAllowanceQualifyingChildrenCount"
  | "personalAllowanceStartMonth";

export function isFieldRelevant(field: ConditionalField, a: TaxAllowanceSurveyAnswers): boolean {
  switch (field) {
    case "marriageDate":
      return a.maritalStatus === "Married";
    case "fetusEligibilityMonth":
      return a.hasFetusAfterDay91 === true;
    case "otherEligiblePersonClaimsPart":
      return a.familyAllowanceClaimMode !== "NotRequested";
    case "motherAllowanceQualifyingChildrenCount":
      return a.isBiologicalOrAdoptiveMother === true;
    case "personalAllowanceStartMonth":
      return a.personalAllowanceEligibility === "Yes";
  }
}

export const FIELD_NOT_RELEVANT_HINT = "Az előző válasz alapján ez a mező jelenleg nem releváns.";

/**
 * Egyetlen normalizáló, amit AZONOS módon használ a mock és a HTTP service.
 * A cél: irreleváns mezők ne kerüljenek be a backend döntési logikájába.
 *
 * Nullable mezők → null.
 * Enum mezők → semleges default (No / None), a `emptyAnswers()`-hoz igazodva.
 * Releváns válaszokat NEM módosít.
 */
export function normalizeSurveyAnswersForSave(
  answers: TaxAllowanceSurveyAnswers,
): TaxAllowanceSurveyAnswers {
  const out: TaxAllowanceSurveyAnswers = { ...answers };

  if (!isFieldRelevant("marriageDate", out)) {
    out.marriageDate = null;
  }
  if (!isFieldRelevant("fetusEligibilityMonth", out)) {
    out.fetusEligibilityMonth = null;
  }
  if (!isFieldRelevant("personalAllowanceStartMonth", out)) {
    out.personalAllowanceStartMonth = null;
  }
  if (!isFieldRelevant("otherEligiblePersonClaimsPart", out)) {
    out.otherEligiblePersonClaimsPart = "No";
  }
  if (!isFieldRelevant("motherAllowanceQualifyingChildrenCount", out)) {
    out.motherAllowanceQualifyingChildrenCount = "None";
  }

  return out;
}
