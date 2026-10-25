namespace ArtMarketplace.Domain.Modules.Catalog;

public enum ArtworkRevisionStatus { Draft = 0, Frozen = 1, Discarded = 2 }

public sealed class ArtworkRevision
{
    private ArtworkRevision() { }

    public ArtworkRevision(Guid id, Guid artworkId, int number, Guid ownerUserId,
        string title, string artistAttribution, string description, Guid categoryId,
        int? year, string medium, string dimensions, string condition, DateTimeOffset now)
    {
        if (id == Guid.Empty || artworkId == Guid.Empty || ownerUserId == Guid.Empty || categoryId == Guid.Empty)
            throw new ArgumentException("Revision, artwork, owner, and category identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        Id = id; ArtworkId = artworkId; Number = number; CreatedByOwnerUserId = ownerUserId;
        Status = ArtworkRevisionStatus.Draft; CreatedAtUtc = now; Version = 0;
        Update(ownerUserId, title, artistAttribution, description, categoryId, year, medium, dimensions, condition, now);
    }

    public Guid Id { get; private set; }
    public Guid ArtworkId { get; private set; }
    public int Number { get; private set; }
    public Guid CreatedByOwnerUserId { get; private set; }
    public ArtworkRevisionStatus Status { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Guid? ArtistId { get; private set; }
    public string ArtistAttribution { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int? Year { get; private set; }
    public string Medium { get; private set; } = string.Empty;
    public string Dimensions { get; private set; } = string.Empty;
    public string Condition { get; private set; } = string.Empty;
    public string? DiscardReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? FrozenAtUtc { get; private set; }
    public long Version { get; private set; }

    public void Update(Guid actorUserId, string title, string artistAttribution, string description,
        Guid categoryId, int? year, string medium, string dimensions, string condition, DateTimeOffset now)
    {
        EnsureMutableBy(actorUserId);
        if (year is < 0 or > 3000) throw new ArgumentOutOfRangeException(nameof(year));
        Title = Required(title, 200); ArtistAttribution = Required(artistAttribution, 200);
        Description = Required(description, 5_000); CategoryId = categoryId;
        Year = year; Medium = Required(medium, 200); Dimensions = Required(dimensions, 200);
        Condition = Required(condition, 1_000); UpdatedAtUtc = now; AdvanceVersion();
    }

    public void SetArtist(Guid actorUserId, Guid? artistId)
    {
        EnsureMutableBy(actorUserId); ArtistId = artistId; AdvanceVersion();
    }

    public void Freeze(Guid actorUserId, DateTimeOffset now)
    {
        EnsureMutableBy(actorUserId); Status = ArtworkRevisionStatus.Frozen;
        FrozenAtUtc = now; AdvanceVersion();
    }

    public void Discard(Guid actorUserId, string reason, DateTimeOffset now)
    {
        EnsureMutableBy(actorUserId); DiscardReason = Required(reason, 500);
        Status = ArtworkRevisionStatus.Discarded; UpdatedAtUtc = now; AdvanceVersion();
    }

    private void EnsureMutableBy(Guid userId)
    {
        if (Status != ArtworkRevisionStatus.Draft) throw new InvalidOperationException("Only a draft revision can be changed.");
        if (CreatedByOwnerUserId != userId) throw new InvalidOperationException("This draft belongs to a previous owner.");
    }

    private void AdvanceVersion() => Version = checked(Version + 1);
    private static string Required(string value, int max)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 || normalized.Length > max)
            throw new ArgumentException($"Value must contain 1 to {max} characters.", nameof(value));
        return normalized;
    }
}
