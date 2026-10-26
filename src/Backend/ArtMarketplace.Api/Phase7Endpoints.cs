using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Application.Payments;
using ArtMarketplace.Domain.Modules.Payments;
using ArtMarketplace.Infrastructure.Payments;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Api;

internal static class Phase7Endpoints
{
    public static void MapPhase7Endpoints(this WebApplication app)
    {
        app.MapPost("/api/orders/{orderId:guid}/payment-attempts", InitiateAsync).RequireAuthorization().RequireRateLimiting("payment").WithTags("Payments");
        app.MapGet("/api/payments/attempts/{attemptId:guid}", GetAttemptAsync).RequireAuthorization().RequireRateLimiting("baseline").WithTags("Payments");
        app.MapPost("/api/payments/callbacks/{providerKey}", CallbackAsync).AllowAnonymous().RequireRateLimiting("payment-callback").WithTags("Payments");
        app.MapGet("/api/payments/return", () => Results.Ok(new { message = "Return acknowledged. Payment state is updated only by a verified server callback." })).AllowAnonymous().RequireRateLimiting("baseline").WithTags("Payments");
        app.MapGet("/api/backoffice/payments/reconciliation", ReconciliationAsync).RequireAuthorization(MarketplacePolicies.SuperAdminWithMfa).RequireRateLimiting("baseline").WithTags("Payments");
        app.MapPost("/api/backoffice/orders/{orderId:guid}/ownership-transfer", TransferOwnershipAsync).RequireAuthorization(MarketplacePolicies.SuperAdminWithMfa).RequireRateLimiting("payment").WithTags("Payments");
        if (app.Environment.IsDevelopment())
            app.MapPost("/api/payments/fake/{attemptId:guid}/complete", CompleteFakeAsync).RequireAuthorization().RequireRateLimiting("payment").WithTags("Payments");
    }

