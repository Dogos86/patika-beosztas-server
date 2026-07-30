import { describe, it, expect } from "vitest";
import { isFieldRelevant, normalizeSurveyAnswersForSave } from "./survey-relevance";
import { emptyAnswers } from "@/services/mock/payroll";
import type { TaxAllowanceSurveyAnswers } from "@/services/types";

function fullyPopulated(): TaxAllowanceSurveyAnswers {
  return {
    ...emptyAnswers(),
    maritalStatus: "Married",
    marriageDate: "2024-05-01",
    hasFetusAfterDay91: true,
    fetusEligibilityMonth: "2025-06",
    familyAllowanceClaimMode: "Shared",
    otherEligiblePersonClaimsPart: "Yes",
    isBiologicalOrAdoptiveMother: true,
    motherAllowanceQualifyingChildrenCount: "Three",
    personalAllowanceEligibility: "Yes",
    personalAllowanceStartMonth: "2025-01",
  };
}

describe("isFieldRelevant", () => {
  it("marriageDate csak Married állapotnál releváns", () => {
    const a = { ...emptyAnswers(), maritalStatus: "Single" as const };
    expect(isFieldRelevant("marriageDate", a)).toBe(false);
    expect(isFieldRelevant("marriageDate", { ...a, maritalStatus: "Married" })).toBe(true);
  });
  it("fetusEligibilityMonth csak hasFetusAfterDay91 esetén releváns", () => {
    expect(isFieldRelevant("fetusEligibilityMonth", emptyAnswers())).toBe(false);
    expect(
      isFieldRelevant("fetusEligibilityMonth", { ...emptyAnswers(), hasFetusAfterDay91: true }),
    ).toBe(true);
  });
  it("otherEligiblePersonClaimsPart csak, ha claim mode != NotRequested", () => {
    expect(isFieldRelevant("otherEligiblePersonClaimsPart", emptyAnswers())).toBe(false);
    expect(
      isFieldRelevant("otherEligiblePersonClaimsPart", {
        ...emptyAnswers(),
        familyAllowanceClaimMode: "Alone",
      }),
    ).toBe(true);
  });
  it("motherAllowanceQualifyingChildrenCount csak anyáknál", () => {
    expect(isFieldRelevant("motherAllowanceQualifyingChildrenCount", emptyAnswers())).toBe(false);
    expect(
      isFieldRelevant("motherAllowanceQualifyingChildrenCount", {
        ...emptyAnswers(),
        isBiologicalOrAdoptiveMother: true,
      }),
    ).toBe(true);
  });
  it("personalAllowanceStartMonth csak Yes esetén releváns", () => {
    expect(isFieldRelevant("personalAllowanceStartMonth", emptyAnswers())).toBe(false);
    expect(
      isFieldRelevant("personalAllowanceStartMonth", {
        ...emptyAnswers(),
        personalAllowanceEligibility: "Yes",
      }),
    ).toBe(true);
  });
});

describe("normalizeSurveyAnswersForSave", () => {
  it("Single esetén marriageDate = null lesz", () => {
    const a = { ...emptyAnswers(), maritalStatus: "Single" as const, marriageDate: "2020-01-01" };
    expect(normalizeSurveyAnswersForSave(a).marriageDate).toBeNull();
  });
  it("hasFetusAfterDay91=false esetén fetusEligibilityMonth = null", () => {
    const a = { ...emptyAnswers(), fetusEligibilityMonth: "2025-06" };
    expect(normalizeSurveyAnswersForSave(a).fetusEligibilityMonth).toBeNull();
  });
  it("personalAllowanceEligibility != Yes esetén personalAllowanceStartMonth = null", () => {
    const a = {
      ...emptyAnswers(),
      personalAllowanceEligibility: "Unknown" as const,
      personalAllowanceStartMonth: "2025-03",
    };
    expect(normalizeSurveyAnswersForSave(a).personalAllowanceStartMonth).toBeNull();
  });
  it("nem releváns enum mezők semleges alapértékre állnak (No / None)", () => {
    const a = {
      ...emptyAnswers(),
      otherEligiblePersonClaimsPart: "Yes" as const,
      motherAllowanceQualifyingChildrenCount: "Three" as const,
    };
    const n = normalizeSurveyAnswersForSave(a);
    expect(n.otherEligiblePersonClaimsPart).toBe("No");
    expect(n.motherAllowanceQualifyingChildrenCount).toBe("None");
  });
  it("releváns válaszokat NEM módosít", () => {
    const a = fullyPopulated();
    expect(normalizeSurveyAnswersForSave(a)).toEqual(a);
  });
  it("idempotens", () => {
    const a = fullyPopulated();
    const once = normalizeSurveyAnswersForSave(a);
    const twice = normalizeSurveyAnswersForSave(once);
    expect(twice).toEqual(once);
  });
});
