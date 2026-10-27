namespace ArtMarketplace.Infrastructure.Identity;

public sealed class BffSession
{
    public Guid Id { get; set; }

    public Guid? SubjectUserId { get; set; }

    public string Surface { get; set; } = string.Empty;

    public string HandleDigest { get; set; } = string.Empty;

    public byte[] ProtectedTicket { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public long Version { get; set; }
}
