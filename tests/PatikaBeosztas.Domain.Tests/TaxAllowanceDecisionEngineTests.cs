using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Domain.Tests;

[TestClass]
public sealed class TaxAllowanceDecisionEngineTests
{
    [TestMethod]
    public void Under25OptOutProducesOnlyTheRequestedDeclaration()
    {
        var survey = ValidSurvey();
        survey.Under25AllowanceOptOut = Under25AllowanceOptOut.Yes;

        var result = TaxAllowanceDecisionEngine.Evaluate(survey, new DateOnly(2004, 1, 1));

        Assert.IsTrue(Required(result, TaxDeclarationType.Under25OptOut));
        Assert.IsFalse(Required(result, TaxDeclarationType.PersonalAllowance));
        Assert.IsFalse(result.NeedsClarification);
    }

    [TestMethod]
    public void FirstMarriageNeedsMarriedFirstMarriageAndConcreteDate()
    {
        var survey = ValidSurvey();
        survey.MaritalStatus = MaritalStatus.Married;
        survey.FirstMarriageStatus = SurveyAnswer.Yes;
        survey.MarriageDate = new DateOnly(2025, 12, 10);

        var result = TaxAllowanceDecisionEngine.Evaluate(survey, new DateOnly(1990, 1, 1));

        Assert.IsTrue(Required(result, TaxDeclarationType.FirstMarriage));
        survey.MarriageDate = null;
        result = TaxAllowanceDecisionEngine.Evaluate(survey, new DateOnly(1990, 1, 1));
        Assert.IsFalse(Required(result, TaxDeclarationType.FirstMarriage));
        Assert.IsTrue(result.NeedsClarification);
    }

    [TestMethod]
    public void ChildStudentFetusAndClaimModeProduceFamilyAllowanceSuggestion()
    {
        var survey = ValidSurvey();
        survey.FamilyAllowanceEligibleChildrenCount = 1;
        survey.DependentStudentCount = 1;
        survey.HasFetusAfterDay91 = true;
        survey.FetusEligibilityMonth = "2026-03";
        survey.FamilyAllowanceClaimMode = FamilyAllowanceClaimMode.Shared;

        var result = TaxAllowanceDecisionEngine.Evaluate(survey, new DateOnly(1990, 1, 1));

        Assert.IsTrue(Required(result, TaxDeclarationType.FamilyAllowance));
    }

