using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts;

public sealed record UpdateEmployeePayrollProfileRequest(
    string EmployeeNumber,
    string? TaxIdentificationNumber,
    DateOnly EmploymentStartDate,
    string? PayrollExternalId,
    EmployeePayrollProfileStatus Status,
    uint? ExpectedVersion);

public sealed record EmployeePayrollProfileResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string MaskedTaxIdentificationNumber,
    string? TaxIdentificationNumber,
    DateOnly EmploymentStartDate,
    string? PayrollExternalId,
    EmployeePayrollProfileStatus Status,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    Guid CreatedByUserId,
    DateTimeOffset UpdatedAtUtc,
    Guid UpdatedByUserId);

public sealed record TaxAllowanceSurveyAnswers(
    MonthlyAllowancePreference MonthlyAllowancePreference,
    MaritalStatus MaritalStatus,
    DateOnly? MarriageDate,
    SurveyAnswer FirstMarriageStatus,
    int FamilyAllowanceEligibleChildrenCount,
    int DependentStudentCount,
    bool HasFetusAfterDay91,
    string? FetusEligibilityMonth,
    bool HasDisabledDependent,
    bool HasSharedCustodyChild,
    FamilyAllowanceClaimMode FamilyAllowanceClaimMode,
    SurveyAnswer OtherEligiblePersonClaimsPart,
    bool IsBiologicalOrAdoptiveMother,
    MotherAllowanceQualifyingChildrenCount MotherAllowanceQualifyingChildrenCount,
    SurveyAnswer HasCurrentOwnChildOrFetusEligibleForFamilyAllowance,
    SurveyAnswer PersonalAllowanceEligibility,
    string? PersonalAllowanceStartMonth,
    SurveyAnswer HasOtherEmployerOrRegularPayer,
    Under25AllowanceOptOut Under25AllowanceOptOut,
    ForeignTaxResidencyOrSimilarForeignBenefit ForeignTaxResidencyOrSimilarForeignBenefit);

public sealed record CreateTaxAllowanceSurveyRequest(
    int TaxYear,
    DateOnly EffectiveFrom,
    TaxAllowanceSurveyAnswers Answers);

public sealed record UpdateTaxAllowanceSurveyRequest(
    DateOnly EffectiveFrom,
    TaxAllowanceSurveyAnswers Answers,
    string? HrPayrollNote,
    uint? ExpectedVersion);

public sealed record UpdateOwnTaxAllowanceSurveyRequest(
    DateOnly EffectiveFrom,
    TaxAllowanceSurveyAnswers Answers,
    uint ExpectedVersion);

public sealed record TaxSurveyVersionRequest(uint ExpectedVersion);

public sealed record ReviewTaxAllowanceSurveyRequest(
    string? HrPayrollNote,
    uint ExpectedVersion);

public sealed record TaxDeclarationRequirementResponse(
    Guid Id,
    Guid EmployeeId,
    Guid SurveyId,
    TaxDeclarationType Type,
    bool RequiredDecision,
    TaxDeclarationRequirementStatus Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Notes,
    string GeneratedByRuleVersion,
    bool ManualOverride,
    string? ManualOverrideReason,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TaxAllowanceSurveyResponse(
    Guid Id,
    Guid EmployeeId,
    int TaxYear,
    string FormVersion,
    string RuleSetVersion,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceMetadata,
    TaxAllowanceSurveyStatus Status,
    TaxAllowanceSurveyAnswers Answers,
    DateTimeOffset? DeclaredAtUtc,
    Guid? DeclaredByUserId,
    DateTimeOffset? ReviewedAtUtc,
    Guid? ReviewedByUserId,
    string? HrPayrollNote,
    IReadOnlyList<TaxDeclarationRequirementResponse> DeclarationRequirements,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdateTaxDeclarationStatusRequest(
    TaxDeclarationRequirementStatus Status,
    DateOnly? EffectiveTo,
    string? Notes,
    uint ExpectedVersion);

public sealed record OverrideTaxDeclarationRequirementRequest(
    bool RequiredDecision,
    TaxDeclarationRequirementStatus Status,
    string Reason,
    DateOnly? EffectiveTo,
    uint ExpectedVersion);

public sealed record CompletePayrollOnboardingRequest(uint ExpectedProfileVersion);

public sealed record PayrollOnboardingSummaryResponse(
    Guid EmployeeId,
    string EmployeeDisplayName,
    EmployeePayrollProfileResponse? PayrollProfile,
    TaxAllowanceSurveyResponse? LatestSurvey,
    int RequiredDeclarationCount,
    int OutstandingDeclarationCount,
    bool IsComplete);

public sealed record PayrollExportEmployeeV1(
    Guid EmployeeId,
    string DisplayName,
    string EmployeeNumber,
    DateOnly EmploymentStartDate,
    string? PayrollExternalId);

public sealed record PayrollExportSurveyV1(
    int TaxYear,
    string FormVersion,
    string RuleSetVersion,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceMetadata,
    TaxAllowanceSurveyStatus Status,
    MonthlyAllowancePreference MonthlyAllowancePreference,
    DateTimeOffset? DeclaredAtUtc,
    DateTimeOffset? ReviewedAtUtc);

public sealed record PayrollExportDeclarationV1(
    TaxDeclarationType Type,
    bool RequiredDecision,
    TaxDeclarationRequirementStatus Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record PayrollOnboardingExportV1(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    PayrollExportEmployeeV1 Employee,
    PayrollExportSurveyV1? Survey,
    IReadOnlyList<PayrollExportDeclarationV1> DeclarationRequirements,
    EmployeePayrollProfileStatus ProfileStatus,
    bool OnboardingComplete);
