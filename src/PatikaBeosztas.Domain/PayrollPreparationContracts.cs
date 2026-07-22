namespace PatikaBeosztas.Domain;

public readonly record struct WorkPremiumTag(string Value);

public readonly record struct PayrollCode(string Value);

public sealed record PayrollAssignmentSegment(
    AssignmentSegment Assignment,
    PayrollCode? PayrollCode,
    IReadOnlySet<WorkPremiumTag> PremiumTags);
