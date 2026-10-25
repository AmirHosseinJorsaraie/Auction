namespace ArtMarketplace.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
