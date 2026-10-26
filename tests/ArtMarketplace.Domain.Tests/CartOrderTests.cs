using ArtMarketplace.Domain.Modules.Orders;

namespace ArtMarketplace.Domain.Tests;

public sealed class CartOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CartRejectsOwnListingAndMixedSellers()
    {
        var buyer = Guid.NewGuid(); var seller = Guid.NewGuid(); var cart = new Cart(Guid.NewGuid(), buyer, Now);
        Assert.Throws<InvalidOperationException>(() => cart.AcceptItem(buyer, "IRR", Now));
        cart.AcceptItem(seller, "IRR", Now);
        Assert.Throws<InvalidOperationException>(() => cart.AcceptItem(Guid.NewGuid(), "IRR", Now));
    }

    [Fact]
    public void CartRejectsMixedCurrency()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid(), Now); var seller = Guid.NewGuid(); cart.AcceptItem(seller, "IRR", Now);
        Assert.Throws<InvalidOperationException>(() => cart.AcceptItem(seller, "USD", Now));
    }

    [Fact]
    public void OrderOwnsSingleDeadlineAndEligibilitySnapshot()
    {
        var order = CreateOrder(); var item = new OrderItem(Guid.NewGuid(), order.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "اثر", 10, "IRR", "فروشنده");
        var reservation = new Reservation(Guid.NewGuid(), order.Id, item.Id, item.ListingId, "token", Now);
        Assert.Equal(Now.AddMinutes(15), order.ReservationExpiresAtUtc); Assert.Equal(3, order.SellerEligibilityVersionAtCheckout);
        Assert.DoesNotContain(typeof(Reservation).GetProperties(), x => x.Name.Contains("Expires", StringComparison.Ordinal) || x.Name.Contains("EligibilityVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void ExpiryBeforeParentDeadlineIsRejected()
    {
        var order = CreateOrder(); Assert.Throws<InvalidOperationException>(() => order.Expire(Now.AddMinutes(14)));
        order.Expire(Now.AddMinutes(15)); Assert.Equal(OrderStatus.PaymentExpired, order.Status);
    }

    [Fact]
    public void ReservationTerminalTransitionIsIdempotentOnlyForSameOutcome()
    {
        var reservation = new Reservation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "token", Now);
        reservation.Cancel(Now); reservation.Cancel(Now); Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Throws<InvalidOperationException>(() => reservation.Expire(Now));
    }

    [Fact]
    public void IntegrityHoldSurvivesCancellation()
    {
        var order = CreateOrder(); order.PlaceReviewIntegrityHold("approval superseded", Now); order.Cancel(Now);
        Assert.True(order.ReviewIntegrityHold); Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    private static Order CreateOrder() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, "IRR", 3, Now.AddMinutes(15), "idempotency", "گیرنده", "نشانی کامل", "123", Now);
}
