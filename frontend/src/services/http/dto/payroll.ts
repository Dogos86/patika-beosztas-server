// Phase 2D — HR és bérszámfejtési onboarding DTO-k a
// contracts/openapi.phase2d.json alapján. Backend PascalCase megőrizve.

export type PayrollProfileStatus = "Draft" | "UnderReview" | "Complete" | "Archived";

export type SurveyStatus =
  "Draft" | "Submitted" | "NeedsClarification" | "Reviewed" | "Completed" | "Cancelled";

export type SurveyAnswer = "Yes" | "No" | "Unknown";

export type MonthlyAllowancePreference = "ApplyMonthly" | "AnnualReturnOnly" | "NeedsConsultation";
export type MaritalStatus = "Single" | "Married" | "Partnership" | "Divorced" | "Widowed" | "Other";
export type FamilyAllowanceClaimMode = "NotRequested" | "Alone" | "Shared" | "Undecided";
export type MotherAllowanceQualifyingChildrenCount =
  "None" | "One" | "Two" | "Three" | "FourPlus" | "Unknown";
export type Under25AllowanceOptOut = "No" | "Yes" | "NeedsConsultation";
export type ForeignTaxResidencyOrSimilarForeignBenefit = "None" | "PresentNeedsConsultation";

export type DeclarationRequirementStatus =
  | "NotRequired"
  | "Required"
  | "ToSend"
  | "Sent"
  | "ReceivedOnya"
  | "ReceivedPaper"
  | "Verified"
  | "Applied"
  | "Rejected"
  | "Expired";

export type DeclarationType =
  | "Under25OptOut"
  | "Under30Mother"
  | "Anyacska"
  | "MultiChildMotherAllowance"
  | "FamilyAllowance"
  | "FirstMarriage"
  | "PersonalAllowance";

export interface TaxAllowanceSurveyAnswersDto {
  monthlyAllowancePreference: MonthlyAllowancePreference;
  maritalStatus: MaritalStatus;
  marriageDate: string | null;
  firstMarriageStatus: SurveyAnswer;
  familyAllowanceEligibleChildrenCount: number;
  dependentStudentCount: number;
  hasFetusAfterDay91: boolean;
  fetusEligibilityMonth: string | null;
  hasDisabledDependent: boolean;
  hasSharedCustodyChild: boolean;
  familyAllowanceClaimMode: FamilyAllowanceClaimMode;
  otherEligiblePersonClaimsPart: SurveyAnswer;
  isBiologicalOrAdoptiveMother: boolean;
  motherAllowanceQualifyingChildrenCount: MotherAllowanceQualifyingChildrenCount;
  hasCurrentOwnChildOrFetusEligibleForFamilyAllowance: SurveyAnswer;
  personalAllowanceEligibility: SurveyAnswer;
  personalAllowanceStartMonth: string | null;
  hasOtherEmployerOrRegularPayer: SurveyAnswer;
  under25AllowanceOptOut: Under25AllowanceOptOut;
  foreignTaxResidencyOrSimilarForeignBenefit: ForeignTaxResidencyOrSimilarForeignBenefit;
}

export interface EmployeePayrollProfileResponseDto {
  id: string;
  employeeId: string;
  employeeNumber: string;
  maskedTaxIdentificationNumber: string;
  taxIdentificationNumber: string | null;
  employmentStartDate: string;
  payrollExternalId: string | null;
  status: PayrollProfileStatus;
  version: number;
  createdAtUtc?: string;
  createdByUserId?: string;
  updatedAtUtc?: string;
  updatedByUserId?: string;
}

export interface UpdateEmployeePayrollProfileRequestDto {
  employeeNumber: string;
  taxIdentificationNumber: string | null;
  employmentStartDate: string;
  payrollExternalId: string | null;
  status: PayrollProfileStatus;
  expectedVersion: number | null;
}

export interface TaxDeclarationRequirementResponseDto {
  id: string;
  employeeId: string;
  surveyId: string;
  type: DeclarationType;
  requiredDecision: boolean;
  status: DeclarationRequirementStatus;
  effectiveFrom: string;
  effectiveTo: string | null;
  notes: string | null;
  generatedByRuleVersion: string;
  manualOverride: boolean;
  manualOverrideReason: string | null;
  version: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface TaxAllowanceSurveyResponseDto {
  id: string;
  employeeId: string;
  taxYear: number;
  formVersion: string;
  ruleSetVersion: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  sourceMetadata: string;
  status: SurveyStatus;
  answers: TaxAllowanceSurveyAnswersDto;
  declaredAtUtc: string | null;
  declaredByUserId: string | null;
  reviewedAtUtc: string | null;
  reviewedByUserId: string | null;
  hrPayrollNote: string | null;
  declarationRequirements: TaxDeclarationRequirementResponseDto[];
  version: number;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface CreateTaxAllowanceSurveyRequestDto {
  taxYear: number;
  effectiveFrom: string;
  answers: TaxAllowanceSurveyAnswersDto;
}

export interface UpdateOwnTaxAllowanceSurveyRequestDto {
  effectiveFrom: string;
  answers: TaxAllowanceSurveyAnswersDto;
  expectedVersion: number;
}

export interface UpdateAdminTaxAllowanceSurveyRequestDto {
  effectiveFrom: string;
  answers: TaxAllowanceSurveyAnswersDto;
  hrPayrollNote: string | null;
  expectedVersion: number | null;
}

export interface TaxSurveyVersionRequestDto {
  expectedVersion: number;
}

export interface ReviewTaxAllowanceSurveyRequestDto {
  hrPayrollNote: string | null;
  expectedVersion: number;
}

export interface UpdateTaxDeclarationStatusRequestDto {
  status: DeclarationRequirementStatus;
  effectiveTo: string | null;
  notes: string | null;
  expectedVersion: number;
}

export interface OverrideTaxDeclarationRequirementRequestDto {
  requiredDecision: boolean;
  status: DeclarationRequirementStatus;
  reason: string;
  effectiveTo: string | null;
  expectedVersion: number;
}

export interface CompletePayrollOnboardingRequestDto {
  expectedProfileVersion: number;
}

export interface PayrollOnboardingSummaryResponseDto {
  employeeId: string;
  employeeDisplayName: string;
  payrollProfile: EmployeePayrollProfileResponseDto | null;
  latestSurvey: TaxAllowanceSurveyResponseDto | null;
  requiredDeclarationCount: number;
  outstandingDeclarationCount: number;
  isComplete: boolean;
}
