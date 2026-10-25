using System.Security.Claims;
using System.Text.Json;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Application.Marketplace;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Api;

internal static class Phase5Endpoints
{
    public static IEndpointRouteBuilder MapPhase5Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var gallery = endpoints.MapGroup("/api/gallery").RequireRateLimiting("baseline").WithTags("Public gallery");
        gallery.MapGet("/", GetGalleryAsync);
        gallery.MapGet("/{listingId:guid}", GetListingAsync);
        gallery.MapGet("/artists", GetArtistsAsync);
        gallery.MapGet("/artists/{slug}", GetArtistAsync);

        var listings = endpoints.MapGroup("/api/marketplace/listings").RequireAuthorization().RequireRateLimiting("baseline").WithTags("Seller listings");
        listings.MapGet("/mine", GetMineAsync);
        listings.MapPost("/{listingId:guid}/publish", PublishAsync);
        listings.MapPost("/{listingId:guid}/reinstate", ReinstateAsync);
        listings.MapPost("/{listingId:guid}/revision", SwapRevisionAsync);
        listings.MapPost("/{listingId:guid}/withdraw", WithdrawAsync);

        endpoints.MapPost("/api/backoffice/listings/{listingId:guid}/suspend", SuspendAsync)
            .RequireAuthorization(MarketplacePolicies.StaffWithMfa).RequireRateLimiting("baseline").WithTags("Backoffice listings");
        return endpoints;
    }

    private static async Task<IResult> GetGalleryAsync([AsParameters] GalleryQuery query, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        var page = Math.Clamp(query.Page ?? 1, 1, 10_000); var pageSize = Math.Clamp(query.PageSize ?? 12, 1, 48); var now = time.GetUtcNow();
        var source = PublicListings(db, now);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(x => EF.Functions.ILike(x.Revision.Title, $"%{term}%") || EF.Functions.ILike(x.Revision.ArtistAttribution, $"%{term}%") || EF.Functions.ILike(x.Revision.Description, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.Category)) source = source.Where(x => x.Category.Slug == query.Category);
        if (!string.IsNullOrWhiteSpace(query.Artist)) source = source.Where(x => x.Artist != null && x.Artist.Slug == query.Artist);
        source = query.Sort?.ToLowerInvariant() switch
        {
            "price-asc" => source.OrderBy(x => x.Listing.PriceAmount).ThenBy(x => x.Listing.Id),
            "price-desc" => source.OrderByDescending(x => x.Listing.PriceAmount).ThenBy(x => x.Listing.Id),
            "title" => source.OrderBy(x => x.Revision.Title).ThenBy(x => x.Listing.Id),
            _ => source.OrderByDescending(x => x.Listing.PublishedAtUtc).ThenBy(x => x.Listing.Id)
        };
        var total = await source.CountAsync(ct);
        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new
        {
            x.Listing.Id, x.Listing.ArtworkId, x.Revision.Title, x.Revision.ArtistAttribution, x.Revision.Year,
            x.Listing.PriceAmount, x.Listing.Currency, x.Listing.Status, available = x.Listing.Status == MarketplaceListingStatus.Published,
            category = new { x.Category.Name, x.Category.Slug }, artist = x.Artist == null ? null : new { x.Artist.Name, x.Artist.Slug },
            seller = new { displayName = x.Profile == null ? "فروشنده آثار هنری" : x.Profile.DisplayName },
            imageAssetId = db.ArtworkAssets.Where(asset => asset.ArtworkRevisionId == x.Revision.Id && asset.Kind == ArtworkAssetKind.PublicImage).OrderBy(asset => asset.Id).Select(asset => (Guid?)asset.Id).FirstOrDefault()
        }).ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    private static async Task<IResult> GetListingAsync(Guid listingId, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        var item = await PublicListings(db, time.GetUtcNow()).Where(x => x.Listing.Id == listingId).Select(x => new
        {
            x.Listing.Id, x.Listing.ArtworkId, x.Revision.Title, x.Revision.ArtistAttribution, x.Revision.Description,
            x.Revision.Year, x.Revision.Medium, x.Revision.Dimensions, x.Revision.Condition,
            x.Listing.PriceAmount, x.Listing.Currency, x.Listing.Status, available = x.Listing.Status == MarketplaceListingStatus.Published,
            category = new { x.Category.Name, x.Category.Slug }, artist = x.Artist == null ? null : new { x.Artist.Name, x.Artist.Slug, x.Artist.Biography },
            seller = new { displayName = x.Profile == null ? "فروشنده آثار هنری" : x.Profile.DisplayName, bio = x.Profile == null ? null : x.Profile.PublicSellerBio },
            images = db.ArtworkAssets.Where(asset => asset.ArtworkRevisionId == x.Revision.Id && asset.Kind == ArtworkAssetKind.PublicImage).OrderBy(asset => asset.Id).Select(asset => new { asset.Id, url = $"/api/media/{asset.Id}" }).ToList()
        }).SingleOrDefaultAsync(ct);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> GetArtistsAsync(MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        var artists = await PublicListings(db, time.GetUtcNow()).Where(x => x.Artist != null)
            .GroupBy(x => new { x.Artist!.Id, x.Artist.Name, x.Artist.Slug }).Select(group => new { group.Key.Id, group.Key.Name, group.Key.Slug, artworkCount = group.Count() })
            .OrderBy(x => x.Name).ToListAsync(ct);
        return Results.Ok(artists);
    }

    private static async Task<IResult> GetArtistAsync(string slug, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        var artist = await db.Artists.AsNoTracking().Where(x => x.Slug == slug).Select(x => new { x.Id, x.Name, x.Slug, x.Biography }).SingleOrDefaultAsync(ct);
        if (artist is null) return Results.NotFound();
        var listings = await PublicListings(db, time.GetUtcNow()).Where(x => x.Artist != null && x.Artist.Slug == slug)
            .OrderByDescending(x => x.Listing.PublishedAtUtc).Select(x => new { x.Listing.Id, x.Revision.Title, x.Listing.PriceAmount, x.Listing.Currency }).ToListAsync(ct);
        return Results.Ok(new { artist, listings });
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        var items = await (from listing in db.MarketplaceListings.AsNoTracking()
                           join artwork in db.Artworks.AsNoTracking() on listing.ArtworkId equals artwork.Id
                           where listing.SellerUserId == userId
                           orderby listing.CreatedAtUtc descending
                           select new
                           {
                               listing.Id, listing.ArtworkId, artwork.InternalCode, listing.PriceAmount, listing.Currency, listing.Status,
                               listing.PublishedArtworkRevisionId, listing.SellerEligibilityVersionAtPublish, listing.SuspensionReason, listing.Version,
                               approvedRevisionIds = db.ArtworkRevisions.Where(revision => revision.ArtworkId == listing.ArtworkId && revision.Status == ArtworkRevisionStatus.Frozen
                                   && db.ReviewDecisions.Any(decision => decision.ArtworkRevisionId == revision.Id && decision.Kind == ReviewDecisionKind.Approve
                                       && !db.ReviewDecisionSupersessions.Any(supersession => supersession.ReviewDecisionId == decision.Id)))
                                   .OrderByDescending(revision => revision.Number).Select(revision => revision.Id).ToList()
                           }).ToListAsync(ct);
        return Results.Ok(items);
    }

    private static Task<IResult> PublishAsync(Guid listingId, PublicationRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct) =>
        ChangePublicationAsync(listingId, request, principal, db, time, PublicationOperation.Publish, ct);
    private static Task<IResult> ReinstateAsync(Guid listingId, PublicationRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct) =>
        ChangePublicationAsync(listingId, request, principal, db, time, PublicationOperation.Reinstate, ct);
    private static Task<IResult> SwapRevisionAsync(Guid listingId, PublicationRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct) =>
        ChangePublicationAsync(listingId, request, principal, db, time, PublicationOperation.SwapRevision, ct);

    private static async Task<IResult> ChangePublicationAsync(Guid listingId, PublicationRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, PublicationOperation operation, CancellationToken ct)
    {
        if (!UserId(principal, out var sellerId)) return Results.Unauthorized();
        var location = await db.MarketplaceListings.AsNoTracking().Where(x => x.Id == listingId).Select(x => new { x.ArtworkId }).SingleOrDefaultAsync(ct);
        if (location is null) return Results.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var now = time.GetUtcNow();
        var user = await LockUser(db, sellerId, ct); var eligibility = await LockEligibility(db, sellerId, ct);
        var artwork = await LockArtwork(db, location.ArtworkId, ct); var listing = await LockListing(db, listingId, ct);
        if (listing is null || artwork is null || listing.SellerUserId != sellerId) return Results.NotFound();
        if (listing.Version != request.ExpectedVersion) return Conflict("The listing changed. Reload before continuing.");
        var revision = await db.ArtworkRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.RevisionId && x.ArtworkId == artwork.Id && x.Status == ArtworkRevisionStatus.Frozen, ct);
        if (revision is null) return Validation("A frozen revision belonging to this artwork is required.");
        var effectiveApproval = await db.ReviewDecisions.AsNoTracking().AnyAsync(decision => decision.ArtworkRevisionId == revision.Id && decision.Kind == ReviewDecisionKind.Approve && !db.ReviewDecisionSupersessions.Any(supersession => supersession.ReviewDecisionId == decision.Id), ct);
        var unresolvedInvalidation = await db.SellerEligibilityInvalidations.AsNoTracking().AnyAsync(invalidation => invalidation.UserId == sellerId && !db.SellerEligibilityInvalidationResolutions.Any(resolution => resolution.InvalidationId == invalidation.Id && resolution.TargetType == "MarketplaceListing" && resolution.TargetId == listingId), ct);
        var hold = await db.ReviewCases.AsNoTracking().AnyAsync(reviewCase => reviewCase.ArtworkId == artwork.Id && reviewCase.Status == ReviewCaseStatus.Open, ct);
        var decision = ListingPublicationPolicy.CanPublishListingNow(new ListingPublicationContext(artwork.CurrentOwnerUserId == sellerId, user?.IsEnabled == true, eligibility?.CanSellAt(now) == true, effectiveApproval, unresolvedInvalidation, hold, true, eligibility?.EligibilityVersion ?? 0));
        if (!decision.Allowed) return Conflict(decision.Reason!);
        try
        {
            switch (operation)
            {
                case PublicationOperation.Publish: listing.Publish(revision.Id, decision.EligibilityVersion, now); break;
                case PublicationOperation.Reinstate: listing.Reinstate(revision.Id, decision.EligibilityVersion, now); break;
                case PublicationOperation.SwapRevision: listing.SwapPublishedRevision(revision.Id, decision.EligibilityVersion, now); break;
            }
            AddAudit(db, $"listing.{operation.ToString().ToLowerInvariant()}", sellerId, artwork.Id, now, new { listingId, revisionId = revision.Id, eligibilityVersion = decision.EligibilityVersion });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { listing.Id, listing.Status, listing.PublishedArtworkRevisionId, listing.SellerEligibilityVersionAtPublish, listing.Version });
        }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (DbUpdateException) { return Conflict("A concurrent marketplace operation won. Reload the listing."); }
    }

    private static async Task<IResult> WithdrawAsync(Guid listingId, VersionRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var sellerId)) return Results.Unauthorized();
        var location = await db.MarketplaceListings.AsNoTracking().Where(x => x.Id == listingId).Select(x => new { x.ArtworkId }).SingleOrDefaultAsync(ct); if (location is null) return Results.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        _ = await LockUser(db, sellerId, ct); var artwork = await LockArtwork(db, location.ArtworkId, ct); var listing = await LockListing(db, listingId, ct);
        if (artwork is null || listing is null || artwork.CurrentOwnerUserId != sellerId || listing.SellerUserId != sellerId) return Results.NotFound();
        if (listing.Version != request.ExpectedVersion) return Conflict("The listing changed. Reload before continuing.");
        try { listing.Withdraw(time.GetUtcNow()); AddAudit(db, "listing.withdrawn", sellerId, artwork.Id, time.GetUtcNow(), new { listingId }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { listing.Status, listing.Version }); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    private static async Task<IResult> SuspendAsync(Guid listingId, SuspensionRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var actorId)) return Results.Unauthorized();
        var location = await db.MarketplaceListings.AsNoTracking().Where(x => x.Id == listingId).Select(x => new { x.ArtworkId }).SingleOrDefaultAsync(ct); if (location is null) return Results.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        _ = await LockArtwork(db, location.ArtworkId, ct); var listing = await LockListing(db, listingId, ct); if (listing is null) return Results.NotFound();
        try { listing.Suspend(request.Reason, time.GetUtcNow()); AddAudit(db, "listing.suspended", actorId, location.ArtworkId, time.GetUtcNow(), new { listingId, request.Reason }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { listing.Status, listing.Version }); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return Conflict(ex.Message); }
    }

    private static IQueryable<PublicListingRow> PublicListings(MarketplaceDbContext db, DateTimeOffset now) =>
        from listing in db.MarketplaceListings.AsNoTracking()
        join artwork in db.Artworks.AsNoTracking() on listing.ArtworkId equals artwork.Id
        join revision in db.ArtworkRevisions.AsNoTracking() on listing.PublishedArtworkRevisionId equals revision.Id
        join category in db.Categories.AsNoTracking() on revision.CategoryId equals category.Id
        join eligibility in db.SellerEligibilities.AsNoTracking() on listing.SellerUserId equals eligibility.UserId
        join user in db.Users.AsNoTracking() on listing.SellerUserId equals user.Id
        join profileJoin in db.UserProfiles.AsNoTracking() on listing.SellerUserId equals profileJoin.UserId into profiles
        from profile in profiles.DefaultIfEmpty()
        join artistJoin in db.Artists.AsNoTracking() on revision.ArtistId equals artistJoin.Id into artists
        from artist in artists.DefaultIfEmpty()
        where (listing.Status == MarketplaceListingStatus.Published || listing.Status == MarketplaceListingStatus.Sold || listing.Status == MarketplaceListingStatus.Completed)
              && user.IsEnabled && eligibility.AccountEnabled && eligibility.VerificationApproved && eligibility.VerificationExpiresAtUtc > now
              && listing.SellerEligibilityVersionAtPublish == eligibility.EligibilityVersion
              && db.ReviewDecisions.Any(decision => decision.ArtworkRevisionId == revision.Id && decision.Kind == ReviewDecisionKind.Approve && !db.ReviewDecisionSupersessions.Any(supersession => supersession.ReviewDecisionId == decision.Id))
              && !db.SellerEligibilityInvalidations.Any(invalidation => invalidation.UserId == listing.SellerUserId && !db.SellerEligibilityInvalidationResolutions.Any(resolution => resolution.InvalidationId == invalidation.Id && resolution.TargetType == "MarketplaceListing" && resolution.TargetId == listing.Id))
        select new PublicListingRow(listing, artwork, revision, category, artist, profile);

    private static Task<ApplicationUser?> LockUser(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<SellerEligibility?> LockEligibility(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<Artwork?> LockArtwork(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<MarketplaceListing?> LockListing(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static bool UserId(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out id);
    private static void AddAudit(MarketplaceDbContext db, string type, Guid actor, Guid artworkId, DateTimeOffset now, object data) => db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(), type, actor, actor, JsonSerializer.Serialize(new { artworkId, data }), null, now));
    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });
    private static IResult Conflict(string message) => Results.Conflict(new { message });

    private sealed record PublicListingRow(MarketplaceListing Listing, Artwork Artwork, ArtworkRevision Revision, Category Category, Artist? Artist, Domain.Modules.Profiles.UserProfile? Profile);
    private enum PublicationOperation { Publish, Reinstate, SwapRevision }
    internal sealed record GalleryQuery(string? Search, string? Category, string? Artist, string? Sort, int? Page, int? PageSize);
    internal sealed record PublicationRequest(Guid RevisionId, long ExpectedVersion);
    internal sealed record VersionRequest(long ExpectedVersion);
    internal sealed record SuspensionRequest(string Reason);
}
