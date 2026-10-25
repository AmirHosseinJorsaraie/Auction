namespace ArtMarketplace.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid AggregateId { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public int Attempts { get; set; }
}
