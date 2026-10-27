using System.Data;
using System.Security.Claims;
using System.Text.Json;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Domain.Modules.Administration;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Api;

internal static class Phase8Endpoints
{
    private static readonly string[] AllowedRoles = ["User", "Expert", "Admin", "SuperAdmin"];

    public static void MapPhase8Endpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/backoffice/authorization/probe/administration", () => Results.NoContent()).RequireAuthorization(MarketplacePolicies.AdministrationRead).WithTags("Authorization matrix");
        endpoints.MapGet("/api/backoffice/authorization/probe/users", () => Results.NoContent()).RequireAuthorization(MarketplacePolicies.UsersManage).WithTags("Authorization matrix");
        endpoints.MapGet("/api/backoffice/authorization/probe/roles", () => Results.NoContent()).RequireAuthorization(MarketplacePolicies.RolesManage).WithTags("Authorization matrix");
        endpoints.MapGet("/api/backoffice/authorization/probe/reviews", () => Results.NoContent()).RequireAuthorization(MarketplacePolicies.ReviewsDecide).WithTags("Authorization matrix");
        var read = endpoints.MapGroup("/api/backoffice/administration").RequireAuthorization(MarketplacePolicies.AdministrationRead).RequireRateLimiting("baseline").WithTags("Administration");
        read.MapGet("/dashboard", DashboardAsync); read.MapGet("/catalog", CatalogMonitorAsync); read.MapGet("/orders", OrdersMonitorAsync); read.MapGet("/payments", PaymentsMonitorAsync);
        endpoints.MapPost("/api/backoffice/administration/listings/{listingId:guid}/suspend", SuspendListingAsync).RequireAuthorization(MarketplacePolicies.CatalogManage).RequireRateLimiting("baseline").WithTags("Administration catalog");
        var users = endpoints.MapGroup("/api/backoffice/users").RequireRateLimiting("baseline").WithTags("Administration users");
        users.MapGet("/", UsersAsync).RequireAuthorization(MarketplacePolicies.UsersManage);
        users.MapPost("/{userId:guid}/status", UserStatusAsync).RequireAuthorization(MarketplacePolicies.UsersManage);
        users.MapPut("/{userId:guid}/roles/{role}", RoleAsync).RequireAuthorization(MarketplacePolicies.RolesManage);
        var categories = endpoints.MapGroup("/api/backoffice/categories").RequireAuthorization(MarketplacePolicies.CatalogManage).RequireRateLimiting("baseline").WithTags("Administration catalog");
        categories.MapGet("/", CategoriesAsync); categories.MapPost("/", CreateCategoryAsync); categories.MapPut("/{categoryId:guid}", UpdateCategoryAsync);
        var content = endpoints.MapGroup("/api/backoffice/content").RequireAuthorization(MarketplacePolicies.ContentManage).RequireRateLimiting("baseline").WithTags("Administration content");
        content.MapGet("/pages", ContentPagesAsync); content.MapPost("/pages", CreateContentPageAsync); content.MapPut("/pages/{pageId:guid}", UpdateContentPageAsync); content.MapGet("/banners", BannersAsync); content.MapPost("/banners", CreateBannerAsync);
        endpoints.MapGet("/api/backoffice/audit", AuditAsync).RequireAuthorization(MarketplacePolicies.AuditRead).RequireRateLimiting("baseline").WithTags("Administration audit");
        endpoints.MapGet("/api/backoffice/configuration", ConfigurationAsync).RequireAuthorization(MarketplacePolicies.SuperAdminWithMfa).RequireRateLimiting("baseline").WithTags("Administration configuration");
        endpoints.MapGet("/api/public/content/pages/{slug}", PublicContentPageAsync).AllowAnonymous().RequireRateLimiting("baseline").WithTags("Public content");
        endpoints.MapGet("/api/public/content/banner", PublicBannerAsync).AllowAnonymous().RequireRateLimiting("baseline").WithTags("Public content");
    }

    private static async Task<IResult> DashboardAsync(MarketplaceDbContext db, CancellationToken ct)
    {
        var today = DateTimeOffset.UtcNow.Date;
        return Results.Ok(new
        {
            users = await db.Users.CountAsync(ct), enabledUsers = await db.Users.CountAsync(x => x.IsEnabled, ct),
            pendingVerifications = await db.SellerVerificationCases.CountAsync(x => x.Status == VerificationStatus.Pending, ct),
            openReviews = await db.ReviewCases.CountAsync(x => x.Status == Domain.Modules.Reviews.ReviewCaseStatus.Open, ct),
            publishedListings = await db.MarketplaceListings.CountAsync(x => x.Status == MarketplaceListingStatus.Published, ct),
            reservedListings = await db.MarketplaceListings.CountAsync(x => x.Status == MarketplaceListingStatus.Reserved, ct),
            awaitingPaymentOrders = await db.Orders.CountAsync(x => x.Status == OrderStatus.AwaitingPayment, ct),
            paidOrders = await db.Orders.CountAsync(x => x.Status >= OrderStatus.Confirmed, ct),
            openReconciliations = await db.PaymentReconciliationCases.CountAsync(x => x.Status == Domain.Modules.Payments.ReconciliationStatus.Open, ct),
            eventsToday = await db.SecurityAuditEvents.CountAsync(x => x.CreatedAtUtc >= today, ct)
        });
    }

    private static async Task<IResult> CatalogMonitorAsync(int page, int pageSize, MarketplaceDbContext db, CancellationToken ct)
    {
        (page, pageSize) = Page(page, pageSize); var query = db.MarketplaceListings.AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc);
        var total = await query.CountAsync(ct); var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.ArtworkId, x.SellerUserId, x.PriceAmount, x.Currency, x.Status, x.UpdatedAtUtc, x.Version }).ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total });
    }
    private static async Task<IResult> OrdersMonitorAsync(int page, int pageSize, MarketplaceDbContext db, CancellationToken ct)
    { (page,pageSize)=Page(page,pageSize);var query=db.Orders.AsNoTracking().OrderByDescending(x=>x.CreatedAtUtc);var total=await query.CountAsync(ct);var items=await query.Skip((page-1)*pageSize).Take(pageSize).Select(x=>new{x.Id,x.TotalAmount,x.Currency,x.Status,x.ReviewIntegrityHold,x.SellerEligibilityHold,x.CreatedAtUtc,x.UpdatedAtUtc}).ToListAsync(ct);return Results.Ok(new{items,page,pageSize,total}); }
    private static async Task<IResult> PaymentsMonitorAsync(int page, int pageSize, MarketplaceDbContext db, CancellationToken ct)
    { (page,pageSize)=Page(page,pageSize);var query=db.PaymentAttempts.AsNoTracking().OrderByDescending(x=>x.CreatedAtUtc);var total=await query.CountAsync(ct);var items=await query.Skip((page-1)*pageSize).Take(pageSize).Select(x=>new{x.Id,x.OrderId,x.ProviderKey,x.Amount,x.Currency,x.Status,x.FailureCode,x.CreatedAtUtc,x.UpdatedAtUtc}).ToListAsync(ct);return Results.Ok(new{items,page,pageSize,total}); }
    private static async Task<IResult> SuspendListingAsync(Guid listingId, ReasonRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    { if(!UserId(principal,out var actorId))return Results.Unauthorized();if(request.Reason.Trim().Length is <3 or >1000)return Validation("A reason is required.");var listing=await db.MarketplaceListings.SingleOrDefaultAsync(x=>x.Id==listingId,ct);if(listing is null)return Results.NotFound();try{listing.Suspend(request.Reason,time.GetUtcNow());Audit(db,"admin.listing-suspended",actorId,listing.ArtworkId,time.GetUtcNow(),new{listing.Id,request.Reason});await db.SaveChangesAsync(ct);return Results.Ok(new{listing.Id,listing.Status,listing.Version});}catch(InvalidOperationException ex){return Conflict(ex.Message);} }

    private static async Task<IResult> UsersAsync(string? search, bool? enabled, int page, int pageSize, MarketplaceDbContext db, CancellationToken ct)
    {
        (page, pageSize) = Page(page, pageSize); var normalized = search?.Trim();
        var query = from user in db.Users.AsNoTracking() join profile in db.UserProfiles.AsNoTracking() on user.Id equals profile.UserId into profiles from profile in profiles.DefaultIfEmpty() where (!enabled.HasValue || user.IsEnabled == enabled) && (string.IsNullOrEmpty(normalized) || (user.Email != null && EF.Functions.ILike(user.Email, $"%{normalized}%")) || (profile != null && EF.Functions.ILike(profile.DisplayName, $"%{normalized}%"))) select new { user.Id, user.Email, displayName = profile == null ? null : profile.DisplayName, user.IsEnabled, user.EmailConfirmed, user.TwoFactorEnabled, user.AuthorizationVersion };
        var total = await query.CountAsync(ct); var rows = await query.OrderBy(x => x.Email).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); var ids = rows.Select(x => x.Id).ToArray();
        var roles = await (from link in db.UserRoles.AsNoTracking() join role in db.Roles.AsNoTracking() on link.RoleId equals role.Id where ids.Contains(link.UserId) select new { link.UserId, role.Name }).ToListAsync(ct);
        var items = rows.Select(x => new { x.Id, x.Email, x.displayName, x.IsEnabled, x.EmailConfirmed, x.TwoFactorEnabled, x.AuthorizationVersion, roles = roles.Where(r => r.UserId == x.Id).Select(r => r.Name).Where(n => n != null).ToArray() });
        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> UserStatusAsync(Guid userId, UserStatusRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var actorId)) return Results.Unauthorized(); if (request.Reason.Trim().Length is < 3 or > 1_000) return Validation("A reason containing 3 to 1000 characters is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var now = time.GetUtcNow();
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct); if (user is null) return Results.NotFound();
        if (!request.Enabled && actorId == userId) return Conflict("You cannot disable your own staff account.");
        var superAdminRole = await db.Roles.AsNoTracking().SingleAsync(x => x.Name == "SuperAdmin", ct); var isSuperAdmin = await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == superAdminRole.Id, ct);
        if (!request.Enabled && isSuperAdmin && await db.UserRoles.CountAsync(x => x.RoleId == superAdminRole.Id, ct) <= 1) return Conflict("The last SuperAdmin cannot be disabled.");
        if (user.IsEnabled == request.Enabled) { await tx.RollbackAsync(ct); return Results.Ok(new { user.Id, user.IsEnabled, idempotentReplay = true }); }
        user.IsEnabled = request.Enabled; user.AuthorizationVersion = checked(user.AuthorizationVersion + 1);
        var sessions = await db.BffSessions.Where(x => x.SubjectUserId == userId && x.RevokedAtUtc == null).ToListAsync(ct); foreach (var session in sessions) { session.RevokedAtUtc = now; session.Version = checked(session.Version + 1); }
        var eligibility = await db.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (!request.Enabled && eligibility is not null)
        {
            var invalidation = eligibility.DisableAccount(request.Reason, now); db.SellerEligibilityInvalidations.Add(invalidation);
            await SafeguardSellerListingsAsync(db, userId, request.Reason, now, ct);
        }
        else if (request.Enabled && eligibility is not null) eligibility.ReactivateAccount();
        Audit(db, request.Enabled ? "admin.user-reactivated" : "admin.user-disabled", actorId, userId, now, new { request.Reason, revokedSessions = sessions.Count });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { user.Id, user.IsEnabled, user.AuthorizationVersion });
    }

    private static async Task<IResult> RoleAsync(Guid userId, string role, RoleRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var actorId)) return Results.Unauthorized(); if (!AllowedRoles.Contains(role, StringComparer.Ordinal)) return Validation("Unknown role."); if (request.Reason.Trim().Length is < 3 or > 1_000) return Validation("A reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var user = await db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct); if (user is null) return Results.NotFound();
        var targetRole = await db.Roles.SingleAsync(x => x.Name == role, ct); var existing = await db.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == targetRole.Id, ct);
        var roleRejection = RoleManagementPolicy.Rejection(actorId, userId, role, request.Assign, existing is not null, role == "SuperAdmin" ? await db.UserRoles.CountAsync(x => x.RoleId == targetRole.Id, ct) : 0);
        if (roleRejection is not null) return Conflict(roleRejection);
        if (request.Assign && existing is null) db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = targetRole.Id });
        if (!request.Assign && existing is not null) db.UserRoles.Remove(existing);
        user.AuthorizationVersion = checked(user.AuthorizationVersion + 1); var now = time.GetUtcNow();
        var sessions = await db.BffSessions.Where(x => x.SubjectUserId == userId && x.RevokedAtUtc == null).ToListAsync(ct); foreach (var session in sessions) { session.RevokedAtUtc = now; session.Version = checked(session.Version + 1); }
        Audit(db, request.Assign ? "admin.role-assigned" : "admin.role-removed", actorId, userId, now, new { role, request.Reason }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { userId, role, request.Assign });
    }

    private static async Task<IResult> CategoriesAsync(MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await db.Categories.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct));
    private static async Task<IResult> CreateCategoryAsync(CategoryRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    { if (!UserId(principal, out var actorId)) return Results.Unauthorized(); try { var item = new Category(Guid.NewGuid(), request.Name, request.Slug, request.DisplayOrder); if (!request.IsActive) item.Deactivate(); db.Categories.Add(item); Audit(db, "admin.category-created", actorId, item.Id, time.GetUtcNow(), new { item.Slug }); await db.SaveChangesAsync(ct); return Results.Created($"/api/backoffice/categories/{item.Id}", item); } catch (Exception ex) when (ex is ArgumentException or DbUpdateException) { return Conflict("Category values must be valid and the slug must be unique."); } }
    private static async Task<IResult> UpdateCategoryAsync(Guid categoryId, CategoryRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    { if (!UserId(principal, out var actorId)) return Results.Unauthorized(); var item = await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId, ct); if (item is null) return Results.NotFound(); try { item.Update(request.Name, request.Slug, request.DisplayOrder, request.IsActive); Audit(db, "admin.category-updated", actorId, item.Id, time.GetUtcNow(), new { item.Slug, item.IsActive }); await db.SaveChangesAsync(ct); return Results.Ok(item); } catch (Exception ex) when (ex is ArgumentException or DbUpdateException) { return Conflict("Category values must be valid and the slug must be unique."); } }
    private static async Task<IResult> ContentPagesAsync(MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await db.ManagedContentPages.AsNoTracking().OrderBy(x => x.Slug).ToListAsync(ct));
    private static async Task<IResult> CreateContentPageAsync(ContentPageRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    { if (!UserId(principal, out var actorId)) return Results.Unauthorized(); try { var page = new ManagedContentPage(Guid.NewGuid(), request.Slug, request.Title, request.Body, request.IsPublished, actorId, time.GetUtcNow()); db.ManagedContentPages.Add(page); Audit(db, "admin.content-created", actorId, page.Id, time.GetUtcNow(), new { page.Slug }); await db.SaveChangesAsync(ct); return Results.Created($"/api/backoffice/content/pages/{page.Id}", page); } catch (Exception ex) when (ex is ArgumentException or DbUpdateException) { return Conflict("Content is invalid or the slug already exists."); } }
    private static async Task<IResult> UpdateContentPageAsync(Guid pageId, ContentPageRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    { if (!UserId(principal, out var actorId)) return Results.Unauthorized(); var page = await db.ManagedContentPages.SingleOrDefaultAsync(x => x.Id == pageId, ct); if (page is null) return Results.NotFound(); try { page.Update(request.Title, request.Body, request.IsPublished, actorId, request.ExpectedVersion, time.GetUtcNow()); Audit(db, "admin.content-updated", actorId, page.Id, time.GetUtcNow(), new { page.Slug, page.IsPublished }); await db.SaveChangesAsync(ct); return Results.Ok(page); } catch (InvalidOperationException ex) { return Conflict(ex.Message); } }
    private static async Task<IResult> BannersAsync(MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await db.SiteBanners.AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc).Take(100).ToListAsync(ct));
    private static async Task<IResult> CreateBannerAsync(BannerRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    { if (!UserId(principal, out var actorId)) return Results.Unauthorized(); try { var banner = new SiteBanner(Guid.NewGuid(), request.Message, request.StartsAtUtc, request.EndsAtUtc, request.IsEnabled, actorId, time.GetUtcNow()); db.SiteBanners.Add(banner); Audit(db, "admin.banner-created", actorId, banner.Id, time.GetUtcNow(), new { banner.IsEnabled, banner.StartsAtUtc, banner.EndsAtUtc }); await db.SaveChangesAsync(ct); return Results.Created($"/api/backoffice/content/banners/{banner.Id}", banner); } catch (ArgumentException ex) { return Validation(ex.Message); } }
    private static async Task<IResult> AuditAsync(string? eventType, int page, int pageSize, MarketplaceDbContext db, CancellationToken ct)
    { (page, pageSize) = Page(page, pageSize); var query = db.SecurityAuditEvents.AsNoTracking().Where(x => string.IsNullOrEmpty(eventType) || x.EventType == eventType).OrderByDescending(x => x.CreatedAtUtc); var total = await query.CountAsync(ct); var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.EventType, x.ActorUserId, x.SubjectUserId, x.TraceId, x.CreatedAtUtc }).ToListAsync(ct); return Results.Ok(new { items, page, pageSize, total }); }
    private static IResult ConfigurationAsync(IConfiguration config, IWebHostEnvironment environment) => Results.Ok(new { environment = environment.EnvironmentName, orders = new { reservationMinutes = config.GetValue<int>("Orders:ReservationMinutes"), expiryScanSeconds = config.GetValue<int>("Orders:ExpiryScanSeconds") }, payments = new { provider = config["Payments:Provider"], callbackFreshnessSeconds = config.GetValue<int>("Payments:CallbackFreshnessSeconds"), ownershipTransferEnabled = config.GetValue<bool>("Payments:OwnershipTransferEnabled"), callbackSecret = "[redacted]", merchantAccount = "[redacted]" }, storage = new { endpointConfigured = !string.IsNullOrWhiteSpace(config["Storage:Endpoint"]), accessKey = "[redacted]", secretKey = "[redacted]" }, authentication = new { issuerConfigured = !string.IsNullOrWhiteSpace(config["Authentication:Issuer"]), clientSecret = "[redacted]" } });
    private static async Task<IResult> PublicContentPageAsync(string slug, MarketplaceDbContext db, CancellationToken ct){var item=await db.ManagedContentPages.AsNoTracking().Where(x=>x.Slug==slug&&x.IsPublished).Select(x=>new{x.Slug,x.Title,x.Body,x.UpdatedAtUtc}).SingleOrDefaultAsync(ct);return item is null?Results.NotFound():Results.Ok(item);}
    private static async Task<IResult> PublicBannerAsync(MarketplaceDbContext db, TimeProvider time, CancellationToken ct){var now=time.GetUtcNow();var item=await db.SiteBanners.AsNoTracking().Where(x=>x.IsEnabled&&x.StartsAtUtc<=now&&(x.EndsAtUtc==null||x.EndsAtUtc>now)).OrderByDescending(x=>x.UpdatedAtUtc).Select(x=>new{x.Message,x.EndsAtUtc}).FirstOrDefaultAsync(ct);return Results.Ok(item);}

    private static async Task SafeguardSellerListingsAsync(MarketplaceDbContext db, Guid sellerId, string reason, DateTimeOffset now, CancellationToken ct)
    {
        var plan = await db.MarketplaceListings.AsNoTracking().Where(x => x.SellerUserId == sellerId && (x.Status == MarketplaceListingStatus.Published || x.Status == MarketplaceListingStatus.Reserved || x.Status == MarketplaceListingStatus.Sold)).OrderBy(x => x.ArtworkId).Select(x => new { x.Id, x.ArtworkId }).ToListAsync(ct);
        foreach (var location in plan) { _ = await db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {location.ArtworkId} FOR UPDATE").SingleAsync(ct); var listing = await db.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"Id\" = {location.Id} FOR UPDATE").SingleAsync(ct); if (listing.Status == MarketplaceListingStatus.Published) listing.Suspend("Seller account was disabled.", now); else { var orderId = await db.Reservations.AsNoTracking().Where(x => x.ListingId == listing.Id).Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(ct); if (orderId is not null) { var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders.orders WHERE \"Id\" = {orderId.Value} FOR UPDATE").SingleAsync(ct); order.PlaceSellerEligibilityHold($"Seller account was disabled: {reason}", now); } } }
    }
    private static (int Page, int PageSize) Page(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100));
    private static bool UserId(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out id);
    private static void Audit(MarketplaceDbContext db, string type, Guid actor, Guid subject, DateTimeOffset now, object data) => db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(), type, actor, subject, JsonSerializer.Serialize(data), null, now));
    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });
    private static IResult Conflict(string message) => Results.Conflict(new { message });
    internal sealed record UserStatusRequest(bool Enabled, string Reason);
    internal sealed record RoleRequest(bool Assign, string Reason);
    internal sealed record CategoryRequest(string Name, string Slug, int DisplayOrder, bool IsActive);
    internal sealed record ContentPageRequest(string Slug, string Title, string Body, bool IsPublished, long ExpectedVersion);
    internal sealed record BannerRequest(string Message, DateTimeOffset StartsAtUtc, DateTimeOffset? EndsAtUtc, bool IsEnabled);
    internal sealed record ReasonRequest(string Reason);
}
