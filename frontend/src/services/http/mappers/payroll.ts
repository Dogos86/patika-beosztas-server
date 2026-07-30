import type {
  AdminUpdateSurveyInput,
  OverrideDeclarationInput,
  OwnCreateSurveyInput,
  OwnUpdateSurveyInput,
  UpdateDeclarationStatusInput,
  UpdatePayrollProfileInput,
} from "@/services/interfaces";
import type {
  CreateTaxAllowanceSurveyRequestDto,
  OverrideTaxDeclarationRequirementRequestDto,
  UpdateAdminTaxAllowanceSurveyRequestDto,
  UpdateEmployeePayrollProfileRequestDto,
  UpdateOwnTaxAllowanceSurveyRequestDto,
  UpdateTaxDeclarationStatusRequestDto,
} from "../dto/payroll";
import { normalizeSurveyAnswersForSave } from "@/lib/survey-relevance";

export function mapProfileUpdateRequest(
  input: UpdatePayrollProfileInput,
): UpdateEmployeePayrollProfileRequestDto {
  return {
    employeeNumber: input.employeeNumber,
    taxIdentificationNumber: input.taxIdentificationNumber,
    employmentStartDate: input.employmentStartDate,
    payrollExternalId: input.payrollExternalId,
    status: input.status,
    expectedVersion: input.expectedVersion,
  };
}

export function mapAdminSurveyUpdateRequest(
  input: AdminUpdateSurveyInput,
): UpdateAdminTaxAllowanceSurveyRequestDto {
  return {
    effectiveFrom: input.effectiveFrom,
    answers: normalizeSurveyAnswersForSave(input.answers),
    hrPayrollNote: input.hrPayrollNote,
    expectedVersion: input.expectedVersion,
  };
}

export function mapOwnSurveyCreateRequest(
  input: OwnCreateSurveyInput,
): CreateTaxAllowanceSurveyRequestDto {
  return {
    taxYear: input.taxYear,
    effectiveFrom: input.effectiveFrom,
    answers: normalizeSurveyAnswersForSave(input.answers),
  };
}

export function mapOwnSurveyUpdateRequest(
  input: OwnUpdateSurveyInput,
): UpdateOwnTaxAllowanceSurveyRequestDto {
  return {
    effectiveFrom: input.effectiveFrom,
    answers: normalizeSurveyAnswersForSave(input.answers),
    expectedVersion: input.expectedVersion,
  };
}

export function mapDeclarationStatusRequest(
  input: UpdateDeclarationStatusInput,
): UpdateTaxDeclarationStatusRequestDto {
  return { ...input };
}

export function mapDeclarationOverrideRequest(
  input: OverrideDeclarationInput,
): OverrideTaxDeclarationRequirementRequestDto {
  return { ...input };
}
