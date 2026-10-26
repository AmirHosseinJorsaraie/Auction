using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Application.Marketplace;
using ArtMarketplace.Application.Orders;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Domain.Modules.Profiles;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Api;

internal static class Phase6Endpoints
{
    public static IEndpointRouteBuilder MapPhase6Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var cart = endpoints.MapGroup("/api/cart").RequireAuthorization().RequireRateLimiting("baseline").WithTags("Cart");
        cart.MapGet("/", GetCartAsync); cart.MapPost("/items", AddCartItemAsync); cart.MapDelete("/items/{listingId:guid}", RemoveCartItemAsync);
        endpoints.MapGet("/api/checkout/summary", GetCheckoutSummaryAsync).RequireAuthorization().RequireRateLimiting("baseline").WithTags("Checkout");
        endpoints.MapPost("/api/checkout", CheckoutAsync).RequireAuthorization().RequireRateLimiting("checkout").WithTags("Checkout");
        var orders = endpoints.MapGroup("/api/orders").RequireAuthorization().RequireRateLimiting("baseline").WithTags("Orders");
        orders.MapGet("/mine", GetMyOrdersAsync); orders.MapGet("/{orderId:guid}", GetOrderAsync); orders.MapPost("/{orderId:guid}/cancel", CancelOrderAsync);
        endpoints.MapGet("/api/seller/orders", GetSellerOrdersAsync).RequireAuthorization().RequireRateLimiting("baseline").WithTags("Seller orders");
        endpoints.MapPost("/api/seller/orders/{orderId:guid}/processing", StartProcessingAsync).RequireAuthorization().RequireRateLimiting("baseline").WithTags("Seller orders");
        endpoints.MapPost("/api/seller/orders/{orderId:guid}/ship", ShipAsync).RequireAuthorization().RequireRateLimiting("baseline").WithTags("Seller orders");
        endpoints.MapPost("/api/seller/orders/{orderId:guid}/delivered", DeliverAsync).RequireAuthorization().RequireRateLimiting("baseline").WithTags("Seller orders");
        endpoints.MapGet("/api/backoffice/orders", GetBackofficeOrdersAsync).RequireAuthorization(MarketplacePolicies.StaffWithMfa).RequireRateLimiting("baseline").WithTags("Backoffice orders");
        return endpoints;
    }

    private static async Task<IResult> GetCartAsync(ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        var cart = await db.Carts.AsNoTracking().SingleOrDefaultAsync(x => x.BuyerUserId == buyerId, ct);
        if (cart is null) return Results.Ok(new { items = Array.Empty<object>(), total = 0m, currency = (string?)null, version = 0L });
        var items = await CartRows(db, cart.Id).OrderBy(x => x.AddedAtUtc).ToListAsync(ct);
        return Results.Ok(new { items, total = items.Sum(x => x.CurrentPriceAmount), cart.Currency, cart.Version });
    }

    private static async Task<IResult> AddCartItemAsync(CartItemRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        var location = await db.MarketplaceListings.AsNoTracking().Where(x => x.Id == request.ListingId).Select(x => new { x.ArtworkId }).SingleOrDefaultAsync(ct);
        if (location is null) return Results.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var now = time.GetUtcNow();
        var buyer = await LockUser(db, buyerId, ct); _ = await LockArtwork(db, location.ArtworkId, ct); var listing = await LockListing(db, request.ListingId, ct);
        if (buyer?.IsEnabled != true) return Conflict("The buyer account is disabled.");
        if (listing is null || listing.Status != MarketplaceListingStatus.Published) return Conflict("The listing is not currently available.");
        if (listing.SellerUserId == buyerId) return Conflict("A seller cannot buy their own listing.");
        var cart = await db.Carts.FromSqlInterpolated($"SELECT * FROM orders.carts WHERE \"BuyerUserId\" = {buyerId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (cart is null) { cart = new Cart(Guid.NewGuid(), buyerId, now); db.Carts.Add(cart); }
        if (await db.CartItems.AnyAsync(x => x.CartId == cart.Id && x.ListingId == listing.Id, ct)) return Results.Ok(new { cart.Id, cart.Version });
        try { cart.AcceptItem(listing.SellerUserId, listing.Currency, now); db.CartItems.Add(new CartItem(Guid.NewGuid(), cart.Id, listing.Id, listing.PriceAmount, listing.Currency, now)); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { cart.Id, cart.Version }); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    private static async Task<IResult> RemoveCartItemAsync(Guid listingId, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var cart = await db.Carts.FromSqlInterpolated($"SELECT * FROM orders.carts WHERE \"BuyerUserId\" = {buyerId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (cart is null) return Results.NoContent();
        var item = await db.CartItems.SingleOrDefaultAsync(x => x.CartId == cart.Id && x.ListingId == listingId, ct);
        if (item is null) return Results.NoContent();
        db.CartItems.Remove(item); var remaining = await db.CartItems.CountAsync(x => x.CartId == cart.Id && x.Id != item.Id, ct);
        cart.ItemRemoved(remaining == 0, time.GetUtcNow()); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> GetCheckoutSummaryAsync(Guid addressId, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        var address = await db.SavedAddresses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == addressId && x.UserId == buyerId, ct);
        if (address is null) return Validation("A shipping address owned by the buyer is required.");
        var cart = await db.Carts.AsNoTracking().SingleOrDefaultAsync(x => x.BuyerUserId == buyerId, ct); if (cart is null) return Conflict("The cart is empty.");
        var items = await CartRows(db, cart.Id).OrderBy(x => x.AddedAtUtc).ToListAsync(ct); if (items.Count == 0) return Conflict("The cart is empty.");
        return Results.Ok(new { items, total = items.Sum(x => x.CurrentPriceAmount), cart.Currency, priceChanged = items.Any(x => x.PriceChanged), address = new { address.Id, address.RecipientName, address.AddressLine, address.PostalCode } });
    }

    private static async Task<IResult> CheckoutAsync(CheckoutRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, IConfiguration config, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100) return Validation("A valid idempotency key is required.");
        var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.BuyerUserId == buyerId && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null) return Results.Ok(new { existing.Id, existing.Status, existing.TotalAmount, existing.Currency, existing.ReservationExpiresAtUtc, idempotentReplay = true });
        var cartPlan = await (from plannedCart in db.Carts.AsNoTracking() join item in db.CartItems.AsNoTracking() on plannedCart.Id equals item.CartId join listing in db.MarketplaceListings.AsNoTracking() on item.ListingId equals listing.Id where plannedCart.BuyerUserId == buyerId orderby listing.ArtworkId select new { CartId = plannedCart.Id, listing.Id, listing.ArtworkId, listing.SellerUserId }).ToListAsync(ct);
        if (cartPlan.Count == 0) return Conflict("The cart is empty.");
        var sellerId = cartPlan[0].SellerUserId; if (cartPlan.Any(x => x.SellerUserId != sellerId)) return Conflict("The cart contains more than one seller.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var now = time.GetUtcNow();
        ApplicationUser? buyer = null; ApplicationUser? seller = null;
        foreach (var userId in new[] { buyerId, sellerId }.Distinct().OrderBy(x => x)) { var user = await LockUser(db, userId, ct); if (userId == buyerId) buyer = user; if (userId == sellerId) seller = user; }
        var eligibility = await LockEligibility(db, sellerId, ct);
        foreach (var artworkId in cartPlan.Select(x => x.ArtworkId).Distinct().OrderBy(x => x)) _ = await LockArtwork(db, artworkId, ct);
        var listings = new List<MarketplaceListing>(); foreach (var id in cartPlan.Select(x => x.Id)) { var listing = await LockListing(db, id, ct); if (listing is not null) listings.Add(listing); }
        var cart = await db.Carts.FromSqlInterpolated($"SELECT * FROM orders.carts WHERE \"Id\" = {cartPlan[0].CartId} FOR UPDATE").SingleOrDefaultAsync(ct);
        var replay = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.BuyerUserId == buyerId && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) { await tx.RollbackAsync(ct); return Results.Ok(new { replay.Id, replay.Status, replay.TotalAmount, replay.Currency, replay.ReservationExpiresAtUtc, idempotentReplay = true }); }
        var address = await db.SavedAddresses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == buyerId, ct);
        var buyerDecision = CheckoutPolicy.CanCheckout(new BuyerCheckoutContext(buyer?.IsEnabled == true, buyer?.EmailConfirmed == true, buyer?.PhoneNumberConfirmed == true, address is not null));
        if (!buyerDecision.Allowed) return Conflict(buyerDecision.Reason!);
        if (cart is null || listings.Count != cartPlan.Count) return Conflict("The cart changed; reload before checkout.");
        var invalidatedListings = new HashSet<Guid>();
        foreach (var listing in listings)
        {
            if (await db.SellerEligibilityInvalidations.AsNoTracking().AnyAsync(inv => inv.UserId == sellerId && listing.SellerEligibilityVersionAtPublish < inv.InvalidatedVersion && !db.SellerEligibilityInvalidationResolutions.Any(res => res.InvalidationId == inv.Id && res.TargetType == "MarketplaceListing" && res.TargetId == listing.Id), ct)) invalidatedListings.Add(listing.Id);
        }
        var eligibilityCurrent = seller?.IsEnabled == true && eligibility?.CanSellAt(now) == true;
        var stale = listings.Where(x => !eligibilityCurrent || x.SellerEligibilityVersionAtPublish != eligibility!.EligibilityVersion || invalidatedListings.Contains(x.Id)).ToList();
        if (stale.Count > 0)
        {
            foreach (var listing in stale.Where(x => x.Status == MarketplaceListingStatus.Published)) { listing.Suspend("Seller eligibility snapshot is stale at checkout.", now); Audit(db, "listing.suspended.checkout-stale", buyerId, listing.ArtworkId, now, new { listing.Id }); }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Conflict("Seller eligibility changed. The cart was not checked out.");
        }
        foreach (var listing in listings)
        {
            if (listing.Status != MarketplaceListingStatus.Published || listing.SellerUserId != sellerId || listing.PublishedArtworkRevisionId is null) return Conflict("A listing is no longer available.");
            var owner = await db.Artworks.AsNoTracking().Where(x => x.Id == listing.ArtworkId).Select(x => x.CurrentOwnerUserId).SingleAsync(ct);
            var approved = await EffectiveApproval(db, listing.PublishedArtworkRevisionId.Value, ct); if (owner != sellerId || !approved) return Conflict("A listing no longer has a valid owner or approval.");
        }
        var cartItems = await db.CartItems.Where(x => x.CartId == cart.Id).ToListAsync(ct); var total = listings.Sum(x => x.PriceAmount);
        if (request.ExpectedTotalAmount != total || request.ExpectedCurrency != cart.Currency) return Results.Conflict(new { message = "The price changed; review and confirm the new total.", total, cart.Currency });
        var orderId = Guid.NewGuid(); var expiry = now.AddMinutes(Math.Clamp(config.GetValue("Orders:ReservationMinutes", 15), 5, 120));
        var sellerName = await db.UserProfiles.AsNoTracking().Where(x => x.UserId == sellerId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) ?? "فروشنده آثار هنری";
        var order = new Order(orderId, buyerId, sellerId, total, cart.Currency!, eligibility!.EligibilityVersion, expiry, request.IdempotencyKey, address!.RecipientName, address.AddressLine, address.PostalCode, now); db.Orders.Add(order);
        foreach (var listing in listings.OrderBy(x => x.ArtworkId))
        {
            var title = await db.ArtworkRevisions.AsNoTracking().Where(x => x.Id == listing.PublishedArtworkRevisionId).Select(x => x.Title).SingleAsync(ct);
            var item = new OrderItem(Guid.NewGuid(), orderId, listing.Id, listing.ArtworkId, listing.PublishedArtworkRevisionId!.Value, title, listing.PriceAmount, listing.Currency, sellerName);
            db.OrderItems.Add(item); db.Reservations.Add(new Reservation(Guid.NewGuid(), orderId, item.Id, listing.Id, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), now)); listing.Reserve(now);
        }
        db.CartItems.RemoveRange(cartItems); cart.CheckedOut(now); Audit(db, "order.created", buyerId, orderId, now, new { itemCount = listings.Count, total, cart.Currency, expiry });
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Created($"/api/orders/{orderId}", new { order.Id, order.Status, order.TotalAmount, order.Currency, order.ReservationExpiresAtUtc }); }
        catch (DbUpdateException) { return Conflict("A concurrent checkout won or this request is already being processed."); }
    }

    private static async Task<IResult> GetMyOrdersAsync(ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) { if (!UserId(principal, out var id)) return Results.Unauthorized(); return Results.Ok(await OrderRows(db).Where(x => x.BuyerUserId == id).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)); }
    private static async Task<IResult> GetSellerOrdersAsync(ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct) { if (!UserId(principal, out var id)) return Results.Unauthorized(); return Results.Ok(await OrderRows(db).Where(x => x.SellerUserId == id).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)); }
    private static async Task<IResult> GetBackofficeOrdersAsync(MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await OrderRows(db).OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct));
    private static async Task<IResult> GetOrderAsync(Guid orderId, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out var userId)) return Results.Unauthorized(); var staff = principal.IsInRole("Expert") || principal.IsInRole("Admin") || principal.IsInRole("SuperAdmin");
        var order = await OrderRows(db).SingleOrDefaultAsync(x => x.Id == orderId && (staff || x.BuyerUserId == userId || x.SellerUserId == userId), ct); if (order is null) return Results.NotFound();
        var shipping = await db.Orders.AsNoTracking().Where(x => x.Id == orderId).Select(x => new { x.RecipientName, x.AddressLine, x.PostalCode }).SingleAsync(ct);
        var items = await db.OrderItems.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.Id).Select(x => new { x.Id, x.ListingId, x.ArtworkId, x.Title, x.PriceAmount, x.Currency, x.SellerDisplayName }).ToListAsync(ct);
        return Results.Ok(new { order = new { order.Id, order.BuyerUserId, order.SellerUserId, order.TotalAmount, order.Currency, order.Status, order.ReservationExpiresAtUtc, order.ReviewIntegrityHold, order.SellerEligibilityHold, order.HoldReason, order.TrackingCode, order.CreatedAtUtc, order.UpdatedAtUtc, order.Version, shipping.RecipientName, shipping.AddressLine, shipping.PostalCode }, items });
    }
    private static async Task<IResult> CancelOrderAsync(Guid orderId, VersionRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        var result = await Phase6OrderOperations.TerminateAsync(db, orderId, buyerId, request.ExpectedVersion, false, time.GetUtcNow(), ct);
        return result.Success ? Results.Ok(new { orderId, status = "Cancelled" }) : result.NotFound ? Results.NotFound() : Conflict(result.Error!);
    }

    private static Task<IResult> StartProcessingAsync(Guid orderId, VersionRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct) => FulfillmentAsync(orderId, request.ExpectedVersion, null, "processing", principal, db, time, ct);
    private static Task<IResult> ShipAsync(Guid orderId, ShippingRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct) => FulfillmentAsync(orderId, request.ExpectedVersion, request.TrackingCode, "ship", principal, db, time, ct);
    private static Task<IResult> DeliverAsync(Guid orderId, VersionRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct) => FulfillmentAsync(orderId, request.ExpectedVersion, null, "deliver", principal, db, time, ct);
    private static async Task<IResult> FulfillmentAsync(Guid orderId, long expectedVersion, string? trackingCode, string operation, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var sellerId)) return Results.Unauthorized(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _ = await LockUser(db, sellerId, ct); var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders.orders WHERE \"Id\" = {orderId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (order is null || order.SellerUserId != sellerId) return Results.NotFound(); if (order.Version != expectedVersion) return Conflict("The order changed; reload before continuing.");
        if (order.ReviewIntegrityHold || order.SellerEligibilityHold) return Conflict("The order is on an integrity hold."); var now = time.GetUtcNow();
        try { if (operation == "processing") order.StartProcessing(now); else if (operation == "ship") order.Ship(trackingCode ?? string.Empty, now); else order.Deliver(now); Audit(db, $"order.{operation}", sellerId, order.Id, now, new { trackingCode }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { order.Id, order.Status, order.TrackingCode, order.Version }); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return Conflict(ex.Message); }
    }

    private static IQueryable<CartRow> CartRows(MarketplaceDbContext db, Guid cartId) => from item in db.CartItems.AsNoTracking() join listing in db.MarketplaceListings.AsNoTracking() on item.ListingId equals listing.Id join revision in db.ArtworkRevisions.AsNoTracking() on listing.PublishedArtworkRevisionId equals revision.Id into revisions from revision in revisions.DefaultIfEmpty() where item.CartId == cartId select new CartRow(listing.Id, listing.ArtworkId, revision == null ? "اثر هنری" : revision.Title, listing.SellerUserId, listing.PriceAmount, listing.Currency, item.PriceAmountAtAdd, listing.PriceAmount != item.PriceAmountAtAdd || listing.Currency != item.CurrencyAtAdd, listing.Status, item.AddedAtUtc);
    private static IQueryable<OrderRow> OrderRows(MarketplaceDbContext db) => db.Orders.AsNoTracking().Select(x => new OrderRow(x.Id, x.BuyerUserId, x.SellerUserId, x.TotalAmount, x.Currency, x.Status, x.ReservationExpiresAtUtc, x.ReviewIntegrityHold, x.SellerEligibilityHold, x.HoldReason, x.TrackingCode, x.CreatedAtUtc, x.UpdatedAtUtc, x.Version));
    private static Task<ApplicationUser?> LockUser(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<SellerEligibility?> LockEligibility(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<Artwork?> LockArtwork(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<MarketplaceListing?> LockListing(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<bool> EffectiveApproval(MarketplaceDbContext db, Guid revisionId, CancellationToken ct) => db.ReviewDecisions.AsNoTracking().AnyAsync(x => x.ArtworkRevisionId == revisionId && x.Kind == ReviewDecisionKind.Approve && !db.ReviewDecisionSupersessions.Any(s => s.ReviewDecisionId == x.Id), ct);
    private static bool UserId(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out id);
    private static void Audit(MarketplaceDbContext db, string type, Guid actor, Guid subject, DateTimeOffset now, object data) => db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(), type, actor, subject, JsonSerializer.Serialize(data), null, now));
    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });
    private static IResult Conflict(string message) => Results.Conflict(new { message });
    private sealed record CartRow(Guid ListingId, Guid ArtworkId, string Title, Guid SellerUserId, decimal CurrentPriceAmount, string Currency, decimal PriceAmountAtAdd, bool PriceChanged, MarketplaceListingStatus Status, DateTimeOffset AddedAtUtc);
    private sealed record OrderRow(Guid Id, Guid BuyerUserId, Guid SellerUserId, decimal TotalAmount, string Currency, OrderStatus Status, DateTimeOffset ReservationExpiresAtUtc, bool ReviewIntegrityHold, bool SellerEligibilityHold, string? HoldReason, string? TrackingCode, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, long Version);
    internal sealed record CartItemRequest(Guid ListingId);
    internal sealed record CheckoutRequest(Guid AddressId, string IdempotencyKey, decimal ExpectedTotalAmount, string ExpectedCurrency);
    internal sealed record VersionRequest(long ExpectedVersion);
    internal sealed record ShippingRequest(string TrackingCode, long ExpectedVersion);
}
