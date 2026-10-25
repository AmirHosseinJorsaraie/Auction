using ArtMarketplace.Application.Common;
using ArtMarketplace.Contracts.System;
using ArtMarketplace.Domain.Common;
using ArtMarketplace.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace ArtMarketplace.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Theory]
    [InlineData("ArtMarketplace.Application")]
    [InlineData("ArtMarketplace.Infrastructure")]
    [InlineData("ArtMarketplace.Api")]
    [InlineData("ArtMarketplace.Contracts")]
    public void DomainDoesNotDependOnOuterLayers(string forbiddenNamespace)
    {
        var result = Types.InAssembly(typeof(Money).Assembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"Domain depends on {forbiddenNamespace}.");
    }

    [Theory]
    [InlineData("ArtMarketplace.Infrastructure")]
    [InlineData("ArtMarketplace.Api")]
    [InlineData("ArtMarketplace.Contracts")]
    public void ApplicationDoesNotDependOnOuterLayers(string forbiddenNamespace)
    {
        var result = Types.InAssembly(typeof(IClock).Assembly)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, $"Application depends on {forbiddenNamespace}.");
    }

    [Fact]
    public void ContractsAreTransportOnly()
    {
        var result = Types.InAssembly(typeof(ServiceStatusResponse).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                typeof(Money).Assembly.GetName().Name!,
                typeof(IClock).Assembly.GetName().Name!,
                typeof(MarketplaceDbContext).Assembly.GetName().Name!)
            .GetResult();

        Assert.True(result.IsSuccessful, "Contracts reference an implementation layer.");
    }
}
