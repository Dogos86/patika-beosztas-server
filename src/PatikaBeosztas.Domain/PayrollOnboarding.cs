namespace PatikaBeosztas.Domain;

public sealed class EmployeePayrollProfile
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public required string EmployeeNumber { get; set; }

    public required string TaxIdentificationNumberCiphertext { get; set; }

    public required string TaxIdentificationNumberHash { get; set; }

    public DateOnly EmploymentStartDate { get; set; }

    public string? PayrollExternalId { get; set; }

    public EmployeePayrollProfileStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public uint Version { get; private set; }

    public Employee? Employee { get; set; }
}

public sealed class TaxAllowanceSurvey
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public int TaxYear { get; set; }

    public required string FormVersion { get; set; }

    public required string RuleSetVersion { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public required string SourceMetadata { get; set; }

    public TaxAllowanceSurveyStatus Status { get; set; }

    public MonthlyAllowancePreference MonthlyAllowancePreference { get; set; }

    public MaritalStatus MaritalStatus { get; set; }

    public DateOnly? MarriageDate { get; set; }

    public SurveyAnswer FirstMarriageStatus { get; set; }

    public int FamilyAllowanceEligibleChildrenCount { get; set; }

    public int DependentStudentCount { get; set; }

    public bool HasFetusAfterDay91 { get; set; }

    public string? FetusEligibilityMonth { get; set; }

    public bool HasDisabledDependent { get; set; }

    public bool HasSharedCustodyChild { get; set; }

    public FamilyAllowanceClaimMode FamilyAllowanceClaimMode { get; set; }

    public SurveyAnswer OtherEligiblePersonClaimsPart { get; set; }

    public bool IsBiologicalOrAdoptiveMother { get; set; }

    public MotherAllowanceQualifyingChildrenCount MotherAllowanceQualifyingChildrenCount { get; set; }

    public SurveyAnswer HasCurrentOwnChildOrFetusEligibleForFamilyAllowance { get; set; }

    public SurveyAnswer PersonalAllowanceEligibility { get; set; }

    public string? PersonalAllowanceStartMonth { get; set; }

    public SurveyAnswer HasOtherEmployerOrRegularPayer { get; set; }

    public Under25AllowanceOptOut Under25AllowanceOptOut { get; set; }

    public ForeignTaxResidencyOrSimilarForeignBenefit ForeignTaxResidencyOrSimilarForeignBenefit { get; set; }

    public DateTimeOffset? DeclaredAtUtc { get; set; }

    public Guid? DeclaredByUserId { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public string? HrPayrollNote { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public uint Version { get; private set; }

    public Employee? Employee { get; set; }

    public ICollection<TaxDeclarationRequirement> DeclarationRequirements { get; } =
        new List<TaxDeclarationRequirement>();
}

public sealed class TaxDeclarationRequirement
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid SurveyId { get; set; }

    public TaxDeclarationType Type { get; set; }

    public bool RequiredDecision { get; set; }

    public TaxDeclarationRequirementStatus Status { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string? Notes { get; set; }

    public required string GeneratedByRuleVersion { get; set; }

    public bool ManualOverride { get; set; }

    public string? ManualOverrideReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public uint Version { get; private set; }

    public Employee? Employee { get; set; }

    public TaxAllowanceSurvey? Survey { get; set; }
}
