using ArtMarketplace.Domain.Modules.Identity;

namespace ArtMarketplace.Domain.Tests;

public sealed class SellerEligibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApprovalEnablesSellingAndAdvancesVersion()
    {
        var eligibility = new SellerEligibility(Guid.NewGuid());

        eligibility.Approve(Now.AddYears(1), Now);

        Assert.True(eligibility.CanSellAt(Now));
        Assert.Equal(2, eligibility.EligibilityVersion);
    }

    [Fact]
    public void RevocationCreatesVersionedInvalidationThatCoversOldSnapshots()
    {
        var eligibility = new SellerEligibility(Guid.NewGuid());
        eligibility.Approve(Now.AddYears(1), Now);
        var approvedVersion = eligibility.EligibilityVersion;

        var invalidation = eligibility.Revoke("Compliance review failed", Now.AddDays(1));

        Assert.False(eligibility.CanSellAt(Now.AddDays(1)));
        Assert.True(invalidation.Covers(approvedVersion));
        Assert.False(invalidation.Covers(invalidation.InvalidatedVersion));
        Assert.Equal(eligibility.EligibilityVersion, invalidation.InvalidatedVersion);
    }

    [Fact]
    public void ResolutionIsAppendOnlyPerTarget()
    {
        var eligibility = new SellerEligibility(Guid.NewGuid());
        eligibility.Approve(Now.AddYears(1), Now);
        var invalidation = eligibility.Revoke("Identity document revoked", Now.AddDays(1));
        var targetId = Guid.NewGuid();

        invalidation.ResolveTarget(
            "Listing",
            targetId,
            Guid.NewGuid(),
            "Listing removed",
            "Moderation case 42",
            Now.AddDays(1));

        Assert.Throws<InvalidOperationException>(() => invalidation.ResolveTarget(
            "Listing",
            targetId,
            Guid.NewGuid(),
            "Second removal",
            "Duplicate action",
            Now.AddDays(2)));
    }
}
