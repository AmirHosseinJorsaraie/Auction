using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ArtMarketplace.Infrastructure.Persistence;

public sealed class MarketplaceDbContextFactory : IDesignTimeDbContextFactory<MarketplaceDbContext>
{
    public MarketplaceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=art_marketplace;Username=art_marketplace;Password=design-time-only";
        var optionsBuilder = new DbContextOptionsBuilder<MarketplaceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseOpenIddict<Guid>();

        return new MarketplaceDbContext(optionsBuilder.Options);
    }
}
