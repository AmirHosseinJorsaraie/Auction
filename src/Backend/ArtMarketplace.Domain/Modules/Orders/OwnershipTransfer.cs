namespace ArtMarketplace.Domain.Modules.Orders;

public sealed class OwnershipTransfer
{
    private OwnershipTransfer() { }
    public OwnershipTransfer(Guid id, Guid orderId, Guid orderItemId, Guid artworkId, Guid listingId,
        Guid orderPaymentApplicationId, Guid fromUserId, Guid toUserId, DateTimeOffset now)
    {
        if (new[] { id, orderId, orderItemId, artworkId, listingId, orderPaymentApplicationId, fromUserId, toUserId }.Any(x => x == Guid.Empty)) throw new ArgumentException("Transfer identifiers are required.");
        if (fromUserId == toUserId) throw new InvalidOperationException("Ownership transfer parties must be different.");
        Id = id; OrderId = orderId; OrderItemId = orderItemId; ArtworkId = artworkId; ListingId = listingId;
        OrderPaymentApplicationId = orderPaymentApplicationId; FromUserId = fromUserId; ToUserId = toUserId; TransferredAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid ArtworkId { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid OrderPaymentApplicationId { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public DateTimeOffset TransferredAtUtc { get; private set; }
}
