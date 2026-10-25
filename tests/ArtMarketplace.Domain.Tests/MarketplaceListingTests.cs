using ArtMarketplace.Domain.Common;
using ArtMarketplace.Domain.Modules.Marketplace;

namespace ArtMarketplace.Domain.Tests;

public sealed class MarketplaceListingTests
{
    [Fact]
    public void DraftRequiresPositivePriceAndCannotClaimPublishedRevision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketplaceListing(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new Money(0, "IRR"), DateTimeOffset.UtcNow));

        var listing = new MarketplaceListing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new Money(100, "IRR"), DateTimeOffset.UtcNow);
        Assert.Equal(MarketplaceListingStatus.Draft, listing.Status);
        Assert.Null(listing.PublishedArtworkRevisionId);
    }

    [Fact]
    public void PublishPinsRevisionAndEligibilityVersion()
    {
        var listing = NewListing(); var revisionId = Guid.NewGuid();
        listing.Publish(revisionId, 7, DateTimeOffset.UtcNow);
        Assert.Equal(MarketplaceListingStatus.Published, listing.Status);
        Assert.Equal(revisionId, listing.PublishedArtworkRevisionId);
        Assert.Equal(7, listing.SellerEligibilityVersionAtPublish);
    }

    [Fact]
    public void SuspendAndReinstateRequireValidTransitions()
    {
        var listing = NewListing(); var revisionId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => listing.Suspend("integrity hold", DateTimeOffset.UtcNow));
        listing.Publish(revisionId, 2, DateTimeOffset.UtcNow);
        listing.Suspend("approval superseded", DateTimeOffset.UtcNow);
        Assert.Equal(MarketplaceListingStatus.Suspended, listing.Status);
        listing.Reinstate(revisionId, 3, DateTimeOffset.UtcNow);
        Assert.Equal(MarketplaceListingStatus.Published, listing.Status);
        Assert.Equal(3, listing.SellerEligibilityVersionAtPublish);
    }

    [Fact]
    public void WithdrawIsTerminalForPhaseFiveOperations()
    {
        var listing = NewListing(); listing.Withdraw(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => listing.Publish(Guid.NewGuid(), 2, DateTimeOffset.UtcNow));
    }

    private static MarketplaceListing NewListing() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(100, "IRR"), DateTimeOffset.UtcNow);
}
