using System.Data;
using System.Text.Json;
using ArtMarketplace.Application.Payments;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Domain.Modules.Payments;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtMarketplace.Api;

internal static class Phase7PaymentOperations
{
    internal sealed record CallbackResult(bool Accepted, bool Replay, bool Applied, bool ReconciliationRequired, string Code, Guid? OrderId = null);

    public static async Task<CallbackResult> ProcessVerifiedCallbackAsync(MarketplaceDbContext db, string providerKey,
        VerifiedProviderCallback callback, DateTimeOffset receivedAtUtc, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await ProcessCoreAsync(db, providerKey, callback, receivedAtUtc, ct); }
            catch (Exception exception) when (attempt < 3 && IsRetryable(exception))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt), ct);
            }
        }
    }

    private static async Task<CallbackResult> ProcessCoreAsync(MarketplaceDbContext db, string providerKey,
        VerifiedProviderCallback callback, DateTimeOffset receivedAtUtc, CancellationToken ct)
    {
        var existingEvent = await db.PaymentProviderEvents.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderKey == providerKey && x.EventId == callback.EventId, ct);
        if (existingEvent is not null)
        {
            var existingReceipt = await db.VerifiedPaymentReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.SourceProviderEventId == existingEvent.Id, ct);
            var applied = existingReceipt is not null && await db.OrderPaymentApplications.AsNoTracking().AnyAsync(x => x.VerifiedPaymentReceiptId == existingReceipt.Id, ct);
            var reconciled = existingReceipt is not null && await db.PaymentReconciliationCases.AsNoTracking().AnyAsync(x => x.VerifiedPaymentReceiptId == existingReceipt.Id, ct);
            var orderId = await db.PaymentAttempts.AsNoTracking().Where(x => x.Id == existingEvent.PaymentAttemptId).Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(ct);
            return new(true, true, applied, reconciled, "event-replay", orderId);
        }

        var attemptPlan = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderKey == providerKey && x.MerchantReference == callback.MerchantReference, ct);
        if (attemptPlan is null) return new(false, false, false, false, "unknown-merchant-reference");
        var orderPlan = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == attemptPlan.OrderId, ct);
        var itemPlan = await db.OrderItems.AsNoTracking().Where(x => x.OrderId == orderPlan.Id).OrderBy(x => x.ArtworkId).ToListAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var sellerUser = await LockUser(db, orderPlan.SellerUserId, ct);
        var eligibility = await LockEligibility(db, orderPlan.SellerUserId, ct);
        foreach (var item in itemPlan)
        {
            _ = await LockArtwork(db, item.ArtworkId, ct);
            _ = await db.ReviewCases.FromSqlInterpolated($"SELECT * FROM reviews.review_cases WHERE \"ArtworkId\" = {item.ArtworkId} ORDER BY \"Id\" FOR UPDATE").ToListAsync(ct);
        }
        var listings = new List<MarketplaceListing>();
        foreach (var item in itemPlan) { var listing = await LockListing(db, item.ListingId, ct); if (listing is not null) listings.Add(listing); }
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders.orders WHERE \"Id\" = {orderPlan.Id} FOR UPDATE").SingleAsync(ct);
        var attempt = await db.PaymentAttempts.FromSqlInterpolated($"SELECT * FROM payments.payment_attempts WHERE \"Id\" = {attemptPlan.Id} FOR UPDATE").SingleAsync(ct);

        var racedEvent = await db.PaymentProviderEvents.AsNoTracking().AnyAsync(x => x.ProviderKey == providerKey && x.EventId == callback.EventId, ct);
        if (racedEvent) { await tx.RollbackAsync(ct); return new(true, true, false, false, "event-replay", order.Id); }

        var providerEvent = new PaymentProviderEvent(Guid.NewGuid(), attempt.Id, providerKey, callback.EventId,
            callback.ProviderTransactionId, callback.Outcome, callback.Amount, callback.Currency, callback.MerchantReference,
            callback.MerchantAccount, callback.OccurredAtUtc, receivedAtUtc, callback.PayloadDigest);
        db.PaymentProviderEvents.Add(providerEvent);
        attempt.RecordOutcome(callback.Outcome, receivedAtUtc);

        if (callback.Outcome != PaymentOutcome.Succeeded)
        {
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new(true, false, false, false, callback.Outcome == PaymentOutcome.Failed ? "payment-failed" : "payment-cancelled", order.Id);
        }

        var duplicateReceipt = await db.VerifiedPaymentReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderKey == providerKey && x.ProviderTransactionId == callback.ProviderTransactionId, ct);
        if (duplicateReceipt is not null)
        {
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new(true, false, false, false, "transaction-replay", order.Id);
        }

        var receipt = new VerifiedPaymentReceipt(Guid.NewGuid(), attempt.Id, providerEvent.Id, providerKey,
            callback.ProviderTransactionId, callback.Amount, callback.Currency, receivedAtUtc);
        db.VerifiedPaymentReceipts.Add(receipt);

        var failure = await ValidateApplicationAsync(db, order, attempt, callback, sellerUser, eligibility, listings, itemPlan, receivedAtUtc, ct);
        var alreadyApplied = await db.OrderPaymentApplications.AsNoTracking().AnyAsync(x => x.OrderId == order.Id, ct);
        if (alreadyApplied) failure ??= ("extra-success", "A different verified receipt was already applied to this order.");

        if (failure is not null)
        {
            db.PaymentReconciliationCases.Add(new PaymentReconciliationCase(Guid.NewGuid(), order.Id, receipt.Id, failure.Value.Code, failure.Value.Details, receivedAtUtc));
            Audit(db, "payment.receipt.reconciliation-required", order.BuyerUserId, order.Id, receivedAtUtc, new { receipt.Id, failure.Value.Code });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new(true, false, false, true, failure.Value.Code, order.Id);
        }

        var application = new OrderPaymentApplication(Guid.NewGuid(), order.Id, receipt.Id, receivedAtUtc);
        db.OrderPaymentApplications.Add(application); order.ConfirmPayment(receivedAtUtc);
        foreach (var listing in listings) listing.MarkSold(receivedAtUtc);
        foreach (var reservation in await db.Reservations.Where(x => x.OrderId == order.Id && x.Status == ReservationStatus.Active).ToListAsync(ct)) reservation.ConvertToSale(receivedAtUtc);
        Audit(db, "payment.applied", order.BuyerUserId, order.Id, receivedAtUtc, new { receiptId = receipt.Id, applicationId = application.Id });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, false, true, false, "payment-applied", order.Id);
    }

    private static async Task<(string Code, string Details)?> ValidateApplicationAsync(MarketplaceDbContext db, Order order,
        PaymentAttempt attempt, VerifiedProviderCallback callback, ApplicationUser? sellerUser, SellerEligibility? eligibility, List<MarketplaceListing> listings,
        List<OrderItem> items, DateTimeOffset now, CancellationToken ct)
    {
        if (attempt.ProviderTransactionId != callback.ProviderTransactionId) return ("transaction-mismatch", "The provider transaction does not match the initiated attempt.");
        if (attempt.Amount != callback.Amount || order.TotalAmount != callback.Amount) return ("amount-mismatch", "The verified amount does not match the order snapshot.");
        if (!string.Equals(attempt.Currency, callback.Currency, StringComparison.Ordinal) || !string.Equals(order.Currency, callback.Currency, StringComparison.Ordinal)) return ("currency-mismatch", "The verified currency does not match the order snapshot.");
        if (attempt.MerchantAccount != callback.MerchantAccount) return ("merchant-account-mismatch", "The callback merchant account does not match the attempt.");
        if (order.Status != OrderStatus.AwaitingPayment) return ("order-not-payable", "The order is no longer awaiting payment.");
        if (now > order.ReservationExpiresAtUtc) return ("late-success", "Payment succeeded after the order reservation deadline; refund review is required.");
        if (order.ReviewIntegrityHold || order.SellerEligibilityHold) return ("order-on-hold", "The order has an integrity or seller eligibility hold.");
        if (sellerUser?.IsEnabled != true || eligibility?.CanSellAt(now) != true || eligibility.EligibilityVersion != order.SellerEligibilityVersionAtCheckout) return ("seller-eligibility-changed", "Seller account or eligibility no longer matches the checkout snapshot.");
        if (listings.Count != items.Count) return ("listing-set-changed", "The order listing set is incomplete.");
        foreach (var item in items)
        {
            var listing = listings.SingleOrDefault(x => x.Id == item.ListingId);
            if (listing is null || listing.Status != MarketplaceListingStatus.Reserved || listing.SellerUserId != order.SellerUserId || listing.ArtworkId != item.ArtworkId || listing.PublishedArtworkRevisionId != item.ArtworkRevisionId) return ("listing-snapshot-mismatch", "A reserved listing no longer matches the order snapshot.");
            if (listing.SellerEligibilityVersionAtPublish != order.SellerEligibilityVersionAtCheckout) return ("listing-eligibility-mismatch", "A listing eligibility snapshot differs from the order snapshot.");
            var invalidated = await db.SellerEligibilityInvalidations.AsNoTracking().AnyAsync(inv => inv.UserId == order.SellerUserId && order.SellerEligibilityVersionAtCheckout < inv.InvalidatedVersion && !db.SellerEligibilityInvalidationResolutions.Any(res => res.InvalidationId == inv.Id && res.TargetType == "Order" && res.TargetId == order.Id), ct);
            if (invalidated) return ("eligibility-invalidation-unresolved", "An eligibility invalidation affecting this order is unresolved.");
            var approved = await db.ReviewDecisions.AsNoTracking().AnyAsync(x => x.ArtworkRevisionId == item.ArtworkRevisionId && x.Kind == ReviewDecisionKind.Approve && !db.ReviewDecisionSupersessions.Any(s => s.ReviewDecisionId == x.Id), ct);
            if (!approved) return ("approval-superseded", "An artwork approval is no longer effective.");
        }
        return null;
    }

    private static Task<ApplicationUser?> LockUser(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<SellerEligibility?> LockEligibility(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<Artwork?> LockArtwork(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<MarketplaceListing?> LockListing(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static bool IsRetryable(Exception exception) => exception is DbUpdateException || exception.ToString().Contains("40001", StringComparison.Ordinal) || exception.ToString().Contains("40P01", StringComparison.Ordinal);
    private static void Audit(MarketplaceDbContext db, string type, Guid actor, Guid subject, DateTimeOffset now, object data) => db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(), type, actor, subject, JsonSerializer.Serialize(data), null, now));
}
