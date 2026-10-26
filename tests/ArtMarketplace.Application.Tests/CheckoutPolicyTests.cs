using ArtMarketplace.Application.Orders;

namespace ArtMarketplace.Application.Tests;

public sealed class CheckoutPolicyTests
{
    [Fact]
    public void CompleteBuyerIsAllowed() => Assert.True(CheckoutPolicy.CanCheckout(new(true, true, true, true)).Allowed);

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void IncompleteBuyerIsDenied(bool enabled, bool email, bool phone, bool address) => Assert.False(CheckoutPolicy.CanCheckout(new(enabled, email, phone, address)).Allowed);
}
