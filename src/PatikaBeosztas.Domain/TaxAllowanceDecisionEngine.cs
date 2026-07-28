using System.Globalization;
using System.Text.RegularExpressions;

namespace PatikaBeosztas.Domain;

public sealed record TaxDeclarationSuggestion(
    TaxDeclarationType Type,
    bool Required,
    string Note);

public sealed record TaxAllowanceDecisionResult(
    string RuleSetVersion,
    bool NeedsClarification,
    IReadOnlyList<TaxDeclarationSuggestion> Suggestions);

public static class TaxAllowanceDecisionEngine
{
    public const string RuleSetVersion = "HU-2026.1";
    public const string FormVersion = "internal-survey-2026.1";
    public const string SourceMetadata =
        "Belső 2026-os adókedvezmény-felmérő; NAV 2026 adóelőleg-nyilatkozat tájékoztatók.";

    public static readonly DateOnly EffectiveTo = new(2026, 12, 31);

    public static TaxAllowanceDecisionResult Evaluate(
        TaxAllowanceSurvey survey,
        DateOnly? employeeBirthDate)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var clarification = HasUnknownOrConsultationAnswer(survey);
        var applyMonthly =
            survey.MonthlyAllowancePreference == MonthlyAllowancePreference.ApplyMonthly;

        var under25OptOut =
            survey.Under25AllowanceOptOut == Under25AllowanceOptOut.Yes;

        var firstMarriage =
            applyMonthly &&
            survey.MaritalStatus == MaritalStatus.Married &&
            survey.FirstMarriageStatus == SurveyAnswer.Yes &&
            survey.MarriageDate is not null;
        if (applyMonthly &&
            survey.MaritalStatus == MaritalStatus.Married &&
            (survey.FirstMarriageStatus == SurveyAnswer.Unknown ||
             survey.MarriageDate is null))
        {
            clarification = true;
        }

        var hasFamilyIndicator =
            survey.FamilyAllowanceEligibleChildrenCount > 0 ||
            survey.DependentStudentCount > 0 ||
            survey.HasFetusAfterDay91 ||
            survey.HasDisabledDependent ||
            survey.HasSharedCustodyChild;
        var familyAllowance =
            applyMonthly &&
            hasFamilyIndicator &&
            survey.FamilyAllowanceClaimMode != FamilyAllowanceClaimMode.NotRequested;

        var currentOwnChild =
            survey.HasCurrentOwnChildOrFetusEligibleForFamilyAllowance == SurveyAnswer.Yes;
        var under30Mother = false;
        if (applyMonthly &&
            survey.IsBiologicalOrAdoptiveMother &&
            currentOwnChild)
        {
            if (employeeBirthDate is null)
            {
                clarification = true;
            }
            else
            {
                under30Mother = IsUnderAgeAtStartOfTaxYear(
                    employeeBirthDate.Value,
                    survey.TaxYear,
                    30);
            }
        }

        var multiChildMother = false;
        if (applyMonthly && survey.IsBiologicalOrAdoptiveMother)
        {
            switch (survey.MotherAllowanceQualifyingChildrenCount)
            {
                case MotherAllowanceQualifyingChildrenCount.Three:
                case MotherAllowanceQualifyingChildrenCount.FourPlus:
                    multiChildMother = true;
                    break;
                case MotherAllowanceQualifyingChildrenCount.Two:
                    if (employeeBirthDate is null)
                    {
                        clarification = true;
                    }
                    else
                    {
                        multiChildMother = IsUnderAgeAtStartOfTaxYear(
                            employeeBirthDate.Value,
                            survey.TaxYear,
                            40);
                    }

                    break;
                case MotherAllowanceQualifyingChildrenCount.Unknown:
                    clarification = true;
                    break;
            }
        }

        var anyacska = multiChildMother && familyAllowance;
        var personalAllowance =
            applyMonthly &&
            survey.PersonalAllowanceEligibility == SurveyAnswer.Yes;

