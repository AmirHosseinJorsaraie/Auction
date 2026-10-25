using ArtMarketplace.Domain.Modules.Catalog;

namespace ArtMarketplace.Domain.Tests;

public sealed class ArtworkAssetTests
{
    [Fact]
    public void AssetCannotPinQuarantineObject()
    {
        Assert.Throws<ArgumentException>(() => new ArtworkAsset(Guid.NewGuid(), Guid.NewGuid(),
            ArtworkAssetKind.PrivateDocument, "quarantine/user/file", new string('A', 64), 100,
            "application/pdf", null, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PublicImageRequiresPublicDerivative()
    {
        Assert.Throws<ArgumentException>(() => new ArtworkAsset(Guid.NewGuid(), Guid.NewGuid(),
            ArtworkAssetKind.PublicImage, "artworks/clean.jpg", new string('A', 64), 100,
            "image/jpeg", null, null, DateTimeOffset.UtcNow));
    }
}
