using ArtMarketplace.Domain.Common;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Domain.Modules.Payments;

namespace ArtMarketplace.Domain.Tests;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AttemptPreservesSnapshotAndMovesThroughProviderLifecycle()
    {
        var attempt = new PaymentAttempt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Fake", "key", "merchant-ref", "merchant", 120, "IRR", Now);
        attempt.MarkRedirectReady("txn-1", "https://example.test/pay", Now.AddSeconds(1));
        attempt.RecordOutcome(PaymentOutcome.Succeeded, Now.AddSeconds(2));
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(120, attempt.Amount);
        Assert.Equal("txn-1", attempt.ProviderTransactionId);
        attempt.RecordOutcome(PaymentOutcome.Failed, Now.AddSeconds(3));
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
    }

    [Fact]
    public void HeldOrderCannotApplyPayment()
    {
        var order = CreateOrder(); order.PlaceReviewIntegrityHold("approval superseded", Now);
        Assert.Throws<InvalidOperationException>(() => order.ConfirmPayment(Now));
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public void VerifiedPaymentMovesOrderAndListingForwardOnly()
    {
        var order = CreateOrder(); var listing = CreateReservedListing();
        order.ConfirmPayment(Now); listing.MarkSold(Now); listing.CompleteOwnershipTransfer(Now);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(MarketplaceListingStatus.Completed, listing.Status);
        Assert.Throws<InvalidOperationException>(() => listing.MarkSold(Now));
    }

    [Fact]
    public void OwnershipTransferPinsEveryCommercialIdentifier()
    {
        var seller = Guid.NewGuid(); var buyer = Guid.NewGuid(); var orderId = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var transfer = new OwnershipTransfer(Guid.NewGuid(), orderId, itemId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), seller, buyer, Now);
        Assert.Equal(orderId, transfer.OrderId); Assert.Equal(itemId, transfer.OrderItemId); Assert.Equal(seller, transfer.FromUserId); Assert.Equal(buyer, transfer.ToUserId);
    }

    [Fact]
    public void ReceiptAndApplicationKeepExactTrace()
    {
        var attemptId = Guid.NewGuid(); var eventId = Guid.NewGuid(); var receipt = new VerifiedPaymentReceipt(Guid.NewGuid(), attemptId, eventId, "Fake", "txn-2", 25, "IRR", Now);
        var application = new OrderPaymentApplication(Guid.NewGuid(), Guid.NewGuid(), receipt.Id, Now);
        Assert.Equal(eventId, receipt.SourceProviderEventId); Assert.Equal(receipt.Id, application.VerifiedPaymentReceiptId);
    }

    private static Order CreateOrder() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, "IRR", 3, Now.AddMinutes(15), "idempotency", "گیرنده", "نشانی", "123", Now);
    private static MarketplaceListing CreateReservedListing()
    {
        var listing = new MarketplaceListing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(10, "IRR"), Now);
        listing.Publish(Guid.NewGuid(), 3, Now); listing.Reserve(Now); return listing;
    }
}