        var suggestions = new[]
        {
            Suggest(
                TaxDeclarationType.Under25OptOut,
                under25OptOut,
                "A 25 év alatti kedvezmény mellőzését a válasz alapján külön nyilatkozattal kell ellenőrizni."),
            Suggest(
                TaxDeclarationType.Under30Mother,
                under30Mother,
                "A 30 év alatti anyák kedvezményére utaló válasz; ez nem végleges jogosultsági döntés, hivatalos nyilatkozat szükséges."),
            Suggest(
                TaxDeclarationType.Anyacska,
                anyacska,
                "Összevont anyai és családi kedvezményi nyilatkozat javasolt; HR/bérszámfejtői ellenőrzés szükséges."),
            Suggest(
                TaxDeclarationType.MultiChildMotherAllowance,
                multiChildMother,
                "Több gyermeket nevelő anyák kedvezményére utaló válasz; ez nem végleges jogosultsági döntés."),
            Suggest(
                TaxDeclarationType.FamilyAllowance,
                familyAllowance,
                "Családi kedvezményhez kapcsolódó nyilatkozat javasolt a megadott belső válaszok alapján."),
            Suggest(
                TaxDeclarationType.FirstMarriage,
                firstMarriage,
                "Első házasok kedvezményéhez kapcsolódó nyilatkozat javasolt; a jogosultsági időszak ellenőrzendő."),
            Suggest(
                TaxDeclarationType.PersonalAllowance,
                personalAllowance,
                "Személyi kedvezményhez kapcsolódó nyilatkozat javasolt; diagnózist a rendszer nem kér és nem tárol.")
        };

        return new TaxAllowanceDecisionResult(
            RuleSetVersion,
            clarification,
            suggestions);
    }

    private static bool HasUnknownOrConsultationAnswer(TaxAllowanceSurvey survey) =>
        survey.MonthlyAllowancePreference == MonthlyAllowancePreference.NeedsConsultation ||
        survey.FirstMarriageStatus == SurveyAnswer.Unknown ||
        survey.OtherEligiblePersonClaimsPart == SurveyAnswer.Unknown ||
        survey.MotherAllowanceQualifyingChildrenCount ==
            MotherAllowanceQualifyingChildrenCount.Unknown ||
        survey.HasCurrentOwnChildOrFetusEligibleForFamilyAllowance == SurveyAnswer.Unknown ||
        survey.PersonalAllowanceEligibility == SurveyAnswer.Unknown ||
        survey.HasOtherEmployerOrRegularPayer == SurveyAnswer.Unknown ||
        survey.Under25AllowanceOptOut == Under25AllowanceOptOut.NeedsConsultation ||
        survey.FamilyAllowanceClaimMode == FamilyAllowanceClaimMode.Undecided ||
        survey.ForeignTaxResidencyOrSimilarForeignBenefit ==
            ForeignTaxResidencyOrSimilarForeignBenefit.PresentNeedsConsultation;

    private static bool IsUnderAgeAtStartOfTaxYear(
        DateOnly birthDate,
        int taxYear,
        int age) =>
        birthDate > new DateOnly(taxYear - age - 1, 12, 31);

    private static TaxDeclarationSuggestion Suggest(
        TaxDeclarationType type,
        bool required,
        string requiredNote) =>
        new(
            type,
            required,
            required
                ? requiredNote
                : "A belső felmérő válaszai alapján jelenleg nem javasolt külön nyilatkozat.");
}

