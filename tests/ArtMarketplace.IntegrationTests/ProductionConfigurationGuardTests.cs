using ArtMarketplace.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ArtMarketplace.IntegrationTests;

public sealed class ProductionConfigurationGuardTests
{
    [Fact]
    public void PlaceholderSecretIsRejected()
    {
        var configuration = Configuration(("Security:Secret", "replace-me-with-a-long-placeholder-value"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationGuard.RequireSecret(configuration, "Security:Secret"));

        Assert.Contains("secret store", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StrongSecretIsAccepted()
    {
        var configuration = Configuration(("Security:Secret", "a-strong-random-production-value-123456789"));

        ProductionConfigurationGuard.RequireSecret(configuration, "Security:Secret");
    }

    [Theory]
    [InlineData("http://identity.example.com")]
    [InlineData("https://identity.example.invalid")]
    [InlineData("not-a-uri")]
    [InlineData("")]
    public void NonHttpsProductionUriIsRejected(string value)
    {
        var configuration = Configuration(("Security:Issuer", value));

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationGuard.RequireHttpsUri(configuration, "Security:Issuer"));
    }

    [Fact]
    public void HttpsProductionUriIsAccepted()
    {
        var configuration = Configuration(("Security:Issuer", "https://identity.example.com/"));

        ProductionConfigurationGuard.RequireHttpsUri(configuration, "Security:Issuer");
    }

    [Fact]
    public void ProductionRequiresExplicitTrustedProxy()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddTrustedProxyForwarding(Configuration(), new TestEnvironment(Environments.Production)));

        Assert.Contains("KnownProxies", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAcceptsValidTrustedProxy()
    {
        var services = new ServiceCollection();

        services.AddTrustedProxyForwarding(
            Configuration(("ReverseProxy:KnownProxies:0", "10.0.0.5")),
            new TestEnvironment(Environments.Production));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder().AddInMemoryCollection(entries.Select(item =>
            new KeyValuePair<string, string?>(item.Key, item.Value))).Build();

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
