using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ArtMarketplace.IntegrationTests;

public sealed class PostgreSqlMigrationSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:17.6-alpine@sha256:ef257d85f76e48da1c64832459b59fcaba1a4dac97bf5d7450c77753542eee94")
        .WithDatabase("art_marketplace_tests")
        .WithUsername("art_marketplace")
        .WithPassword("test-only-password")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task MigrationsApplyToRealPostgresql()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var dbContext = new MarketplaceDbContext(options);
        await dbContext.Database.MigrateAsync();

        Assert.True(await dbContext.Database.CanConnectAsync());
        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains("202610170001_InitialFoundation", applied);
        Assert.Contains("20261019191732_Phase2SecurityAuditOutbox", applied);
        Assert.Contains("20261024153457_Phase3CatalogArtworkIntake", applied);
        Assert.Contains("20261024155926_Phase3RelationalConstraints", applied);
        Assert.Contains("20261025124706_Phase4ExpertReviewModeration", applied);
        Assert.Contains("20261025124946_Phase4ReviewAnnotations", applied);
        Assert.Contains("20261025143817_Phase5PublicGalleryListings", applied);
        Assert.Contains("20261026145542_Phase6CartCheckoutOrders", applied);
        Assert.Contains("20261026145822_Phase6OrderConstraints", applied);
    }
}