public static partial class TaxAllowanceSurveyRules
{
    public static IReadOnlyList<DomainValidationIssue> Validate(TaxAllowanceSurvey survey)
    {
        ArgumentNullException.ThrowIfNull(survey);
        var issues = new List<DomainValidationIssue>();

        if (survey.TaxYear != 2026)
        {
            issues.Add(new(
                "TAX_SURVEY_YEAR_NOT_SUPPORTED",
                "Ebben a verzióban csak a 2026-os adóév támogatott."));
        }

        if (survey.EffectiveFrom.Year != survey.TaxYear)
        {
            issues.Add(new(
                "TAX_SURVEY_EFFECTIVE_DATE_INVALID",
                "A hatály kezdete a felmérés adóévébe essen."));
        }

        if (survey.EffectiveTo is not null &&
            (survey.EffectiveTo.Value.Year != survey.TaxYear ||
             survey.EffectiveTo < survey.EffectiveFrom))
        {
            issues.Add(new(
                "TAX_SURVEY_EFFECTIVE_TO_INVALID",
                "A hatály vége az adóévbe essen, és ne előzze meg a hatály kezdetét."));
        }

        if (string.IsNullOrWhiteSpace(survey.SourceMetadata) ||
            survey.SourceMetadata.Length > 500)
        {
            issues.Add(new(
                "TAX_SURVEY_SOURCE_METADATA_INVALID",
                "A szabályforrás metaadata 1–500 karakter hosszú legyen."));
        }

        if (survey.FamilyAllowanceEligibleChildrenCount is < 0 or > 20)
        {
            issues.Add(new(
                "FAMILY_ELIGIBLE_CHILD_COUNT_INVALID",
                "A családi kedvezményre jogosító gyermekek száma 0 és 20 között lehet."));
        }

        if (survey.DependentStudentCount is < 0 or > 20)
        {
            issues.Add(new(
                "DEPENDENT_STUDENT_COUNT_INVALID",
                "Az eltartott tanulók száma 0 és 20 között lehet."));
        }

        ValidateMonth(
            survey.FetusEligibilityMonth,
            survey.TaxYear,
            "FETUS_ELIGIBILITY_MONTH_INVALID",
            issues);
        ValidateMonth(
            survey.PersonalAllowanceStartMonth,
            survey.TaxYear,
            "PERSONAL_ALLOWANCE_START_MONTH_INVALID",
            issues);

        if (!survey.HasFetusAfterDay91 &&
            !string.IsNullOrWhiteSpace(survey.FetusEligibilityMonth))
        {
            issues.Add(new(
                "FETUS_ELIGIBILITY_MONTH_NOT_APPLICABLE",
                "Magzati jogosultsági hónap csak a 91. napot elért magzat jelölésekor adható meg."));
        }

        if (survey.PersonalAllowanceEligibility == SurveyAnswer.No &&
            !string.IsNullOrWhiteSpace(survey.PersonalAllowanceStartMonth))
        {
            issues.Add(new(
                "PERSONAL_ALLOWANCE_START_MONTH_NOT_APPLICABLE",
                "Kezdő hónap csak jelzett személyi kedvezményi érintettségnél adható meg."));
        }

        if (survey.HrPayrollNote?.Length > 1000)
        {
            issues.Add(new(
                "HR_PAYROLL_NOTE_TOO_LONG",
                "A HR/bérszámfejtési megjegyzés legfeljebb 1000 karakter lehet."));
        }

        return issues;
    }

    private static void ValidateMonth(
        string? value,
        int taxYear,
        string code,
        List<DomainValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!MonthPattern().IsMatch(value) ||
            !DateOnly.TryParseExact(
                $"{value}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month) ||
            month.Year != taxYear)
        {
            issues.Add(new(
                code,
                "A hónap formátuma YYYY-MM legyen, és a felmérés adóévébe essen."));
        }
    }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant)]
    private static partial Regex MonthPattern();
}

