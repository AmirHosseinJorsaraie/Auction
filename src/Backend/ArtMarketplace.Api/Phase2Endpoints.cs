using System.Security.Claims;
using System.Text.Json;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Profiles;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Api;

internal static class Phase2Endpoints
{
    public static IEndpointRouteBuilder MapPhase2Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var account = endpoints.MapGroup("/api/account")
            .RequireAuthorization()
            .RequireRateLimiting("baseline")
            .WithTags("Account");

        account.MapGet("/profile", GetProfileAsync);
        account.MapPut("/profile", PutProfileAsync);
        account.MapGet("/addresses", GetAddressesAsync);
        account.MapPost("/addresses", AddAddressAsync);
        account.MapDelete("/addresses/{addressId:guid}", DeleteAddressAsync);

        endpoints.MapPost("/api/seller-verification/submit", SubmitVerificationAsync)
            .RequireAuthorization()
            .RequireRateLimiting("baseline")
            .WithTags("Seller verification");

        var verification = endpoints.MapGroup("/api/backoffice/seller-verifications")
            .RequireAuthorization(MarketplacePolicies.VerifySellers)
            .RequireRateLimiting("baseline")
            .WithTags("Backoffice seller verification");

        verification.MapGet("/pending", GetPendingVerificationsAsync);
        verification.MapPost("/{caseId:guid}/decision", DecideVerificationAsync);
        verification.MapPost("/invalidations/{invalidationId:guid}/resolutions", ResolveInvalidationAsync);
        return endpoints;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var profile = await database.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return profile is null
            ? Results.NotFound()
            : Results.Ok(new ProfileResponse(
                profile.DisplayName,
                profile.Locale,
                profile.PublicSellerBio));
    }

    private static async Task<IResult> PutProfileAsync(
        ProfileRequest request,
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var profile = await database.UserProfiles.SingleOrDefaultAsync(
                item => item.UserId == userId,
                cancellationToken);
            if (profile is null)
            {
                profile = new UserProfile(userId, request.DisplayName, request.Locale);
                profile.Update(request.DisplayName, request.Locale, request.PublicSellerBio);
                database.UserProfiles.Add(profile);
            }
            else
            {
                profile.Update(request.DisplayName, request.Locale, request.PublicSellerBio);
            }

            await database.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (ArgumentException exception)
        {
            return Validation(exception);
        }
    }

    private static async Task<IResult> GetAddressesAsync(
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var addresses = await database.SavedAddresses.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new AddressResponse(
                item.Id,
                item.Label,
                item.RecipientName,
                item.AddressLine,
                item.PostalCode))
            .ToListAsync(cancellationToken);
        return Results.Ok(addresses);
    }

    private static async Task<IResult> AddAddressAsync(
        AddressRequest request,
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var address = new SavedAddress(
                Guid.NewGuid(),
                userId,
                request.Label,
                request.RecipientName,
                request.AddressLine,
                request.PostalCode);
            database.SavedAddresses.Add(address);
            await database.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/account/addresses/{address.Id}", new { address.Id });
        }
        catch (ArgumentException exception)
        {
            return Validation(exception);
        }
    }

    private static async Task<IResult> DeleteAddressAsync(
        Guid addressId,
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var address = await database.SavedAddresses.SingleOrDefaultAsync(
            item => item.Id == addressId && item.UserId == userId,
            cancellationToken);
        if (address is null)
        {
            return Results.NotFound();
        }

        database.SavedAddresses.Remove(address);
        await database.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SubmitVerificationAsync(
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var verification = await database.SellerVerificationCases
            .Where(item => item.ApplicantUserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (verification is null || verification.Status is VerificationStatus.Rejected
            or VerificationStatus.Revoked or VerificationStatus.Expired)
        {
            verification = new SellerVerificationCase(Guid.NewGuid(), userId, now);
            database.SellerVerificationCases.Add(verification);
        }

        try
        {
            verification.Submit(now);
        }
        catch (InvalidOperationException)
        {
            return Results.Conflict(new { message = "A verification case is already pending or approved." });
        }

        AddAudit(database, "seller_verification.submitted", userId, userId, verification.Id, httpContext, now);
        await database.SaveChangesAsync(cancellationToken);
        return Results.Accepted($"/api/seller-verification/{verification.Id}", new
        {
            verification.Id,
            verification.Status,
            verification.Version
        });
    }

    private static async Task<IResult> GetPendingVerificationsAsync(
        MarketplaceDbContext database,
        CancellationToken cancellationToken)
    {
        var cases = await database.SellerVerificationCases.AsNoTracking()
            .Where(item => item.Status == VerificationStatus.Pending)
            .OrderBy(item => item.SubmittedAtUtc)
            .Select(item => new
            {
                item.Id,
                item.ApplicantUserId,
                item.SubmittedAtUtc,
                item.Version
            })
            .ToListAsync(cancellationToken);
        return Results.Ok(cases);
    }

    private static async Task<IResult> DecideVerificationAsync(
        Guid caseId,
        VerificationDecisionRequest request,
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var verificationLocation = await database.SellerVerificationCases.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == caseId,
            cancellationToken);
        if (verificationLocation is null)
        {
            return Results.NotFound();
        }

        await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        _ = await database.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {verificationLocation.ApplicantUserId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var lockedEligibility = await database.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {verificationLocation.ApplicantUserId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var verification = await database.SellerVerificationCases.FromSqlInterpolated($"SELECT * FROM profiles.seller_verification_cases WHERE \"Id\" = {caseId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (verification is null) return Results.NotFound();

        if (verification.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "The verification case changed; reload before deciding." });
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            switch (request.Decision.Trim().ToLowerInvariant())
            {
                case "approve":
                    if (request.ExpiresAtUtc is null)
                    {
                        return Validation("expiresAtUtc is required for approval.");
                    }

                    var expiry = request.ExpiresAtUtc.Value.ToUniversalTime();
                    verification.Approve(actorUserId, request.Reason, expiry, now);
                    var eligibility = lockedEligibility;
                    if (eligibility is null)
                    {
                        eligibility = new SellerEligibility(verification.ApplicantUserId);
                        database.SellerEligibilities.Add(eligibility);
                    }

                    eligibility.Approve(expiry, now);
                    AddOutbox(database, "SellerEligibilityApproved", eligibility.UserId, new
                    {
                        eligibility.UserId,
                        eligibility.EligibilityVersion,
                        ExpiresAtUtc = expiry
                    }, now);
                    break;
                case "request-changes":
                    verification.RequestChanges(actorUserId, request.Reason, now);
                    break;
                case "reject":
                    verification.Reject(actorUserId, request.Reason, now);
                    break;
                case "revoke":
                    verification.Revoke(actorUserId, request.Reason, now);
                    var revokedEligibility = lockedEligibility ?? throw new InvalidOperationException("Seller eligibility does not exist.");
                    var invalidation = revokedEligibility.Revoke(request.Reason, now);
                    database.SellerEligibilityInvalidations.Add(invalidation);
                    AddOutbox(database, "SellerEligibilityInvalidated", revokedEligibility.UserId, new
                    {
                        invalidation.Id,
                        invalidation.UserId,
                        invalidation.InvalidatedVersion,
                        invalidation.Reason
                    }, now);
                    var affectedListings = await database.MarketplaceListings.AsNoTracking().Where(item => item.SellerUserId == revokedEligibility.UserId && (item.Status == MarketplaceListingStatus.Published || item.Status == MarketplaceListingStatus.Reserved || item.Status == MarketplaceListingStatus.Sold)).OrderBy(item => item.ArtworkId).Select(item => new { item.Id, item.ArtworkId, item.Status }).ToListAsync(cancellationToken);
                    foreach (var location in affectedListings)
                    {
                        _ = await database.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {location.ArtworkId} FOR UPDATE").SingleAsync(cancellationToken);
                        var listing = await database.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"Id\" = {location.Id} FOR UPDATE").SingleAsync(cancellationToken);
                        if (listing.Status == MarketplaceListingStatus.Published) listing.Suspend("Seller eligibility was revoked.", now);
                        else if (listing.Status is MarketplaceListingStatus.Reserved or MarketplaceListingStatus.Sold)
                        {
                            var orderId = await database.Reservations.AsNoTracking().Where(x => x.ListingId == listing.Id).Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(cancellationToken);
                            if (orderId is not null)
                            {
                                var order = await database.Orders.FromSqlInterpolated($"SELECT * FROM orders.orders WHERE \"Id\" = {orderId.Value} FOR UPDATE").SingleAsync(cancellationToken);
                                order.PlaceSellerEligibilityHold(listing.Status == MarketplaceListingStatus.Sold ? "Seller eligibility was revoked after payment; fulfillment and ownership transfer are on hold." : "Seller eligibility was revoked while the order was reserved.", now);
                            }
                        }
                    }
                    break;
                default:
                    return Validation("decision must be approve, request-changes, reject, or revoke.");
            }
        }
        catch (ArgumentException exception)
        {
            return Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }

        AddAudit(
            database,
            $"seller_verification.{request.Decision.Trim().ToLowerInvariant()}",
            actorUserId,
            verification.ApplicantUserId,
            verification.Id,
            httpContext,
            now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { verification.Id, verification.Status, verification.Version });
    }

    private static async Task<IResult> ResolveInvalidationAsync(
        Guid invalidationId,
        InvalidationResolutionRequest request,
        ClaimsPrincipal principal,
        MarketplaceDbContext database,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var invalidation = await database.SellerEligibilityInvalidations
            .Include(item => item.Resolutions)
            .SingleOrDefaultAsync(item => item.Id == invalidationId, cancellationToken);
        if (invalidation is null)
        {
            return Results.NotFound();
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            var resolution = invalidation.ResolveTarget(
                request.TargetType,
                request.TargetId,
                actorUserId,
                request.Reason,
                request.Evidence,
                now);
            AddAudit(
                database,
                "seller_eligibility.invalidation_resolved",
                actorUserId,
                invalidation.UserId,
                resolution.Id,
                httpContext,
                now);
            await database.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { resolution.Id });
        }
        catch (ArgumentException exception)
        {
            return Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out userId);

    private static void AddAudit(
        MarketplaceDbContext database,
        string eventType,
        Guid actorUserId,
        Guid subjectUserId,
        Guid aggregateId,
        HttpContext httpContext,
        DateTimeOffset now) =>
        database.SecurityAuditEvents.Add(new SecurityAuditEvent(
            Guid.NewGuid(),
            eventType,
            actorUserId,
            subjectUserId,
            JsonSerializer.Serialize(new { aggregateId }),
            httpContext.TraceIdentifier,
            now));

    private static void AddOutbox(
        MarketplaceDbContext database,
        string type,
        Guid aggregateId,
        object payload,
        DateTimeOffset now) =>
        database.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            AggregateId = aggregateId,
            PayloadJson = JsonSerializer.Serialize(payload),
            OccurredAtUtc = now
        });

    private static IResult Validation(ArgumentException exception) => Validation(exception.Message);

    private static IResult Validation(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    internal sealed record ProfileRequest(string DisplayName, string Locale, string? PublicSellerBio);

    internal sealed record ProfileResponse(string DisplayName, string Locale, string? PublicSellerBio);

    internal sealed record AddressRequest(
        string Label,
        string RecipientName,
        string AddressLine,
        string PostalCode);

    internal sealed record AddressResponse(
        Guid Id,
        string Label,
        string RecipientName,
        string AddressLine,
        string PostalCode);

    internal sealed record VerificationDecisionRequest(
        string Decision,
        string Reason,
        int ExpectedVersion,
        DateTimeOffset? ExpiresAtUtc);

    internal sealed record InvalidationResolutionRequest(
        string TargetType,
        Guid TargetId,
        string Reason,
        string Evidence);
}
