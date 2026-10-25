using System.Text.Json;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtMarketplace.Identity.Services;

internal sealed partial class SellerVerificationExpirationService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SellerVerificationExpirationService> logger) : BackgroundService
{
    private static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ExpireDueCasesAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ExpireDueCasesAsync(stoppingToken);
        }
    }

    private async Task ExpireDueCasesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var now = timeProvider.GetUtcNow();
        var dueCases = await database.SellerVerificationCases
            .Where(item => item.Status == VerificationStatus.Approved
                && item.ExpiresAtUtc != null
                && item.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var verification in dueCases)
        {
            verification.Expire(now);
            var eligibility = await database.SellerEligibilities.SingleOrDefaultAsync(
                item => item.UserId == verification.ApplicantUserId,
                cancellationToken);
            if (eligibility is null)
            {
                continue;
            }

            var invalidation = eligibility.Expire("Verification validity period ended.", now);
            database.SellerEligibilityInvalidations.Add(invalidation);
            database.SecurityAuditEvents.Add(new SecurityAuditEvent(
                Guid.NewGuid(),
                "seller_verification.expired",
                SystemActorId,
                verification.ApplicantUserId,
                JsonSerializer.Serialize(new { verification.Id, invalidation.InvalidatedVersion }),
                null,
                now));
            database.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "SellerEligibilityInvalidated",
                AggregateId = verification.ApplicantUserId,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    invalidation.Id,
                    invalidation.UserId,
                    invalidation.InvalidatedVersion,
                    invalidation.Reason
                }),
                OccurredAtUtc = now
            });
        }

        if (dueCases.Count == 0)
        {
            return;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            LogConcurrentExpiration(logger, exception);
        }
    }

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "A concurrent replica already processed one or more seller verification expirations.")]
    private static partial void LogConcurrentExpiration(ILogger logger, Exception exception);
}
