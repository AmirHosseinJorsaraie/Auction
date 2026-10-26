namespace ArtMarketplace.Domain.Modules.Catalog;

public sealed class Artwork
{
    private Artwork() { }

    public Artwork(Guid id, Guid ownerUserId, string internalCode, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || ownerUserId == Guid.Empty)
            throw new ArgumentException("Artwork and owner identifiers are required.");

        Id = id;
        CurrentOwnerUserId = ownerUserId;
        InternalCode = Required(internalCode, 64);
        CreatedAtUtc = createdAtUtc;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid CurrentOwnerUserId { get; private set; }
    public string InternalCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public long Version { get; private set; }

    public void EnsureOwnedBy(Guid userId)
    {
        if (CurrentOwnerUserId != userId)
            throw new InvalidOperationException("Only the current artwork owner may perform this operation.");
    }

    public void TransferOwnership(Guid fromUserId, Guid toUserId)
    {
        EnsureOwnedBy(fromUserId);
        if (toUserId == Guid.Empty || toUserId == fromUserId) throw new InvalidOperationException("A different new owner is required.");
        CurrentOwnerUserId = toUserId; Version = checked(Version + 1);
    }

    private static string Required(string value, int max)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 || normalized.Length > max)
            throw new ArgumentException($"Value must contain 1 to {max} characters.", nameof(value));
        return normalized;
    }
}
