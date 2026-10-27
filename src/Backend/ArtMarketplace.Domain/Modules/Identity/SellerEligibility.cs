namespace ArtMarketplace.Domain.Modules.Identity;

public sealed class SellerEligibility
{
    private SellerEligibility()
    {
    }

    public SellerEligibility(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user identifier is required.", nameof(userId));
        }

        UserId = userId;
        AccountEnabled = true;
        EligibilityVersion = 1;
    }

    public Guid UserId { get; private set; }

    public bool AccountEnabled { get; private set; }

    public bool VerificationApproved { get; private set; }

    public DateTimeOffset? VerificationExpiresAtUtc { get; private set; }

    public long EligibilityVersion { get; private set; }

    public bool CanSellAt(DateTimeOffset now) =>
        AccountEnabled && VerificationApproved && VerificationExpiresAtUtc > now;

    public void Approve(DateTimeOffset expiresAtUtc, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAtUtc, now);

        VerificationApproved = true;
        VerificationExpiresAtUtc = expiresAtUtc;
        AdvanceVersion();
    }

    public SellerEligibilityInvalidation Revoke(string reason, DateTimeOffset now) =>
        Invalidate(reason, now, disableAccount: false);

    public SellerEligibilityInvalidation DisableAccount(string reason, DateTimeOffset now) =>
        Invalidate(reason, now, disableAccount: true);

    public void ReactivateAccount()
    {
        if (AccountEnabled) return;
        AccountEnabled = true;
        AdvanceVersion();
    }

    public SellerEligibilityInvalidation Expire(string reason, DateTimeOffset now)
    {
        if (VerificationExpiresAtUtc is null || VerificationExpiresAtUtc > now)
        {
            throw new InvalidOperationException("Seller eligibility has not expired.");
        }

        return Invalidate(reason, now, disableAccount: false);
    }

    private SellerEligibilityInvalidation Invalidate(string reason, DateTimeOffset now, bool disableAccount)
    {
        var normalizedReason = reason.Trim();
        if (normalizedReason.Length is < 3 or > 1_000)
        {
            throw new ArgumentException("Invalidation reason must contain 3 to 1000 characters.", nameof(reason));
        }

        if (disableAccount)
        {
            AccountEnabled = false;
        }

        VerificationApproved = false;
        AdvanceVersion();

        return new SellerEligibilityInvalidation(
            Guid.NewGuid(),
            UserId,
            EligibilityVersion,
            normalizedReason,
            now);
    }

    private void AdvanceVersion() => EligibilityVersion = checked(EligibilityVersion + 1);
}
