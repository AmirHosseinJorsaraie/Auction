namespace ArtMarketplace.Domain.Modules.Identity;

public sealed class SellerVerificationCase
{
    private SellerVerificationCase()
    {
    }

    public SellerVerificationCase(Guid id, Guid applicantUserId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || applicantUserId == Guid.Empty)
        {
            throw new ArgumentException("Case and applicant identifiers are required.");
        }

        Id = id;
        ApplicantUserId = applicantUserId;
        CreatedAtUtc = createdAtUtc;
        Status = VerificationStatus.Draft;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid ApplicantUserId { get; private set; }

    public VerificationStatus Status { get; private set; }

    public int Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public string? DecisionReason { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public void Submit(DateTimeOffset now)
    {
        EnsureStatus(VerificationStatus.Draft, VerificationStatus.ChangesRequested);
        Status = VerificationStatus.Pending;
        SubmittedAtUtc = now;
        AdvanceVersion();
    }

    public void RequestChanges(Guid actorUserId, string reason, DateTimeOffset now)
    {
        Decide(actorUserId, reason, now, VerificationStatus.ChangesRequested, null);
    }

    public void Approve(Guid actorUserId, string reason, DateTimeOffset expiresAtUtc, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAtUtc, now);

        Decide(actorUserId, reason, now, VerificationStatus.Approved, expiresAtUtc);
    }

    public void Reject(Guid actorUserId, string reason, DateTimeOffset now) =>
        Decide(actorUserId, reason, now, VerificationStatus.Rejected, null);

    public void Revoke(Guid actorUserId, string reason, DateTimeOffset now)
    {
        EnsureStatus(VerificationStatus.Approved);
        ApplyDecision(actorUserId, reason, now, VerificationStatus.Revoked, null);
    }

    public void Expire(DateTimeOffset now)
    {
        EnsureStatus(VerificationStatus.Approved);
        if (ExpiresAtUtc is null || ExpiresAtUtc > now)
        {
            throw new InvalidOperationException("The verification has not expired.");
        }

        Status = VerificationStatus.Expired;
        DecidedAtUtc = now;
        AdvanceVersion();
    }

    private void Decide(Guid actorUserId, string reason, DateTimeOffset now, VerificationStatus status, DateTimeOffset? expiresAtUtc)
    {
        EnsureStatus(VerificationStatus.Pending);
        ApplyDecision(actorUserId, reason, now, status, expiresAtUtc);
    }

    private void ApplyDecision(Guid actorUserId, string reason, DateTimeOffset now, VerificationStatus status, DateTimeOffset? expiresAtUtc)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("An actor identifier is required.", nameof(actorUserId));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length is < 3 or > 1_000)
        {
            throw new ArgumentException("Decision reason must contain 3 to 1000 characters.", nameof(reason));
        }

        Status = status;
        DecisionReason = normalizedReason;
        DecidedByUserId = actorUserId;
        DecidedAtUtc = now;
        ExpiresAtUtc = expiresAtUtc;
        AdvanceVersion();
    }

    private void EnsureStatus(params VerificationStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException($"Transition from {Status} is not allowed.");
        }
    }

    private void AdvanceVersion() => Version = checked(Version + 1);
}
