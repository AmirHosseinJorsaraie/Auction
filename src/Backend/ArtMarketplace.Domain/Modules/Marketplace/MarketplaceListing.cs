using ArtMarketplace.Domain.Common;

namespace ArtMarketplace.Domain.Modules.Marketplace;

public enum MarketplaceListingStatus
{
    Draft = 0,
    Published = 1,
    Suspended = 2,
    Withdrawn = 3,
    Reserved = 4,
    Sold = 5,
    Completed = 6
}

public sealed class MarketplaceListing
{
    private MarketplaceListing() { }

    public MarketplaceListing(Guid id, Guid artworkId, Guid sellerUserId, Money price, DateTimeOffset now)
    {
        if (id == Guid.Empty || artworkId == Guid.Empty || sellerUserId == Guid.Empty)
            throw new ArgumentException("Listing, artwork, and seller identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price.Amount, nameof(price));
        Id = id; ArtworkId = artworkId; SellerUserId = sellerUserId;
        PriceAmount = price.Amount; Currency = price.Currency;
        Status = MarketplaceListingStatus.Draft; CreatedAtUtc = now; UpdatedAtUtc = now; Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid ArtworkId { get; private set; }
    public Guid SellerUserId { get; private set; }
    public decimal PriceAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public long SellerEligibilityVersionAtPublish { get; private set; }
    public Guid? PublishedArtworkRevisionId { get; private set; }
    public MarketplaceListingStatus Status { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public long Version { get; private set; }

    public void Publish(Guid revisionId, long eligibilityVersion, DateTimeOffset now)
    {
        if (Status != MarketplaceListingStatus.Draft) throw new InvalidOperationException("Only a draft listing can be published.");
        Activate(revisionId, eligibilityVersion, now);
    }

    public void Reinstate(Guid revisionId, long eligibilityVersion, DateTimeOffset now)
    {
        if (Status != MarketplaceListingStatus.Suspended) throw new InvalidOperationException("Only a suspended listing can be reinstated.");
        Activate(revisionId, eligibilityVersion, now);
    }

    public void SwapPublishedRevision(Guid revisionId, long eligibilityVersion, DateTimeOffset now)
    {
        if (Status is MarketplaceListingStatus.Reserved or MarketplaceListingStatus.Sold or MarketplaceListingStatus.Completed)
            throw new InvalidOperationException("The published revision cannot change after reservation or sale.");
        if (Status != MarketplaceListingStatus.Published && Status != MarketplaceListingStatus.Suspended)
            throw new InvalidOperationException("Only a published or suspended listing can change its revision pin.");
        PublishedArtworkRevisionId = RequiredId(revisionId);
        SellerEligibilityVersionAtPublish = PositiveVersion(eligibilityVersion);
        UpdatedAtUtc = now; AdvanceVersion();
    }

    public void Suspend(string reason, DateTimeOffset now)
    {
        if (Status == MarketplaceListingStatus.Suspended) return;
        if (Status != MarketplaceListingStatus.Published) throw new InvalidOperationException("Only a published listing can be suspended.");
        SuspensionReason = Required(reason, 1_000); Status = MarketplaceListingStatus.Suspended;
        UpdatedAtUtc = now; AdvanceVersion();
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (Status is MarketplaceListingStatus.Withdrawn or MarketplaceListingStatus.Reserved or MarketplaceListingStatus.Sold or MarketplaceListingStatus.Completed)
            throw new InvalidOperationException("This listing cannot be withdrawn in its current state.");
        Status = MarketplaceListingStatus.Withdrawn; UpdatedAtUtc = now; AdvanceVersion();
    }

    public void Reserve(DateTimeOffset now)
    {
        if (Status != MarketplaceListingStatus.Published)
            throw new InvalidOperationException("Only a published listing can be reserved.");
        if (PublishedArtworkRevisionId is null)
            throw new InvalidOperationException("A listing must pin a revision before reservation.");
        Status = MarketplaceListingStatus.Reserved; UpdatedAtUtc = now; AdvanceVersion();
    }

    public void ReleaseReservation(bool canRepublish, string reason, DateTimeOffset now)
    {
        if (Status != MarketplaceListingStatus.Reserved)
            throw new InvalidOperationException("Only a reserved listing can be released.");
        Status = canRepublish ? MarketplaceListingStatus.Published : MarketplaceListingStatus.Suspended;
        SuspensionReason = canRepublish ? null : Required(reason, 1_000);
        UpdatedAtUtc = now; AdvanceVersion();
    }

    public void MarkSold(DateTimeOffset now)
    {
        if (Status != MarketplaceListingStatus.Reserved) throw new InvalidOperationException("Only a reserved listing can be sold.");
        Status = MarketplaceListingStatus.Sold; UpdatedAtUtc = now; AdvanceVersion();
    }

    public void CompleteOwnershipTransfer(DateTimeOffset now)
    {
        if (Status != MarketplaceListingStatus.Sold) throw new InvalidOperationException("Only a sold listing can complete ownership transfer.");
        Status = MarketplaceListingStatus.Completed; UpdatedAtUtc = now; AdvanceVersion();
    }

    private void Activate(Guid revisionId, long eligibilityVersion, DateTimeOffset now)
    {
        PublishedArtworkRevisionId = RequiredId(revisionId);
        SellerEligibilityVersionAtPublish = PositiveVersion(eligibilityVersion);
        SuspensionReason = null; Status = MarketplaceListingStatus.Published;
        PublishedAtUtc ??= now; UpdatedAtUtc = now; AdvanceVersion();
    }

    private void AdvanceVersion() => Version = checked(Version + 1);
    private static Guid RequiredId(Guid value) => value == Guid.Empty ? throw new ArgumentException("A published revision is required.") : value;
    private static long PositiveVersion(long value) => value <= 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}
