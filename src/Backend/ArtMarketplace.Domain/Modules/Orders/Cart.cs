namespace ArtMarketplace.Domain.Modules.Orders;

public sealed class Cart
{
    private Cart() { }

    public Cart(Guid id, Guid buyerUserId, DateTimeOffset now)
    {
        if (id == Guid.Empty || buyerUserId == Guid.Empty) throw new ArgumentException("Cart and buyer identifiers are required.");
        Id = id; BuyerUserId = buyerUserId; CreatedAtUtc = now; UpdatedAtUtc = now; Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid BuyerUserId { get; private set; }
    public Guid? SellerUserId { get; private set; }
    public string? Currency { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    public void AcceptItem(Guid sellerUserId, string currency, DateTimeOffset now)
    {
        if (sellerUserId == Guid.Empty || string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Seller and currency are required.");
        if (sellerUserId == BuyerUserId) throw new InvalidOperationException("A seller cannot buy their own listing.");
        if (SellerUserId is not null && SellerUserId != sellerUserId) throw new InvalidOperationException("A cart can contain listings from only one seller.");
        if (Currency is not null && Currency != currency) throw new InvalidOperationException("A cart can contain only one currency.");
        SellerUserId = sellerUserId; Currency = currency; UpdatedAtUtc = now; Version = checked(Version + 1);
    }

    public void ItemRemoved(bool isEmpty, DateTimeOffset now)
    {
        if (isEmpty) { SellerUserId = null; Currency = null; }
        UpdatedAtUtc = now; Version = checked(Version + 1);
    }

    public void CheckedOut(DateTimeOffset now)
    {
        SellerUserId = null; Currency = null; UpdatedAtUtc = now; Version = checked(Version + 1);
    }
}

public sealed class CartItem
{
    private CartItem() { }
    public CartItem(Guid id, Guid cartId, Guid listingId, decimal priceAmountAtAdd, string currencyAtAdd, DateTimeOffset now)
    {
        if (id == Guid.Empty || cartId == Guid.Empty || listingId == Guid.Empty) throw new ArgumentException("Cart item identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priceAmountAtAdd);
        Id = id; CartId = cartId; ListingId = listingId; PriceAmountAtAdd = priceAmountAtAdd;
        CurrencyAtAdd = currencyAtAdd; AddedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public Guid ListingId { get; private set; }
    public decimal PriceAmountAtAdd { get; private set; }
    public string CurrencyAtAdd { get; private set; } = string.Empty;
    public DateTimeOffset AddedAtUtc { get; private set; }
}
