using ArtMarketplace.Application.Common;

namespace ArtMarketplace.Application.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void ApplicationContractExposesUtcTime()
    {
        Assert.Equal(typeof(DateTimeOffset), typeof(IClock).GetProperty(nameof(IClock.UtcNow))!.PropertyType);
    }
}
