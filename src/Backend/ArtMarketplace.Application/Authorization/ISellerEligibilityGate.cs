namespace ArtMarketplace.Application.Authorization;

public interface ISellerEligibilityGate
{
    Task<SellerEligibilityDecision> EvaluateAsync(
        Guid userId,
        long? capturedEligibilityVersion,
        CancellationToken cancellationToken = default);
}

public sealed record SellerEligibilityDecision(
    bool Allowed,
    long CurrentVersion,
    bool SnapshotInvalidated,
    string Reason);
