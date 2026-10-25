namespace ArtMarketplace.Infrastructure.Persistence;

public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent()
    {
    }

    public SecurityAuditEvent(
        Guid id,
        string eventType,
        Guid actorUserId,
        Guid subjectUserId,
        string dataJson,
        string? traceId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        EventType = eventType;
        ActorUserId = actorUserId;
        SubjectUserId = subjectUserId;
        DataJson = dataJson;
        TraceId = traceId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public Guid ActorUserId { get; private set; }

    public Guid SubjectUserId { get; private set; }

    public string DataJson { get; private set; } = string.Empty;

    public string? TraceId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
