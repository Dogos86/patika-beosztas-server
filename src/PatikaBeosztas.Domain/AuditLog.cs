namespace PatikaBeosztas.Domain;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? ActorUserId { get; set; }

    public required string Action { get; set; }

    public required string EntityType { get; set; }

    public required string EntityId { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public required string CorrelationId { get; set; }

    public required string ChangeSummary { get; set; }
}
