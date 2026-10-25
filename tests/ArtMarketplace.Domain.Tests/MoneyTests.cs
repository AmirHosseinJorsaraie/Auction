using ArtMarketplace.Domain.Common;

namespace ArtMarketplace.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void ConstructorRejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1m, "IRR"));
    }

    [Theory]
    [InlineData("irr")]
    [InlineData("IR")]
    [InlineData("IRRR")]
    public void ConstructorRequiresUppercaseIsoCurrency(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, currency));
    }
}
