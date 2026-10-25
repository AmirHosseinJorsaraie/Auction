namespace ArtMarketplace.Application.Storage;

public interface IImageSanitizer
{
    Task<SanitizedFile> SanitizeAsync(Stream content, string contentType, CancellationToken cancellationToken);
}

public sealed record SanitizedFile(Stream Content, string ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
