using ArtMarketplace.Domain.Modules.Payments;

namespace ArtMarketplace.Application.Payments;

public interface IPaymentProvider
{
    string ProviderKey { get; }
    Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken cancellationToken);
    ValueTask<PaymentCallbackVerification> VerifyCallbackAsync(string rawBody, string? signature, string? timestamp, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken);
}

public sealed record PaymentInitiationRequest(Guid PaymentAttemptId, Guid OrderId, decimal Amount, string Currency,
    string MerchantReference, string MerchantAccount, string ReturnUrl);
public sealed record PaymentInitiationResult(bool Succeeded, string? ProviderTransactionId, string? RedirectUrl, string? FailureCode)
{
    public static PaymentInitiationResult Success(string transactionId, string redirectUrl) => new(true, transactionId, redirectUrl, null);
    public static PaymentInitiationResult Failure(string code) => new(false, null, null, code);
}
public sealed record VerifiedProviderCallback(string EventId, string ProviderTransactionId, string MerchantReference,
    string MerchantAccount, decimal Amount, string Currency, PaymentOutcome Outcome, DateTimeOffset OccurredAtUtc, string PayloadDigest);
public sealed record PaymentCallbackVerification(bool IsValid, string? Error, VerifiedProviderCallback? Callback)
{
    public static PaymentCallbackVerification Invalid(string error) => new(false, error, null);
    public static PaymentCallbackVerification Valid(VerifiedProviderCallback callback) => new(true, null, callback);
}
