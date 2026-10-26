using System.Data;
using System.Text.Json;
using ArtMarketplace.Application.Marketplace;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtMarketplace.Api;

internal sealed record OrderTerminationResult(bool Success, bool NotFound = false, string? Error = null);

internal static class Phase6OrderOperations
{
    public static async Task<OrderTerminationResult> TerminateAsync(MarketplaceDbContext db, Guid orderId, Guid? buyerId, long? expectedVersion, bool expired, DateTimeOffset now, CancellationToken ct)
    {
        var plan = await (from item in db.OrderItems.AsNoTracking() join listing in db.MarketplaceListings.AsNoTracking() on item.ListingId equals listing.Id where item.OrderId == orderId orderby item.ArtworkId select new { item.ArtworkId, item.ListingId, listing.SellerUserId }).ToListAsync(ct);
        var location = await db.Orders.AsNoTracking().Where(x => x.Id == orderId).Select(x => new { x.BuyerUserId, x.SellerUserId }).SingleOrDefaultAsync(ct);
        if (location is null) return new(false, true); if (buyerId is not null && location.BuyerUserId != buyerId) return new(false, true);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        ApplicationUser? seller = null;
        foreach (var id in new[] { location.BuyerUserId, location.SellerUserId }.Distinct().OrderBy(x => x)) { var user = await db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct); if (id == location.SellerUserId) seller = user; }
        var eligibility = await db.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {location.SellerUserId} FOR UPDATE").SingleOrDefaultAsync(ct);
        foreach (var artworkId in plan.Select(x => x.ArtworkId).Distinct().OrderBy(x => x)) _ = await db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {artworkId} FOR UPDATE").SingleOrDefaultAsync(ct);
        var listings = new List<MarketplaceListing>(); foreach (var listingId in plan.Select(x => x.ListingId)) { var listing = await db.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"Id\" = {listingId} FOR UPDATE").SingleOrDefaultAsync(ct); if (listing is not null) listings.Add(listing); }
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders.orders WHERE \"Id\" = {orderId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (order is null) return new(false, true); if (expectedVersion is not null && order.Version != expectedVersion) return new(false, false, "The order changed; reload before continuing.");
        if (order.Status == (expired ? OrderStatus.PaymentExpired : OrderStatus.Cancelled)) { await tx.RollbackAsync(ct); return new(true); }
        if (order.Status != OrderStatus.AwaitingPayment) return new(false, false, "Only an awaiting-payment order can be released.");
        if (expired && now < order.ReservationExpiresAtUtc) return new(false, false, "The reservation has not expired.");
        if (listings.Count != plan.Count || listings.Any(x => x.Status != MarketplaceListingStatus.Reserved)) return new(false, false, "The order reservation is incomplete.");
        var reservations = await db.Reservations.Where(x => x.OrderId == orderId).OrderBy(x => x.Id).ToListAsync(ct); if (reservations.Count != listings.Count || reservations.Any(x => x.Status != ReservationStatus.Active)) return new(false, false, "The order reservations are incomplete.");
        foreach (var listing in listings)
        {
            var owner = await db.Artworks.AsNoTracking().Where(x => x.Id == listing.ArtworkId).Select(x => x.CurrentOwnerUserId).SingleAsync(ct);
            var approved = listing.PublishedArtworkRevisionId is not null && await db.ReviewDecisions.AsNoTracking().AnyAsync(x => x.ArtworkRevisionId == listing.PublishedArtworkRevisionId && x.Kind == ReviewDecisionKind.Approve && !db.ReviewDecisionSupersessions.Any(s => s.ReviewDecisionId == x.Id), ct);
            var invalid = await db.SellerEligibilityInvalidations.AsNoTracking().AnyAsync(inv => inv.UserId == listing.SellerUserId && listing.SellerEligibilityVersionAtPublish < inv.InvalidatedVersion && !db.SellerEligibilityInvalidationResolutions.Any(res => res.InvalidationId == inv.Id && res.TargetType == "MarketplaceListing" && res.TargetId == listing.Id), ct);
            var reviewHold = await db.ReviewCases.AsNoTracking().AnyAsync(x => x.ArtworkId == listing.ArtworkId && x.Status == ReviewCaseStatus.Open, ct);
            var decision = ListingPublicationPolicy.CanPublishListingNow(new ListingPublicationContext(owner == listing.SellerUserId, seller?.IsEnabled == true, eligibility?.CanSellAt(now) == true, approved, invalid, reviewHold || order.ReviewIntegrityHold || order.SellerEligibilityHold, true, eligibility?.EligibilityVersion ?? 0));
            var snapshotCurrent = eligibility is not null && listing.SellerEligibilityVersionAtPublish == eligibility.EligibilityVersion && order.SellerEligibilityVersionAtCheckout == eligibility.EligibilityVersion;
            listing.ReleaseReservation(decision.Allowed && snapshotCurrent, decision.Reason ?? "Commercial integrity hold prevents republishing.", now);
        }
        foreach (var reservation in reservations) { if (expired) reservation.Expire(now); else reservation.Cancel(now); }
        if (expired) order.Expire(now); else order.Cancel(now);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(), expired ? "order.expired" : "order.cancelled", buyerId ?? order.BuyerUserId, order.BuyerUserId, JsonSerializer.Serialize(new { orderId, itemCount = listings.Count }), null, now));
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(true); }
        catch (DbUpdateException) { return new(false, false, "A concurrent order operation won; reload before continuing."); }
    }
}
