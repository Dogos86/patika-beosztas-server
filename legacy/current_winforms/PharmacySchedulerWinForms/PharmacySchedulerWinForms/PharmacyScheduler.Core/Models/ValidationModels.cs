namespace PharmacyScheduler.Core.Models;

public sealed class ValidationIssue
{
    public Severity Severity { get; set; } = Severity.Soft;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ShiftEntryId { get; set; }

    public override string ToString() => $"[{Severity.ToDisplayText()}] {Message}";
}

public sealed class ValidationReport
{
    public List<ValidationIssue> Issues { get; } = new();

    public bool HasBlockingIssues => Issues.Any(x => x.Severity == Severity.Hard);

    public void Add(ValidationIssue issue) => Issues.Add(issue);
}
