using ArtMarketplace.Domain.Modules.Catalog;

namespace ArtMarketplace.Domain.Tests;

public sealed class ArtworkRevisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FrozenRevisionCannotBeEdited()
    {
        var owner = Guid.NewGuid();
        var revision = Create(owner);

        revision.Freeze(owner, Now.AddMinutes(1));

        Assert.Equal(ArtworkRevisionStatus.Frozen, revision.Status);
        Assert.Throws<InvalidOperationException>(() => revision.Update(owner, "Changed", "Artist", "Description",
            Guid.NewGuid(), 2020, "Oil", "10x10", "Good", Now.AddMinutes(2)));
    }

    [Fact]
    public void DraftCannotBeEditedByADifferentOwner()
    {
        var revision = Create(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => revision.Update(Guid.NewGuid(), "Changed", "Artist", "Description",
            Guid.NewGuid(), 2020, "Oil", "10x10", "Good", Now));
    }

    private static ArtworkRevision Create(Guid owner) => new(Guid.NewGuid(), Guid.NewGuid(), 1, owner,
        "اثر آزمایشی", "هنرمند", "توضیحات اثر", Guid.NewGuid(), 2020, "رنگ روغن", "10x10 cm", "سالم", Now);
}