    private static async Task<IResult> InitiateAsync(Guid orderId, InitiatePaymentRequest request, ClaimsPrincipal principal,
        MarketplaceDbContext db, IPaymentProvider provider, IOptions<PaymentProviderOptions> options, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100) return Validation("A valid idempotency key is required.");
        var replay = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId && x.BuyerUserId == buyerId && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return Results.Ok(AttemptResponse(replay, true));
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId && x.BuyerUserId == buyerId, ct);
        if (order is null) return Results.NotFound();
        var now = time.GetUtcNow();
        if (order.Status != Domain.Modules.Orders.OrderStatus.AwaitingPayment || order.ReservationExpiresAtUtc <= now || order.ReviewIntegrityHold || order.SellerEligibilityHold) return Conflict("The order is not currently payable.");
        var paymentOptions = options.Value;
        var attempt = new PaymentAttempt(Guid.NewGuid(), order.Id, buyerId, provider.ProviderKey, request.IdempotencyKey,
            $"order-{order.Id:N}-attempt-{Guid.NewGuid():N}", paymentOptions.MerchantAccount, order.TotalAmount, order.Currency, now);
        db.PaymentAttempts.Add(attempt);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var winner = await db.PaymentAttempts.AsNoTracking().SingleAsync(x => x.OrderId == orderId && x.BuyerUserId == buyerId && x.IdempotencyKey == request.IdempotencyKey, ct);
            return Results.Ok(AttemptResponse(winner, true));
        }

        PaymentInitiationResult result;
        try
        {
            result = await provider.InitiateAsync(new(attempt.Id, order.Id, order.TotalAmount, order.Currency,
                attempt.MerchantReference, attempt.MerchantAccount, $"{paymentOptions.PublicBaseUrl.TrimEnd('/')}/orders/{order.Id:D}"), ct);
        }
        catch (Exception) { result = PaymentInitiationResult.Failure("provider-unavailable"); }

        var persisted = await db.PaymentAttempts.SingleAsync(x => x.Id == attempt.Id, ct);
        if (result.Succeeded) persisted.MarkRedirectReady(result.ProviderTransactionId!, result.RedirectUrl!, time.GetUtcNow());
        else persisted.MarkProviderFailure(result.FailureCode ?? "provider-failed", time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return result.Succeeded ? Results.Created($"/api/payments/attempts/{persisted.Id:D}", AttemptResponse(persisted, false)) : Results.Problem(statusCode: 502, title: "Payment provider unavailable", detail: "The attempt is saved and can be safely retried with the same idempotency key.");
    }

    private static async Task<IResult> GetAttemptAsync(Guid attemptId, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        var attempt = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.BuyerUserId == userId, ct);
        return attempt is null ? Results.NotFound() : Results.Ok(AttemptResponse(attempt, false));
    }

    private static async Task<IResult> CallbackAsync(string providerKey, HttpRequest request, MarketplaceDbContext db,
        IPaymentProvider provider, TimeProvider time, CancellationToken ct)
    {
        if (!string.Equals(providerKey, provider.ProviderKey, StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, false, 16_384, leaveOpen: false);
        var body = await reader.ReadToEndAsync(ct);
        if (body.Length is 0 or > 64_000) return Results.BadRequest(new { message = "Invalid callback body." });
        var verification = await provider.VerifyCallbackAsync(body, request.Headers["X-Payment-Signature"].FirstOrDefault(), request.Headers["X-Payment-Timestamp"].FirstOrDefault(), time.GetUtcNow(), ct);
        if (!verification.IsValid) return Results.BadRequest(new { message = verification.Error });
        var result = await Phase7PaymentOperations.ProcessVerifiedCallbackAsync(db, provider.ProviderKey, verification.Callback!, time.GetUtcNow(), ct);
        return result.Accepted ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> CompleteFakeAsync(Guid attemptId, FakeCompletionRequest request, ClaimsPrincipal principal,
        MarketplaceDbContext db, IPaymentProvider provider, IOptions<PaymentProviderOptions> options, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var buyerId)) return Results.Unauthorized();
        var attempt = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.BuyerUserId == buyerId, ct);
        if (attempt is null) return Results.NotFound();
        var now = time.GetUtcNow(); var timestamp = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var payload = new FakePaymentProvider.FakeCallbackPayload(request.EventId ?? $"evt_{Guid.NewGuid():N}", attempt.ProviderTransactionId ?? $"fake_{attempt.Id:N}",
            attempt.MerchantReference, request.MerchantAccount ?? attempt.MerchantAccount, request.Amount ?? attempt.Amount,
            request.Currency ?? attempt.Currency, request.Outcome, now);
        var body = JsonSerializer.Serialize(payload); var signature = FakePaymentProvider.Sign(timestamp, body, options.Value.CallbackSecret);
        var verified = await provider.VerifyCallbackAsync(body, signature, timestamp, now, ct);
        if (!verified.IsValid) return Results.BadRequest(new { message = verified.Error });
        var result = await Phase7PaymentOperations.ProcessVerifiedCallbackAsync(db, provider.ProviderKey, verified.Callback!, now, ct);
        return result.Accepted ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> ReconciliationAsync(MarketplaceDbContext db, CancellationToken ct) => Results.Ok(await db.PaymentReconciliationCases.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(200).Select(x => new { x.Id, x.OrderId, x.VerifiedPaymentReceiptId, x.ReasonCode, x.Details, x.Status, x.CreatedAtUtc, x.ResolvedAtUtc }).ToListAsync(ct));
    private static async Task<IResult> TransferOwnershipAsync(Guid orderId, ClaimsPrincipal principal, MarketplaceDbContext db, IOptions<PaymentProviderOptions> options, TimeProvider time, CancellationToken ct)
    {
        if (!options.Value.OwnershipTransferEnabled) return Results.Conflict(new { message = "Ownership transfer is disabled until the legal trigger is approved and configured." });
        if (!UserId(principal, out var actorId)) return Results.Unauthorized();
        var result = await Phase7OwnershipOperations.TransferAsync(db, orderId, actorId, time.GetUtcNow(), ct);
        return result.Success ? Results.Ok(result) : result.NotFound ? Results.NotFound() : Results.Conflict(new { message = result.Error });
    }
    private static object AttemptResponse(PaymentAttempt x, bool replay) => new { x.Id, x.OrderId, x.ProviderKey, x.Amount, x.Currency, x.Status, x.RedirectUrl, x.FailureCode, x.CreatedAtUtc, x.UpdatedAtUtc, idempotentReplay = replay };
    private static bool UserId(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out id);
    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });
    private static IResult Conflict(string message) => Results.Conflict(new { message });
    internal sealed record InitiatePaymentRequest(string IdempotencyKey);
    internal sealed record FakeCompletionRequest(string Outcome, string? EventId, decimal? Amount, string? Currency, string? MerchantAccount);
}
