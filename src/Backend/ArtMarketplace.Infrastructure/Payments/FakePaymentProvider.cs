using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArtMarketplace.Application.Payments;
using ArtMarketplace.Domain.Modules.Payments;
using Microsoft.Extensions.Options;

namespace ArtMarketplace.Infrastructure.Payments;

public sealed class FakePaymentProvider(IOptions<PaymentProviderOptions> options) : IPaymentProvider
{
    private readonly PaymentProviderOptions _options = options.Value;
    public string ProviderKey => "Fake";

    public Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transactionId = $"fake_{request.PaymentAttemptId:N}";
        var url = $"{_options.PublicBaseUrl.TrimEnd('/')}/payments/fake?attemptId={request.PaymentAttemptId:D}";
        return Task.FromResult(PaymentInitiationResult.Success(transactionId, url));
    }

    public ValueTask<PaymentCallbackVerification> VerifyCallbackAsync(string rawBody, string? signature, string? timestamp,
        DateTimeOffset receivedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unix)) return ValueTask.FromResult(PaymentCallbackVerification.Invalid("Missing or invalid callback timestamp."));
        var sentAt = DateTimeOffset.FromUnixTimeSeconds(unix);
        if ((receivedAtUtc - sentAt).Duration() > TimeSpan.FromSeconds(_options.CallbackFreshnessSeconds)) return ValueTask.FromResult(PaymentCallbackVerification.Invalid("Stale callback."));
        var expected = Sign(timestamp!, rawBody, _options.CallbackSecret);
        if (string.IsNullOrWhiteSpace(signature) || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature))) return ValueTask.FromResult(PaymentCallbackVerification.Invalid("Invalid callback signature."));
        try
        {
            var payload = JsonSerializer.Deserialize<FakeCallbackPayload>(rawBody, JsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.EventId) || string.IsNullOrWhiteSpace(payload.ProviderTransactionId)) return ValueTask.FromResult(PaymentCallbackVerification.Invalid("Invalid callback payload."));
            if (!Enum.TryParse<PaymentOutcome>(payload.Outcome, true, out var outcome)) return ValueTask.FromResult(PaymentCallbackVerification.Invalid("Invalid payment outcome."));
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
            return ValueTask.FromResult(PaymentCallbackVerification.Valid(new(payload.EventId, payload.ProviderTransactionId,
                payload.MerchantReference, payload.MerchantAccount, payload.Amount, payload.Currency, outcome, payload.OccurredAtUtc, digest)));
        }
        catch (JsonException) { return ValueTask.FromResult(PaymentCallbackVerification.Invalid("Invalid callback JSON.")); }
    }

    public static string Sign(string timestamp, string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public sealed record FakeCallbackPayload(string EventId, string ProviderTransactionId, string MerchantReference,
        string MerchantAccount, decimal Amount, string Currency, string Outcome, DateTimeOffset OccurredAtUtc);
}
