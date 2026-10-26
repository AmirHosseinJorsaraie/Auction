namespace ArtMarketplace.Domain.Modules.Orders;

public enum OrderStatus { AwaitingPayment = 0, PaymentExpired = 1, Cancelled = 2, Confirmed = 3, Processing = 4, Shipped = 5, Delivered = 6 }
public enum ReservationStatus { Active = 0, Released = 1, Expired = 2, Cancelled = 3 }

public sealed class Order
{
    private Order() { }
    public Order(Guid id, Guid buyerUserId, Guid sellerUserId, decimal totalAmount, string currency,
        long sellerEligibilityVersionAtCheckout, DateTimeOffset reservationExpiresAtUtc, string idempotencyKey,
        string recipientName, string addressLine, string postalCode, DateTimeOffset now)
    {
        if (id == Guid.Empty || buyerUserId == Guid.Empty || sellerUserId == Guid.Empty) throw new ArgumentException("Order parties are required.");
        if (buyerUserId == sellerUserId) throw new InvalidOperationException("A seller cannot buy their own listing.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalAmount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sellerEligibilityVersionAtCheckout);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reservationExpiresAtUtc, now);
        Id = id; BuyerUserId = buyerUserId; SellerUserId = sellerUserId; TotalAmount = totalAmount; Currency = Required(currency, 3);
        SellerEligibilityVersionAtCheckout = sellerEligibilityVersionAtCheckout; ReservationExpiresAtUtc = reservationExpiresAtUtc;
        IdempotencyKey = Required(idempotencyKey, 100); RecipientName = Required(recipientName, 120);
        AddressLine = Required(addressLine, 500); PostalCode = Required(postalCode, 32);
        Status = OrderStatus.AwaitingPayment; CreatedAtUtc = now; UpdatedAtUtc = now; Version = 1;
    }
    public Guid Id { get; private set; }
    public Guid BuyerUserId { get; private set; }
    public Guid SellerUserId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public long SellerEligibilityVersionAtCheckout { get; private set; }
    public DateTimeOffset ReservationExpiresAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public bool ReviewIntegrityHold { get; private set; }
    public bool SellerEligibilityHold { get; private set; }
    public string? HoldReason { get; private set; }
    public string? TrackingCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    public void Cancel(DateTimeOffset now) => Terminate(OrderStatus.Cancelled, now);
    public void Expire(DateTimeOffset now)
    {
        if (now < ReservationExpiresAtUtc) throw new InvalidOperationException("The order reservation has not expired.");
        Terminate(OrderStatus.PaymentExpired, now);
    }
    public void PlaceReviewIntegrityHold(string reason, DateTimeOffset now) { ReviewIntegrityHold = true; HoldReason = Required(reason, 1_000); Touch(now); }
    public void PlaceSellerEligibilityHold(string reason, DateTimeOffset now) { SellerEligibilityHold = true; HoldReason = Required(reason, 1_000); Touch(now); }
    public void StartProcessing(DateTimeOffset now) { Transition(OrderStatus.Confirmed, OrderStatus.Processing, now); }
    public void Ship(string trackingCode, DateTimeOffset now) { TrackingCode = Required(trackingCode, 120); Transition(OrderStatus.Processing, OrderStatus.Shipped, now); }
    public void Deliver(DateTimeOffset now) { Transition(OrderStatus.Shipped, OrderStatus.Delivered, now); }

    private void Terminate(OrderStatus target, DateTimeOffset now)
    {
        if (Status == target) return;
        if (Status != OrderStatus.AwaitingPayment) throw new InvalidOperationException("Only an awaiting-payment order can be cancelled or expired.");
        Status = target; Touch(now);
    }
    private void Transition(OrderStatus expected, OrderStatus target, DateTimeOffset now)
    {
        if (Status != expected) throw new InvalidOperationException($"Order must be {expected}.");
        Status = target; Touch(now);
    }
    private void Touch(DateTimeOffset now) { UpdatedAtUtc = now; Version = checked(Version + 1); }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}

public sealed class OrderItem
{
    private OrderItem() { }
    public OrderItem(Guid id, Guid orderId, Guid listingId, Guid artworkId, Guid artworkRevisionId, string title, decimal priceAmount, string currency, string sellerDisplayName)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || listingId == Guid.Empty || artworkId == Guid.Empty || artworkRevisionId == Guid.Empty) throw new ArgumentException("Order item identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priceAmount);
        Id = id; OrderId = orderId; ListingId = listingId; ArtworkId = artworkId; ArtworkRevisionId = artworkRevisionId;
        Title = Required(title, 200); PriceAmount = priceAmount; Currency = Required(currency, 3); SellerDisplayName = Required(sellerDisplayName, 120);
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid ArtworkId { get; private set; }
    public Guid ArtworkRevisionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal PriceAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string SellerDisplayName { get; private set; } = string.Empty;
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}

public sealed class Reservation
{
    private Reservation() { }
    public Reservation(Guid id, Guid orderId, Guid orderItemId, Guid listingId, string token, DateTimeOffset now)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || orderItemId == Guid.Empty || listingId == Guid.Empty) throw new ArgumentException("Reservation identifiers are required.");
        Id = id; OrderId = orderId; OrderItemId = orderItemId; ListingId = listingId; Token = string.IsNullOrWhiteSpace(token) ? throw new ArgumentException("Reservation token is required.") : token;
        Status = ReservationStatus.Active; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid ListingId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? TerminalAtUtc { get; private set; }
    public void Cancel(DateTimeOffset now) => Terminate(ReservationStatus.Cancelled, now);
    public void Expire(DateTimeOffset now) => Terminate(ReservationStatus.Expired, now);
    private void Terminate(ReservationStatus target, DateTimeOffset now) { if (Status == target) return; if (Status != ReservationStatus.Active) throw new InvalidOperationException("Reservation is already terminal."); Status = target; TerminalAtUtc = now; }
}
