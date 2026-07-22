namespace PatikaBeosztas.Domain;

public sealed class LeaveRequest
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public LeaveType Type { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public bool IsFullDay { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public LeaveRequestStatus Status { get; set; }

    public string? EmployeeNote { get; set; }

    public string? DecisionReason { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; private set; }

    public Employee? Employee { get; set; }

    public ICollection<LeaveStatusHistory> StatusHistory { get; } =
        new List<LeaveStatusHistory>();
}

public sealed class LeaveStatusHistory
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid LeaveRequestId { get; set; }

    public LeaveRequestStatus? FromStatus { get; set; }

    public LeaveRequestStatus ToStatus { get; set; }

    public Guid ActorUserId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? Reason { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }
}
