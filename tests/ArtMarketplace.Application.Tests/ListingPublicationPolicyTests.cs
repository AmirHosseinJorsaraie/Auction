using ArtMarketplace.Application.Marketplace;

namespace ArtMarketplace.Application.Tests;

public sealed class ListingPublicationPolicyTests
{
    [Fact]
    public void ValidContextAllowsPublicationAndReturnsCurrentVersion()
    {
        var result = ListingPublicationPolicy.CanPublishListingNow(new(true, true, true, true, false, false, true, 8));
        Assert.True(result.Allowed); Assert.Equal(8, result.EligibilityVersion);
    }

    [Theory]
    [InlineData(false, true, true, true, false, false, true)]
    [InlineData(true, false, true, true, false, false, true)]
    [InlineData(true, true, false, true, false, false, true)]
    [InlineData(true, true, true, false, false, false, true)]
    [InlineData(true, true, true, true, true, false, true)]
    [InlineData(true, true, true, true, false, true, true)]
    [InlineData(true, true, true, true, false, false, false)]
    public void AnyFailedGuardDeniesPublication(bool owner, bool account, bool eligible, bool approval, bool invalidation, bool hold, bool channel)
    {
        var result = ListingPublicationPolicy.CanPublishListingNow(new(owner, account, eligible, approval, invalidation, hold, channel, 3));
        Assert.False(result.Allowed); Assert.NotNull(result.Reason);
    }
}
