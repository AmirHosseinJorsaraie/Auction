namespace ArtMarketplace.Domain.Modules.Payments;

public enum PaymentAttemptStatus { Prepared = 0, RedirectReady = 1, ProviderFailed = 2, Succeeded = 3, Failed = 4, Cancelled = 5 }
public enum PaymentOutcome { Succeeded = 0, Failed = 1, Cancelled = 2 }
public enum ReconciliationStatus { Open = 0, Resolved = 1 }

public sealed class PaymentAttempt
{
    private PaymentAttempt() { }
    public PaymentAttempt(Guid id, Guid orderId, Guid buyerUserId, string providerKey, string idempotencyKey,
        string merchantReference, string merchantAccount, decimal amount, string currency, DateTimeOffset now)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || buyerUserId == Guid.Empty) throw new ArgumentException("Payment identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Id = id; OrderId = orderId; BuyerUserId = buyerUserId; ProviderKey = Text(providerKey, 40);
        IdempotencyKey = Text(idempotencyKey, 100); MerchantReference = Text(merchantReference, 100);
        MerchantAccount = Text(merchantAccount, 120); Amount = amount; Currency = Text(currency, 3);
        Status = PaymentAttemptStatus.Prepared; CreatedAtUtc = now; UpdatedAtUtc = now; Version = 1;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid BuyerUserId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string MerchantReference { get; private set; } = string.Empty;
    public string MerchantAccount { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string? ProviderTransactionId { get; private set; }
    public string? RedirectUrl { get; private set; }
    public PaymentAttemptStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }
    public void MarkRedirectReady(string transactionId, string redirectUrl, DateTimeOffset now)
    {
        if (Status == PaymentAttemptStatus.RedirectReady && ProviderTransactionId == transactionId) return;
        if (Status != PaymentAttemptStatus.Prepared) throw new InvalidOperationException("Only a prepared attempt can be initiated.");
        ProviderTransactionId = Text(transactionId, 160); RedirectUrl = Text(redirectUrl, 2_000);
        Status = PaymentAttemptStatus.RedirectReady; Touch(now);
    }
    public void MarkProviderFailure(string code, DateTimeOffset now)
    {
        if (Status != PaymentAttemptStatus.Prepared) return;
        FailureCode = Text(code, 120); Status = PaymentAttemptStatus.ProviderFailed; Touch(now);
    }
    public void RecordOutcome(PaymentOutcome outcome, DateTimeOffset now)
    {
        if (Status == PaymentAttemptStatus.Succeeded && outcome != PaymentOutcome.Succeeded) return;
        Status = outcome switch { PaymentOutcome.Succeeded => PaymentAttemptStatus.Succeeded, PaymentOutcome.Failed => PaymentAttemptStatus.Failed, _ => PaymentAttemptStatus.Cancelled };
        Touch(now);
    }
    private void Touch(DateTimeOffset now) { UpdatedAtUtc = now; Version = checked(Version + 1); }
    private static string Text(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}

public sealed class PaymentProviderEvent
{
    private PaymentProviderEvent() { }
    public PaymentProviderEvent(Guid id, Guid attemptId, string providerKey, string eventId, string providerTransactionId,
        PaymentOutcome outcome, decimal amount, string currency, string merchantReference, string merchantAccount,
        DateTimeOffset providerOccurredAtUtc, DateTimeOffset receivedAtUtc, string payloadDigest)
    {
        if (id == Guid.Empty || attemptId == Guid.Empty) throw new ArgumentException("Event identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Id = id; PaymentAttemptId = attemptId; ProviderKey = Required(providerKey, 40); EventId = Required(eventId, 160);
        ProviderTransactionId = Required(providerTransactionId, 160); Outcome = outcome; Amount = amount;
        Currency = Required(currency, 3); MerchantReference = Required(merchantReference, 100);
        MerchantAccount = Required(merchantAccount, 120); ProviderOccurredAtUtc = providerOccurredAtUtc;
        ReceivedAtUtc = receivedAtUtc; PayloadDigest = Required(payloadDigest, 64);
    }
    public Guid Id { get; private set; }
    public Guid PaymentAttemptId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string ProviderTransactionId { get; private set; } = string.Empty;
    public PaymentOutcome Outcome { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string MerchantReference { get; private set; } = string.Empty;
    public string MerchantAccount { get; private set; } = string.Empty;
    public DateTimeOffset ProviderOccurredAtUtc { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public string PayloadDigest { get; private set; } = string.Empty;
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}

public sealed class VerifiedPaymentReceipt
{
    private VerifiedPaymentReceipt() { }
    public VerifiedPaymentReceipt(Guid id, Guid attemptId, Guid sourceEventId, string providerKey, string providerTransactionId,
        decimal amount, string currency, DateTimeOffset verifiedAtUtc)
    {
        if (id == Guid.Empty || attemptId == Guid.Empty || sourceEventId == Guid.Empty) throw new ArgumentException("Receipt identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Id = id; PaymentAttemptId = attemptId; SourceProviderEventId = sourceEventId; ProviderKey = providerKey;
        ProviderTransactionId = providerTransactionId; Amount = amount; Currency = currency; VerifiedAtUtc = verifiedAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid PaymentAttemptId { get; private set; }
    public Guid SourceProviderEventId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string ProviderTransactionId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset VerifiedAtUtc { get; private set; }
}

public sealed class OrderPaymentApplication
{
    private OrderPaymentApplication() { }
    public OrderPaymentApplication(Guid id, Guid orderId, Guid receiptId, DateTimeOffset appliedAtUtc)
    { if (id == Guid.Empty || orderId == Guid.Empty || receiptId == Guid.Empty) throw new ArgumentException("Application identifiers are required."); Id = id; OrderId = orderId; VerifiedPaymentReceiptId = receiptId; AppliedAtUtc = appliedAtUtc; }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid VerifiedPaymentReceiptId { get; private set; }
    public DateTimeOffset AppliedAtUtc { get; private set; }
}

public sealed class PaymentReconciliationCase
{
    private PaymentReconciliationCase() { }
    public PaymentReconciliationCase(Guid id, Guid orderId, Guid receiptId, string reasonCode, string details, DateTimeOffset now)
    { if (id == Guid.Empty || orderId == Guid.Empty || receiptId == Guid.Empty) throw new ArgumentException("Reconciliation identifiers are required."); Id = id; OrderId = orderId; VerifiedPaymentReceiptId = receiptId; ReasonCode = Required(reasonCode, 80); Details = Required(details, 1_000); Status = ReconciliationStatus.Open; CreatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid VerifiedPaymentReceiptId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public ReconciliationStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public void Resolve(DateTimeOffset now) { if (Status == ReconciliationStatus.Resolved) return; Status = ReconciliationStatus.Resolved; ResolvedAtUtc = now; }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
}