public static class PayrollOnboardingRules
{
    public static IReadOnlyList<DomainValidationIssue> ValidateProfile(
        string employeeNumber,
        string taxIdentificationNumber,
        string? payrollExternalId)
    {
        var issues = new List<DomainValidationIssue>();
        if (string.IsNullOrWhiteSpace(employeeNumber) ||
            employeeNumber.Trim().Length > 50)
        {
            issues.Add(new(
                "EMPLOYEE_NUMBER_INVALID",
                "A dolgozói törzsszám 1–50 karakter hosszú legyen."));
        }

        var normalizedTaxIdentifier = new string(
            taxIdentificationNumber.Where(char.IsDigit).ToArray());
        if (normalizedTaxIdentifier.Length != 10 ||
            normalizedTaxIdentifier.Length != taxIdentificationNumber.Trim().Length)
        {
            issues.Add(new(
                "TAX_IDENTIFICATION_NUMBER_INVALID",
                "Az adóazonosító jel pontosan 10 számjegyből álljon."));
        }

        if (payrollExternalId?.Trim().Length > 100)
        {
            issues.Add(new(
                "PAYROLL_EXTERNAL_ID_TOO_LONG",
                "A külső bérszámfejtési azonosító legfeljebb 100 karakter lehet."));
        }

        return issues;
    }

    public static bool CanTransitionSurvey(
        TaxAllowanceSurveyStatus from,
        TaxAllowanceSurveyStatus to) =>
        (from, to) switch
        {
            (TaxAllowanceSurveyStatus.Draft, TaxAllowanceSurveyStatus.Submitted) => true,
            (TaxAllowanceSurveyStatus.Draft, TaxAllowanceSurveyStatus.NeedsClarification) => true,
            (TaxAllowanceSurveyStatus.Submitted, TaxAllowanceSurveyStatus.Draft) => true,
            (TaxAllowanceSurveyStatus.NeedsClarification, TaxAllowanceSurveyStatus.Draft) => true,
            (TaxAllowanceSurveyStatus.Reviewed, TaxAllowanceSurveyStatus.Draft) => true,
            (TaxAllowanceSurveyStatus.Submitted, TaxAllowanceSurveyStatus.Reviewed) => true,
            (TaxAllowanceSurveyStatus.NeedsClarification, TaxAllowanceSurveyStatus.Reviewed) => true,
            (TaxAllowanceSurveyStatus.Reviewed, TaxAllowanceSurveyStatus.Completed) => true,
            (_, TaxAllowanceSurveyStatus.Cancelled) when from != TaxAllowanceSurveyStatus.Completed => true,
            _ => false
        };

    public static bool CanTransitionRequirement(
        TaxDeclarationRequirementStatus from,
        TaxDeclarationRequirementStatus to) =>
        (from, to) switch
        {
            (TaxDeclarationRequirementStatus.Required, TaxDeclarationRequirementStatus.ToSend) => true,
            (TaxDeclarationRequirementStatus.ToSend, TaxDeclarationRequirementStatus.Sent) => true,
            (TaxDeclarationRequirementStatus.Sent, TaxDeclarationRequirementStatus.ReceivedOnya) => true,
            (TaxDeclarationRequirementStatus.Sent, TaxDeclarationRequirementStatus.ReceivedPaper) => true,
            (TaxDeclarationRequirementStatus.ReceivedOnya, TaxDeclarationRequirementStatus.Verified) => true,
            (TaxDeclarationRequirementStatus.ReceivedPaper, TaxDeclarationRequirementStatus.Verified) => true,
            (TaxDeclarationRequirementStatus.Verified, TaxDeclarationRequirementStatus.Applied) => true,
            (TaxDeclarationRequirementStatus.ReceivedOnya, TaxDeclarationRequirementStatus.Rejected) => true,
            (TaxDeclarationRequirementStatus.ReceivedPaper, TaxDeclarationRequirementStatus.Rejected) => true,
            (TaxDeclarationRequirementStatus.Rejected, TaxDeclarationRequirementStatus.ToSend) => true,
            (_, TaxDeclarationRequirementStatus.Expired)
                when from is not TaxDeclarationRequirementStatus.NotRequired and
                    not TaxDeclarationRequirementStatus.Applied => true,
            _ => false
        };
}
