using ArtMarketplace.Domain.Modules.Catalog;

namespace ArtMarketplace.Domain.Tests;

public sealed class UploadIntentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IntentIsSingleUse()
    {
        var user = Guid.NewGuid();
        var intent = Create(user);
        intent.BeginProcessing(user, Now);
        intent.Complete(Now);

        Assert.Equal(UploadIntentStatus.Completed, intent.Status);
        Assert.Throws<InvalidOperationException>(() => intent.BeginProcessing(user, Now));
    }

    [Fact]
    public void ExpiredIntentFailsClosed()
    {
        var user = Guid.NewGuid();
        var intent = Create(user);
        Assert.Throws<InvalidOperationException>(() => intent.BeginProcessing(user, Now.AddMinutes(11)));
        Assert.Equal(UploadIntentStatus.Expired, intent.Status);
    }

    private static UploadIntent Create(Guid user) => new(Guid.NewGuid(), user, Guid.NewGuid(), Guid.NewGuid(),
        ArtworkAssetKind.PublicImage, "art.jpg", "image/jpeg", 100, "quarantine/test", Now, Now.AddMinutes(10));
}
