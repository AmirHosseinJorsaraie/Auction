using ArtMarketplace.Domain.Common;
namespace ArtMarketplace.Domain.Modules.Marketplace;

public enum MarketplaceListingStatus { Draft = 0, Withdrawn = 1 }
public sealed class MarketplaceListing
{
    private MarketplaceListing() { }
    public MarketplaceListing(Guid id,Guid artworkId,Guid sellerUserId,Money price,long eligibilityVersion,DateTimeOffset now){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price.Amount,nameof(price));Id=id;ArtworkId=artworkId;SellerUserId=sellerUserId;PriceAmount=price.Amount;Currency=price.Currency;SellerEligibilityVersion=eligibilityVersion;Status=MarketplaceListingStatus.Draft;CreatedAtUtc=now;}
    public Guid Id { get; private set; } public Guid ArtworkId { get; private set; } public Guid SellerUserId { get; private set; } public decimal PriceAmount { get; private set; } public string Currency { get; private set; }=string.Empty; public long SellerEligibilityVersion { get; private set; } public Guid? PublishedArtworkRevisionId { get; private set; } public MarketplaceListingStatus Status { get; private set; } public DateTimeOffset CreatedAtUtc { get; private set; }
}
