using ArtMarketplace.Domain.Common;
using ArtMarketplace.Domain.Modules.Marketplace;

namespace ArtMarketplace.Domain.Tests;

public sealed class MarketplaceListingTests
{
    [Fact]
    public void DraftRequiresPositivePriceAndCannotClaimPublishedRevision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketplaceListing(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new Money(0, "IRR"), 2, DateTimeOffset.UtcNow));

        var listing = new MarketplaceListing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new Money(100, "IRR"), 2, DateTimeOffset.UtcNow);
        Assert.Equal(MarketplaceListingStatus.Draft, listing.Status);
        Assert.Null(listing.PublishedArtworkRevisionId);
    }
}
