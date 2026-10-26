using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtMarketplace.Api;

internal sealed class ReservationExpiryService(IServiceScopeFactory scopes, TimeProvider time, ILogger<ReservationExpiryService> logger, IConfiguration configuration) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> BatchFailed = LoggerMessage.Define(LogLevel.Error, new EventId(6001, "ReservationExpiryBatchFailed"), "Reservation expiry batch failed; it will be retried.");
    private static readonly Action<ILogger, Guid, string?, Exception?> ExpiryDeferred = LoggerMessage.Define<Guid, string?>(LogLevel.Warning, new EventId(6002, "ReservationExpiryDeferred"), "Order {OrderId} expiry deferred: {Reason}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Clamp(configuration.GetValue("Orders:ExpiryScanSeconds", 30), 5, 300);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ExpireBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { BatchFailed(logger, ex); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    internal async Task ExpireBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(); var now = time.GetUtcNow();
        var candidates = await db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.AwaitingPayment && x.ReservationExpiresAtUtc <= now).OrderBy(x => x.ReservationExpiresAtUtc).ThenBy(x => x.Id).Select(x => x.Id).Take(25).ToListAsync(ct);
        foreach (var id in candidates) { var result = await Phase6OrderOperations.TerminateAsync(db, id, null, null, true, now, ct); if (!result.Success && !result.NotFound) ExpiryDeferred(logger, id, result.Error, null); db.ChangeTracker.Clear(); }
    }
}
