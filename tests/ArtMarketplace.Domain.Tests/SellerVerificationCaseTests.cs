using ArtMarketplace.Domain.Modules.Identity;

namespace ArtMarketplace.Domain.Tests;

public sealed class SellerVerificationCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CaseFollowsDraftPendingApprovedLifecycle()
    {
        var verification = new SellerVerificationCase(Guid.NewGuid(), Guid.NewGuid(), Now);
        var reviewer = Guid.NewGuid();

        verification.Submit(Now.AddMinutes(1));
        verification.Approve(reviewer, "Documents verified", Now.AddYears(1), Now.AddMinutes(2));

        Assert.Equal(VerificationStatus.Approved, verification.Status);
        Assert.Equal(reviewer, verification.DecidedByUserId);
        Assert.Equal(3, verification.Version);
    }

    [Fact]
    public void ApprovedCaseCanBeRevokedButCannotBeApprovedTwice()
    {
        var verification = new SellerVerificationCase(Guid.NewGuid(), Guid.NewGuid(), Now);
        var reviewer = Guid.NewGuid();
        verification.Submit(Now);
        verification.Approve(reviewer, "Documents verified", Now.AddYears(1), Now);

        Assert.Throws<InvalidOperationException>(() =>
            verification.Approve(reviewer, "Duplicate approval", Now.AddYears(1), Now));

        verification.Revoke(reviewer, "Document was revoked", Now.AddDays(1));
        Assert.Equal(VerificationStatus.Revoked, verification.Status);
    }
}
