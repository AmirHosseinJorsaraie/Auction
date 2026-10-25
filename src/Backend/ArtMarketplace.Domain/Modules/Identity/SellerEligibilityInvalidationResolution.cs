namespace ArtMarketplace.Domain.Modules.Identity;

public sealed class SellerEligibilityInvalidationResolution
{
    private SellerEligibilityInvalidationResolution()
    {
    }

    internal SellerEligibilityInvalidationResolution(
        Guid id,
        Guid invalidationId,
        string targetType,
        Guid targetId,
        Guid actorUserId,
        string reason,
        string evidence,
        DateTimeOffset createdAtUtc)
    {
        if (targetId == Guid.Empty || actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Target and actor identifiers are required.");
        }

        Id = id;
        InvalidationId = invalidationId;
        TargetType = Require(targetType, 80);
        TargetId = targetId;
        ActorUserId = actorUserId;
        Reason = Require(reason, 1_000);
        Evidence = Require(evidence, 2_000);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid InvalidationId { get; private set; }

    public string TargetType { get; private set; } = string.Empty;

    public Guid TargetId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string Evidence { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Require(string value, int maximumLength)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 3 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value must contain 3 to {maximumLength} characters.", nameof(value));
        }

        return normalized;
    }
}
