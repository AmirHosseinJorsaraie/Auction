using ArtMarketplace.Backoffice.Components;

namespace ArtMarketplace.Backoffice.Tests;

public sealed class BackofficeAssemblyTests
{
    [Fact]
    public void AppComponentIsCompiled()
    {
        Assert.Equal("App", typeof(App).Name);
    }
}
