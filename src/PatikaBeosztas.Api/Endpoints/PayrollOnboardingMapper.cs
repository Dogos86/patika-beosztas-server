using PatikaBeosztas.Application.Security;
using PatikaBeosztas.Contracts;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Api.Endpoints;

internal static class PayrollOnboardingMapper
{
    public static EmployeePayrollProfileResponse MapProfile(
        EmployeePayrollProfile profile,
        ITaxIdentifierProtector protector,
        bool includeSensitive)
    {
        var taxIdentifier = protector.Unprotect(
            profile.TaxIdentificationNumberCiphertext);
        return new EmployeePayrollProfileResponse(
            profile.Id,
            profile.EmployeeId,
            profile.EmployeeNumber,
            protector.Mask(taxIdentifier),
            includeSensitive ? taxIdentifier : null,
            profile.EmploymentStartDate,
            profile.PayrollExternalId,
            profile.Status,
            profile.Version,
            profile.CreatedAtUtc,
            profile.CreatedByUserId,
            profile.UpdatedAtUtc,
            profile.UpdatedByUserId);
    }

    public static TaxAllowanceSurveyResponse MapSurvey(TaxAllowanceSurvey survey) =>
        new(
            survey.Id,
            survey.EmployeeId,
            survey.TaxYear,
            survey.FormVersion,
            survey.RuleSetVersion,
            survey.EffectiveFrom,
            survey.EffectiveTo,
            survey.SourceMetadata,
            survey.Status,
            MapAnswers(survey),
            survey.DeclaredAtUtc,
            survey.DeclaredByUserId,
            survey.ReviewedAtUtc,
            survey.ReviewedByUserId,
            survey.HrPayrollNote,
            survey.DeclarationRequirements
                .OrderBy(requirement => requirement.Type)
                .Select(MapRequirement)
                .ToArray(),
            survey.Version,
            survey.CreatedAtUtc,
            survey.UpdatedAtUtc);

    public static TaxDeclarationRequirementResponse MapRequirement(
        TaxDeclarationRequirement requirement) =>
        new(
            requirement.Id,
            requirement.EmployeeId,
            requirement.SurveyId,
            requirement.Type,
            requirement.RequiredDecision,
            requirement.Status,
            requirement.EffectiveFrom,
            requirement.EffectiveTo,
            requirement.Notes,
            requirement.GeneratedByRuleVersion,
            requirement.ManualOverride,
            requirement.ManualOverrideReason,
            requirement.Version,
            requirement.CreatedAtUtc,
            requirement.UpdatedAtUtc);

    public static TaxAllowanceSurvey CreateSurvey(
        Guid organizationId,
        Guid employeeId,
        Guid actorUserId,
        int taxYear,
        DateOnly effectiveFrom,
        TaxAllowanceSurveyAnswers answers,
        DateTimeOffset now)
    {
        var survey = new TaxAllowanceSurvey
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EmployeeId = employeeId,
            TaxYear = taxYear,
            FormVersion = TaxAllowanceDecisionEngine.FormVersion,
            RuleSetVersion = TaxAllowanceDecisionEngine.RuleSetVersion,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = TaxAllowanceDecisionEngine.EffectiveTo,
            SourceMetadata = TaxAllowanceDecisionEngine.SourceMetadata,
            Status = TaxAllowanceSurveyStatus.Draft,
            CreatedAtUtc = now,
            CreatedByUserId = actorUserId,
            UpdatedAtUtc = now,
            UpdatedByUserId = actorUserId
        };
        ApplyAnswers(survey, answers);
        return survey;
    }

    public static void ApplyAnswers(
        TaxAllowanceSurvey survey,
        TaxAllowanceSurveyAnswers answers)
    {
        survey.MonthlyAllowancePreference = answers.MonthlyAllowancePreference;
        survey.MaritalStatus = answers.MaritalStatus;
        survey.MarriageDate = answers.MarriageDate;
        survey.FirstMarriageStatus = answers.FirstMarriageStatus;
        survey.FamilyAllowanceEligibleChildrenCount =
            answers.FamilyAllowanceEligibleChildrenCount;
        survey.DependentStudentCount = answers.DependentStudentCount;
        survey.HasFetusAfterDay91 = answers.HasFetusAfterDay91;
        survey.FetusEligibilityMonth = NormalizeOptional(
            answers.FetusEligibilityMonth);
        survey.HasDisabledDependent = answers.HasDisabledDependent;
        survey.HasSharedCustodyChild = answers.HasSharedCustodyChild;
        survey.FamilyAllowanceClaimMode = answers.FamilyAllowanceClaimMode;
        survey.OtherEligiblePersonClaimsPart = answers.OtherEligiblePersonClaimsPart;
        survey.IsBiologicalOrAdoptiveMother = answers.IsBiologicalOrAdoptiveMother;
        survey.MotherAllowanceQualifyingChildrenCount =
            answers.MotherAllowanceQualifyingChildrenCount;
        survey.HasCurrentOwnChildOrFetusEligibleForFamilyAllowance =
            answers.HasCurrentOwnChildOrFetusEligibleForFamilyAllowance;
        survey.PersonalAllowanceEligibility = answers.PersonalAllowanceEligibility;
        survey.PersonalAllowanceStartMonth = NormalizeOptional(
            answers.PersonalAllowanceStartMonth);
        survey.HasOtherEmployerOrRegularPayer = answers.HasOtherEmployerOrRegularPayer;
        survey.Under25AllowanceOptOut = answers.Under25AllowanceOptOut;
        survey.ForeignTaxResidencyOrSimilarForeignBenefit =
            answers.ForeignTaxResidencyOrSimilarForeignBenefit;
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TaxAllowanceSurveyAnswers MapAnswers(TaxAllowanceSurvey survey) =>
        new(
            survey.MonthlyAllowancePreference,
            survey.MaritalStatus,
            survey.MarriageDate,
            survey.FirstMarriageStatus,
            survey.FamilyAllowanceEligibleChildrenCount,
            survey.DependentStudentCount,
            survey.HasFetusAfterDay91,
            survey.FetusEligibilityMonth,
            survey.HasDisabledDependent,
            survey.HasSharedCustodyChild,
            survey.FamilyAllowanceClaimMode,
            survey.OtherEligiblePersonClaimsPart,
            survey.IsBiologicalOrAdoptiveMother,
            survey.MotherAllowanceQualifyingChildrenCount,
            survey.HasCurrentOwnChildOrFetusEligibleForFamilyAllowance,
            survey.PersonalAllowanceEligibility,
            survey.PersonalAllowanceStartMonth,
            survey.HasOtherEmployerOrRegularPayer,
            survey.Under25AllowanceOptOut,
            survey.ForeignTaxResidencyOrSimilarForeignBenefit);
}
