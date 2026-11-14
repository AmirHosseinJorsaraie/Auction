namespace ArtMarketplace.PublicWeb.Client.Models;

// Public catalog contracts are intentionally isolated from UI components so API changes
// can be handled in one place without mixing transport details into page markup.
public sealed record CategorySummary(Guid Id, string Name, string Slug = "");
public sealed record ArtistSummary(Guid Id, string Name, string Slug, int ArtworkCount = 0, string? Biography = null);
public sealed record SellerSummary(string DisplayName, string? Bio = null);
public sealed record ListingImage(Guid Id, string Url);

public sealed record GalleryItem(
    Guid Id,
    Guid ArtworkId,
    string Title,
    string ArtistAttribution,
    int? Year,
    decimal PriceAmount,
    string Currency,
    string Status,
    bool Available,
    CategorySummary Category,
    ArtistSummary? Artist,
    SellerSummary Seller,
    Guid? ImageAssetId);

public sealed record GalleryPage(
    IReadOnlyList<GalleryItem> Items,
    int Page,
    int PageSize,
    int Total,
    int TotalPages);

public sealed record ListingDetail(
    Guid Id,
    Guid ArtworkId,
    string Title,
    string ArtistAttribution,
    int? Year,
    decimal PriceAmount,
    string Currency,
    string Status,
    bool Available,
    CategorySummary Category,
    ArtistSummary? Artist,
    SellerSummary Seller,
    Guid? ImageAssetId,
    string Description,
    string Medium,
    string Dimensions,
    string Condition,
    IReadOnlyList<ListingImage> Images);

public sealed record ArtistDetail(ArtistSummary Artist, IReadOnlyList<ArtistListing> Listings);
public sealed record ArtistListing(Guid Id, string Title, decimal PriceAmount, string Currency);
public sealed record ContentPage(string Slug, string Title, string Body, DateTimeOffset UpdatedAtUtc);
public sealed record PublicBanner(string Message, DateTimeOffset? EndsAtUtc);

public sealed record CartView(IReadOnlyList<CartItem> Items, decimal Total, string? Currency, long Version);
public sealed record CartItem(Guid ListingId, string Title, decimal CurrentPriceAmount, string Currency, decimal PriceAmountAtAdd, bool PriceChanged, string Status);
public sealed record AddressView(Guid Id, string Label, string RecipientName, string AddressLine, string PostalCode);
public sealed record CreatedAddress(Guid Id);
public sealed record CheckoutResult(Guid Id);

public sealed record OrderSummary(Guid Id, decimal TotalAmount, string Currency, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset ReservationExpiresAtUtc, bool ReviewIntegrityHold, bool SellerEligibilityHold);
public sealed record OrderDetail(OrderView Order, IReadOnlyList<OrderItem> Items);
public sealed record OrderView(Guid Id, decimal TotalAmount, string Currency, string Status, DateTimeOffset ReservationExpiresAtUtc, string RecipientName, string AddressLine, string PostalCode, string? HoldReason, long Version);
public sealed record OrderItem(Guid Id, string Title, decimal PriceAmount, string Currency);
public sealed record PaymentAttempt(Guid Id, string Status, string? RedirectUrl, string? FailureCode);
public sealed record PaymentCompletion(string? Code, Guid? OrderId);

public sealed record ArtworkSummary(Guid Id, string InternalCode, DateTimeOffset CreatedAtUtc);
public sealed record ArtworkDetail(Guid Id, string InternalCode, IReadOnlyList<ArtworkRevision> Revisions, IReadOnlyList<ArtworkAsset> Assets);
public sealed record ArtworkRevision(Guid Id, int Number, string Title, string Status, string ArtistAttribution, DateTimeOffset UpdatedAtUtc);
public sealed record ArtworkAsset(Guid Id, Guid ArtworkRevisionId, string Kind, string ContentType, long Size, string Sha256);
public sealed record ReviewResult(IReadOnlyList<ReviewRound> Rounds, IReadOnlyList<ReviewFinding> Findings, IReadOnlyList<ReviewDecision> Decisions, IReadOnlyList<Guid> SupersededDecisionIds);
public sealed record ReviewRound(Guid Id, int Number, string Status);
public sealed record ReviewFinding(Guid Id, Guid ReviewRoundId, string Category, string Message);
public sealed record ReviewDecision(Guid Id, Guid ReviewRoundId, string Kind, string Reason, string PublicComment);

public sealed record SellerListing(Guid Id, Guid ArtworkId, string InternalCode, decimal PriceAmount, string Currency, string Status, Guid? PublishedArtworkRevisionId, string? SuspensionReason, long Version, IReadOnlyList<Guid> ApprovedRevisionIds);
public sealed record SellerOrder(Guid Id, Guid BuyerUserId, decimal TotalAmount, string Currency, string Status, DateTimeOffset CreatedAtUtc);
public sealed record CreatedArtwork(Guid ArtworkId);
public sealed record UploadIntent(Guid Id, string UploadUrl);
public sealed record FinalizedUpload(string Sha256);
public sealed record SessionView(bool Authenticated, string? DisplayName, string CsrfToken);