    [TestMethod]
    public void Under30MotherUsesEmployeeBirthDateWithoutFinalEligibilityClaim()
    {
        var survey = ValidSurvey();
        survey.IsBiologicalOrAdoptiveMother = true;
        survey.HasCurrentOwnChildOrFetusEligibleForFamilyAllowance = SurveyAnswer.Yes;

        var young = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1996, 1, 1));
        var older = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1995, 12, 31));

        Assert.IsTrue(Required(young, TaxDeclarationType.Under30Mother));
        Assert.IsFalse(Required(older, TaxDeclarationType.Under30Mother));
        StringAssert.Contains(
            Suggestion(young, TaxDeclarationType.Under30Mother).Note,
            "nem végleges");
    }

    [TestMethod]
    public void TwoThreeAndFourPlusChildMotherScenariosAreVersionedAndReproducible()
    {
        var survey = ValidSurvey();
        survey.IsBiologicalOrAdoptiveMother = true;
        survey.MotherAllowanceQualifyingChildrenCount =
            MotherAllowanceQualifyingChildrenCount.Two;

        var eligibleTwo = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1986, 1, 1));
        var ineligibleTwo = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1985, 12, 31));
        survey.MotherAllowanceQualifyingChildrenCount =
            MotherAllowanceQualifyingChildrenCount.Three;
        var three = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1970, 1, 1));
        survey.MotherAllowanceQualifyingChildrenCount =
            MotherAllowanceQualifyingChildrenCount.FourPlus;
        var fourPlus = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1970, 1, 1));

        Assert.IsTrue(Required(eligibleTwo, TaxDeclarationType.MultiChildMotherAllowance));
        Assert.IsFalse(Required(ineligibleTwo, TaxDeclarationType.MultiChildMotherAllowance));
        Assert.IsTrue(Required(three, TaxDeclarationType.MultiChildMotherAllowance));
        Assert.IsTrue(Required(fourPlus, TaxDeclarationType.MultiChildMotherAllowance));
        Assert.AreEqual("HU-2026.1", fourPlus.RuleSetVersion);
    }

    [TestMethod]
    public void AnyacskaRequiresBothMultiChildMotherAndFamilyAllowanceIndicators()
    {
        var survey = ValidSurvey();
        survey.IsBiologicalOrAdoptiveMother = true;
        survey.MotherAllowanceQualifyingChildrenCount =
            MotherAllowanceQualifyingChildrenCount.Three;
        survey.FamilyAllowanceEligibleChildrenCount = 2;
        survey.FamilyAllowanceClaimMode = FamilyAllowanceClaimMode.Alone;

        var result = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1980, 1, 1));

        Assert.IsTrue(Required(result, TaxDeclarationType.MultiChildMotherAllowance));
        Assert.IsTrue(Required(result, TaxDeclarationType.FamilyAllowance));
        Assert.IsTrue(Required(result, TaxDeclarationType.Anyacska));
    }

    [TestMethod]
    public void PersonalAllowanceIndicatorDoesNotCaptureDiagnosis()
    {
        var survey = ValidSurvey();
        survey.PersonalAllowanceEligibility = SurveyAnswer.Yes;
        survey.PersonalAllowanceStartMonth = "2026-02";

        var result = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1990, 1, 1));

        Assert.IsTrue(Required(result, TaxDeclarationType.PersonalAllowance));
        StringAssert.Contains(
            Suggestion(result, TaxDeclarationType.PersonalAllowance).Note,
            "diagnózist");
    }

    [TestMethod]
    public void UnknownAndForeignTaxAnswersRequireClarification()
    {
        var unknown = ValidSurvey();
        unknown.PersonalAllowanceEligibility = SurveyAnswer.Unknown;
        var foreign = ValidSurvey();
        foreign.ForeignTaxResidencyOrSimilarForeignBenefit =
            ForeignTaxResidencyOrSimilarForeignBenefit.PresentNeedsConsultation;

        Assert.IsTrue(TaxAllowanceDecisionEngine
            .Evaluate(unknown, new DateOnly(1990, 1, 1))
            .NeedsClarification);
        Assert.IsTrue(TaxAllowanceDecisionEngine
            .Evaluate(foreign, new DateOnly(1990, 1, 1))
            .NeedsClarification);
    }

    [TestMethod]
    public void AnnualReturnPreferenceDoesNotSuggestMonthlyDeclarations()
    {
        var survey = ValidSurvey();
        survey.MonthlyAllowancePreference =
            MonthlyAllowancePreference.AnnualReturnOnly;
        survey.PersonalAllowanceEligibility = SurveyAnswer.Yes;
        survey.FamilyAllowanceEligibleChildrenCount = 3;
        survey.FamilyAllowanceClaimMode = FamilyAllowanceClaimMode.Alone;

        var result = TaxAllowanceDecisionEngine.Evaluate(
            survey,
            new DateOnly(1990, 1, 1));

        Assert.IsFalse(Required(result, TaxDeclarationType.PersonalAllowance));
        Assert.IsFalse(Required(result, TaxDeclarationType.FamilyAllowance));
    }

    [TestMethod]
    public void SurveyValidationRejectsUnsupportedYearCountsAndInconsistentMonths()
    {
        var survey = ValidSurvey();
        survey.TaxYear = 2027;
        survey.FamilyAllowanceEligibleChildrenCount = -1;
        survey.DependentStudentCount = 21;
        survey.HasFetusAfterDay91 = false;
        survey.FetusEligibilityMonth = "2027-13";
        survey.PersonalAllowanceEligibility = SurveyAnswer.No;
        survey.PersonalAllowanceStartMonth = "2027-01";

        var issues = TaxAllowanceSurveyRules.Validate(survey);

        var expectedCodes = new[]
        {
            "TAX_SURVEY_YEAR_NOT_SUPPORTED",
            "TAX_SURVEY_EFFECTIVE_DATE_INVALID",
            "FAMILY_ELIGIBLE_CHILD_COUNT_INVALID",
            "DEPENDENT_STUDENT_COUNT_INVALID",
            "FETUS_ELIGIBILITY_MONTH_INVALID",
            "FETUS_ELIGIBILITY_MONTH_NOT_APPLICABLE",
            "PERSONAL_ALLOWANCE_START_MONTH_NOT_APPLICABLE"
        };
        CollectionAssert.IsSubsetOf(
            expectedCodes,
            issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void SurveyValidationRequiresVersionSourceAndOrderedEffectiveDates()
    {
        var survey = ValidSurvey();
        survey.SourceMetadata = string.Empty;
        survey.EffectiveTo = new DateOnly(2025, 12, 31);

        var issues = TaxAllowanceSurveyRules.Validate(survey);

        Assert.IsTrue(issues.Any(issue =>
            issue.Code == "TAX_SURVEY_SOURCE_METADATA_INVALID"));
        Assert.IsTrue(issues.Any(issue =>
            issue.Code == "TAX_SURVEY_EFFECTIVE_TO_INVALID"));
    }

    [TestMethod]
    public void SurveyAndDeclarationStateMachinesRejectShortcuts()
    {
        Assert.IsTrue(PayrollOnboardingRules.CanTransitionSurvey(
            TaxAllowanceSurveyStatus.Submitted,
            TaxAllowanceSurveyStatus.Reviewed));
        Assert.IsFalse(PayrollOnboardingRules.CanTransitionSurvey(
            TaxAllowanceSurveyStatus.Submitted,
            TaxAllowanceSurveyStatus.Completed));
        Assert.IsTrue(PayrollOnboardingRules.CanTransitionRequirement(
            TaxDeclarationRequirementStatus.Sent,
            TaxDeclarationRequirementStatus.ReceivedOnya));
        Assert.IsFalse(PayrollOnboardingRules.CanTransitionRequirement(
            TaxDeclarationRequirementStatus.Required,
            TaxDeclarationRequirementStatus.Applied));
    }

    private static bool Required(
        TaxAllowanceDecisionResult result,
        TaxDeclarationType type) =>
        Suggestion(result, type).Required;

    private static TaxDeclarationSuggestion Suggestion(
        TaxAllowanceDecisionResult result,
        TaxDeclarationType type) =>
        result.Suggestions.Single(item => item.Type == type);

    private static TaxAllowanceSurvey ValidSurvey() =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            TaxYear = 2026,
            FormVersion = TaxAllowanceDecisionEngine.FormVersion,
            RuleSetVersion = TaxAllowanceDecisionEngine.RuleSetVersion,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = TaxAllowanceDecisionEngine.EffectiveTo,
            SourceMetadata = TaxAllowanceDecisionEngine.SourceMetadata,
            Status = TaxAllowanceSurveyStatus.Draft,
            MonthlyAllowancePreference = MonthlyAllowancePreference.ApplyMonthly,
            MaritalStatus = MaritalStatus.Single,
            FirstMarriageStatus = SurveyAnswer.No,
            FamilyAllowanceEligibleChildrenCount = 0,
            DependentStudentCount = 0,
            FamilyAllowanceClaimMode = FamilyAllowanceClaimMode.NotRequested,
            OtherEligiblePersonClaimsPart = SurveyAnswer.No,
            MotherAllowanceQualifyingChildrenCount =
                MotherAllowanceQualifyingChildrenCount.None,
            HasCurrentOwnChildOrFetusEligibleForFamilyAllowance = SurveyAnswer.No,
            PersonalAllowanceEligibility = SurveyAnswer.No,
            HasOtherEmployerOrRegularPayer = SurveyAnswer.No,
            Under25AllowanceOptOut = Under25AllowanceOptOut.No,
            ForeignTaxResidencyOrSimilarForeignBenefit =
                ForeignTaxResidencyOrSimilarForeignBenefit.None
        };
}
