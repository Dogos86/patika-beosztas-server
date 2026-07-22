using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Contracts.Payroll;

public sealed record WorkPremiumTag(string Code);

public sealed record PayrollCode(string Code);

public sealed record AssignmentSegment(
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeType TimeType,
    PayrollCode? PayrollCode,
    IReadOnlyList<WorkPremiumTag> PremiumTags);
