using System.Data;
using System.Text.Json;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtMarketplace.Api;

internal static class Phase7OwnershipOperations
{
    internal sealed record TransferResult(bool Success, bool NotFound = false, bool Replay = false, string? Error = null, int TransferCount = 0);

    public static async Task<TransferResult> TransferAsync(MarketplaceDbContext db, Guid orderId, Guid actorId, DateTimeOffset now, CancellationToken ct)
    {
        var plan = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (plan is null) return new(false, true);
        var items = await db.OrderItems.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.ArtworkId).ToListAsync(ct);
        if (items.Count == 0) return new(false, Error: "The order has no items.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var users = new Dictionary<Guid, ApplicationUser>();
        foreach (var userId in new[] { plan.SellerUserId, plan.BuyerUserId }.OrderBy(x => x))
        {
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (user is not null) users[userId] = user;
        }
        var eligibility = await db.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {plan.SellerUserId} FOR UPDATE").SingleOrDefaultAsync(ct);

        var artworks = new List<Artwork>();
        foreach (var item in items)
        {
            var artwork = await db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {item.ArtworkId} FOR UPDATE").SingleAsync(ct); artworks.Add(artwork);
            _ = await db.ReviewCases.FromSqlInterpolated($"SELECT * FROM reviews.review_cases WHERE \"ArtworkId\" = {item.ArtworkId} FOR UPDATE").ToListAsync(ct);
        }
        var listings = new List<MarketplaceListing>();
        foreach (var item in items) listings.Add(await db.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"Id\" = {item.ListingId} FOR UPDATE").SingleAsync(ct));
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders.orders WHERE \"Id\" = {orderId} FOR UPDATE").SingleAsync(ct);
        var application = await db.OrderPaymentApplications.FromSqlInterpolated($"SELECT * FROM payments.order_payment_applications WHERE \"OrderId\" = {orderId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (application is null) return new(false, Error: "No verified payment application exists for this order.");
        var existing = await db.OwnershipTransfers.AsNoTracking().CountAsync(x => x.OrderId == orderId, ct);
        if (existing == items.Count) { await tx.RollbackAsync(ct); return new(true, Replay: true, TransferCount: existing); }
        if (existing > 0) return new(false, Error: "The ownership transfer set is incomplete and requires reconciliation.");
        if (order.Status != OrderStatus.Delivered) return new(false, Error: "Ownership transfer requires a delivered order.");
        if (order.ReviewIntegrityHold || order.SellerEligibilityHold) return new(false, Error: "The order is on hold.");
        if (!users.TryGetValue(order.SellerUserId, out var seller) || !seller.IsEnabled || !users.TryGetValue(order.BuyerUserId, out var buyer) || !buyer.IsEnabled || eligibility?.CanSellAt(now) != true || eligibility.EligibilityVersion != order.SellerEligibilityVersionAtCheckout)
            return new(false, Error: "Buyer or seller account eligibility no longer matches the order snapshot.");

        foreach (var item in items)
        {
            var artwork = artworks.Single(x => x.Id == item.ArtworkId); var listing = listings.Single(x => x.Id == item.ListingId);
            if (artwork.CurrentOwnerUserId != order.SellerUserId || listing.SellerUserId != order.SellerUserId || listing.ArtworkId != artwork.Id || listing.Status != MarketplaceListingStatus.Sold)
                return new(false, Error: "Ownership or listing state no longer matches the immutable order snapshot.");
            var drafts = await db.ArtworkRevisions.Where(x => x.ArtworkId == artwork.Id && x.Status == ArtworkRevisionStatus.Draft && x.CreatedByOwnerUserId == order.SellerUserId).ToListAsync(ct);
            foreach (var draft in drafts) draft.Discard(order.SellerUserId, "OwnershipChanged", now);
            var openCases = await db.ReviewCases.Where(x => x.ArtworkId == artwork.Id && x.Status == ReviewCaseStatus.Open && x.SubmitterUserId == order.SellerUserId).ToListAsync(ct);
            foreach (var reviewCase in openCases)
            {
                var rounds = await db.ReviewRounds.Where(x => x.ReviewCaseId == reviewCase.Id && x.Status == ReviewRoundStatus.PendingReview).ToListAsync(ct);
                foreach (var round in rounds) round.Withdraw(now);
                reviewCase.Withdraw(now);
            }
            artwork.TransferOwnership(order.SellerUserId, order.BuyerUserId); listing.CompleteOwnershipTransfer(now);
            db.OwnershipTransfers.Add(new OwnershipTransfer(Guid.NewGuid(), order.Id, item.Id, artwork.Id, listing.Id, application.Id, order.SellerUserId, order.BuyerUserId, now));
        }
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(), "ownership.transferred", actorId, order.Id, JsonSerializer.Serialize(new { itemCount = items.Count, fromUserId = order.SellerUserId, toUserId = order.BuyerUserId }), null, now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, TransferCount: items.Count);
    }
}
