namespace ArtMarketplace.Domain.Modules.Administration;

public sealed class ManagedContentPage
{
    private ManagedContentPage() { }
    public ManagedContentPage(Guid id, string slug, string title, string body, bool isPublished, Guid actorUserId, DateTimeOffset now)
    { if (id == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Content identifiers are required."); Id = id; Slug = Required(slug, 120); Title = Required(title, 200); Body = Required(body, 10_000); IsPublished = isPublished; UpdatedByUserId = actorUserId; UpdatedAtUtc = now; Version = 1; }
    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public bool IsPublished { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }
    public void Update(string title, string body, bool isPublished, Guid actorUserId, long expectedVersion, DateTimeOffset now)
    { if (Version != expectedVersion) throw new InvalidOperationException("Content changed; reload before saving."); Title = Required(title, 200); Body = Required(body, 10_000); IsPublished = isPublished; UpdatedByUserId = actorUserId; UpdatedAtUtc = now; Version = checked(Version + 1); }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}

public sealed class SiteBanner
{
    private SiteBanner() { }
    public SiteBanner(Guid id, string message, DateTimeOffset startsAtUtc, DateTimeOffset? endsAtUtc, bool isEnabled, Guid actorUserId, DateTimeOffset now)
    { if (id == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Banner identifiers are required."); if (endsAtUtc <= startsAtUtc) throw new ArgumentException("Banner end must be after its start."); Id = id; Message = Required(message, 500); StartsAtUtc = startsAtUtc; EndsAtUtc = endsAtUtc; IsEnabled = isEnabled; UpdatedByUserId = actorUserId; UpdatedAtUtc = now; Version = 1; }
    public Guid Id { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public bool IsEnabled { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}
