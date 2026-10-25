namespace ArtMarketplace.Domain.Modules.Identity;

public sealed class SellerEligibilityInvalidation
{
    private readonly List<SellerEligibilityInvalidationResolution> _resolutions = [];

    private SellerEligibilityInvalidation()
    {
    }

    public SellerEligibilityInvalidation(
        Guid id,
        Guid userId,
        long invalidatedVersion,
        string reason,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        InvalidatedVersion = invalidatedVersion;
        Reason = reason;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public long InvalidatedVersion { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<SellerEligibilityInvalidationResolution> Resolutions => _resolutions.AsReadOnly();

    public bool Covers(long snapshotVersion) => snapshotVersion < InvalidatedVersion;

    public SellerEligibilityInvalidationResolution ResolveTarget(
        string targetType,
        Guid targetId,
        Guid actorUserId,
        string reason,
        string evidence,
        DateTimeOffset now)
    {
        if (_resolutions.Any(item => item.TargetType == targetType && item.TargetId == targetId))
        {
            throw new InvalidOperationException("This target already has an append-only resolution.");
        }

        var resolution = new SellerEligibilityInvalidationResolution(
            Guid.NewGuid(), Id, targetType, targetId, actorUserId, reason, evidence, now);
        _resolutions.Add(resolution);
        return resolution;
    }
}
