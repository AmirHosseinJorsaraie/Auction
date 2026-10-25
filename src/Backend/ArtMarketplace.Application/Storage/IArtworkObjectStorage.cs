namespace ArtMarketplace.Application.Storage;

public interface IArtworkObjectStorage
{
    string CreateQuarantineUploadUrl(string objectKey, string contentType, long contentLength, DateTimeOffset expiresAtUtc);
    Task<StoredObject> ReadQuarantineAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken);
    Task<PromotedObject> PutCleanIfAbsentAsync(string objectKey, Stream content, string contentType, string sha256, CancellationToken cancellationToken);
    Task<PromotedObject> PutPublicDerivativeIfAbsentAsync(string objectKey, Stream content, string contentType, string sha256, CancellationToken cancellationToken);
    Task DeleteQuarantineAsync(string objectKey, CancellationToken cancellationToken);
    string CreatePrivateDownloadUrl(string objectKey, DateTimeOffset expiresAtUtc);
    string CreatePublicReadUrl(string objectKey, DateTimeOffset expiresAtUtc);
}

public sealed record StoredObject(Stream Content, long Length, string? VersionId) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed record PromotedObject(string ObjectKey, string? VersionId);
