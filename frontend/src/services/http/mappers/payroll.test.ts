import { describe, it, expect } from "vitest";
import {
  mapAdminSurveyUpdateRequest,
  mapDeclarationOverrideRequest,
  mapProfileUpdateRequest,
} from "./payroll";
import type { TaxAllowanceSurveyAnswers } from "@/services/types";

const answers: TaxAllowanceSurveyAnswers = {
  monthlyAllowancePreference: "ApplyMonthly",
  maritalStatus: "Single",
  marriageDate: null,
  firstMarriageStatus: "No",
  familyAllowanceEligibleChildrenCount: 0,
  dependentStudentCount: 0,
  hasFetusAfterDay91: false,
  fetusEligibilityMonth: null,
  hasDisabledDependent: false,
  hasSharedCustodyChild: false,
  familyAllowanceClaimMode: "NotRequested",
  otherEligiblePersonClaimsPart: "No",
  isBiologicalOrAdoptiveMother: false,
  motherAllowanceQualifyingChildrenCount: "None",
  hasCurrentOwnChildOrFetusEligibleForFamilyAllowance: "No",
  personalAllowanceEligibility: "No",
  personalAllowanceStartMonth: null,
  hasOtherEmployerOrRegularPayer: "No",
  under25AllowanceOptOut: "No",
  foreignTaxResidencyOrSimilarForeignBenefit: "None",
};

describe("payroll mappers", () => {
  it("profil update kérés kanonikus", () => {
    const dto = mapProfileUpdateRequest({
      employeeNumber: "E-001",
      taxIdentificationNumber: "8123456789",
      employmentStartDate: "2024-01-01",
      payrollExternalId: null,
      status: "Complete",
      expectedVersion: 3,
    });
    expect(dto.expectedVersion).toBe(3);
    expect(dto.status).toBe("Complete");
  });
  it("admin survey update átadja expectedVersion-t (nullable)", () => {
    const dto = mapAdminSurveyUpdateRequest({
      effectiveFrom: "2026-01-01",
      answers,
      hrPayrollNote: null,
      expectedVersion: null,
    });
    expect(dto.expectedVersion).toBeNull();
    expect(dto.answers.monthlyAllowancePreference).toBe("ApplyMonthly");
  });
  it("declaration override kérés viszi az okot", () => {
    const dto = mapDeclarationOverrideRequest({
      requiredDecision: false,
      status: "NotRequired",
      reason: "Adminisztratív felülbírálás",
      effectiveTo: null,
      expectedVersion: 2,
    });
    expect(dto.reason).toBe("Adminisztratív felülbírálás");
  });
});
