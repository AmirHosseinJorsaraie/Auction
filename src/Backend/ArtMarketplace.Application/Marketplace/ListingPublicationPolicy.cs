namespace ArtMarketplace.Application.Marketplace;

public sealed record ListingPublicationContext(
    bool IsCurrentOwner,
    bool IsSellerAccountEnabled,
    bool IsSellerEligible,
    bool HasEffectiveApproval,
    bool HasUnresolvedEligibilityInvalidation,
    bool HasCommercialHold,
    bool SatisfiesChannelConstraint,
    long CurrentEligibilityVersion);

public sealed record ListingPublicationDecision(bool Allowed, string? Reason, long EligibilityVersion)
{
    public static ListingPublicationDecision Denied(string reason) => new(false, reason, 0);
}

public static class ListingPublicationPolicy
{
    public static ListingPublicationDecision CanPublishListingNow(ListingPublicationContext context)
    {
        if (!context.IsCurrentOwner) return ListingPublicationDecision.Denied("Only the current artwork owner can publish this listing.");
        if (!context.IsSellerAccountEnabled) return ListingPublicationDecision.Denied("The seller account is disabled.");
        if (!context.IsSellerEligible || context.CurrentEligibilityVersion <= 0) return ListingPublicationDecision.Denied("Seller eligibility is not currently valid.");
        if (!context.HasEffectiveApproval) return ListingPublicationDecision.Denied("The selected artwork revision does not have an effective approval.");
        if (context.HasUnresolvedEligibilityInvalidation) return ListingPublicationDecision.Denied("A seller eligibility invalidation must be explicitly resolved for this listing.");
        if (context.HasCommercialHold) return ListingPublicationDecision.Denied("A commercial integrity hold blocks publication.");
        if (!context.SatisfiesChannelConstraint) return ListingPublicationDecision.Denied("The artwork is active in another sales channel.");
        return new ListingPublicationDecision(true, null, context.CurrentEligibilityVersion);
    }
}
