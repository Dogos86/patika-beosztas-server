using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Infrastructure.Persistence;

public sealed class AuditWriter(
    PatikaDbContext dbContext,
    TimeProvider timeProvider)
{
    public void Add(
        Guid organizationId,
        Guid? actorUserId,
        string action,
        string entityType,
        string entityId,
        string correlationId,
        string changeSummary)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TimestampUtc = timeProvider.GetUtcNow(),
            CorrelationId = correlationId,
            ChangeSummary = changeSummary
        });
    }
}
