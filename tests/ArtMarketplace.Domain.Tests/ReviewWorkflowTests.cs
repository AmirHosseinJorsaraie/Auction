using ArtMarketplace.Domain.Modules.Reviews;

namespace ArtMarketplace.Domain.Tests;

public sealed class ReviewWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RoundPinsRevisionAndAcceptsExactlyOneFinalDecision()
    {
        var revisionId = Guid.NewGuid();
        var round = new ReviewRound(Guid.NewGuid(), Guid.NewGuid(), revisionId, 1, Now);

        round.Decide(ReviewDecisionKind.Approve, 1, Now.AddMinutes(2));

        Assert.Equal(revisionId, round.ArtworkRevisionId);
        Assert.Equal(ReviewRoundStatus.Approved, round.Status);
        Assert.Equal(2, round.Version);
        Assert.Throws<InvalidOperationException>(() => round.Decide(ReviewDecisionKind.Reject, 1, Now.AddMinutes(3)));
    }

    [Fact]
    public void StaleExpectedVersionIsRejected()
    {
        var round = new ReviewRound(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        Assert.Throws<InvalidOperationException>(() => round.Decide(ReviewDecisionKind.RequestChanges, 0, Now));
        Assert.Equal(ReviewRoundStatus.PendingReview, round.Status);
    }

    [Fact]
    public void DecisionRequiresPublicReasonAndComment()
    {
        Assert.Throws<ArgumentException>(() => new ReviewDecision(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReviewDecisionKind.Reject, " ", "Visible", null, Now));
        Assert.Throws<ArgumentException>(() => new ReviewDecision(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReviewDecisionKind.Reject, "Reason", " ", null, Now));
    }

    [Fact]
    public void ClosedCaseCannotBeClosedAgain()
    {
        var item = new ReviewCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        item.Close(Now.AddMinutes(1));
        Assert.Equal(ReviewCaseStatus.Closed, item.Status);
        Assert.Throws<InvalidOperationException>(() => item.Close(Now.AddMinutes(2)));
    }
}
