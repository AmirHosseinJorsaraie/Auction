using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtMarketplace.Infrastructure.Identity;

public sealed class SellerEligibilityGate(
    MarketplaceDbContext database,
    TimeProvider timeProvider) : ISellerEligibilityGate
{
    public async Task<SellerEligibilityDecision> EvaluateAsync(
        Guid userId,
        long? capturedEligibilityVersion,
        CancellationToken cancellationToken = default)
    {
        var eligibility = await database.SellerEligibilities.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (eligibility is null)
        {
            return new SellerEligibilityDecision(false, 0, false, "Seller eligibility is not established.");
        }

        var invalidated = capturedEligibilityVersion is not null
            && await database.SellerEligibilityInvalidations.AsNoTracking().AnyAsync(
                item => item.UserId == userId
                    && capturedEligibilityVersion.Value < item.InvalidatedVersion,
                cancellationToken);
        if (invalidated)
        {
            return new SellerEligibilityDecision(
                false,
                eligibility.EligibilityVersion,
                true,
                "The captured seller eligibility version was invalidated.");
        }

        return eligibility.CanSellAt(timeProvider.GetUtcNow())
            ? new SellerEligibilityDecision(true, eligibility.EligibilityVersion, false, "Eligible.")
            : new SellerEligibilityDecision(
                false,
                eligibility.EligibilityVersion,
                false,
                "The account or verification is not currently eligible to sell.");
    }
}
