namespace ArtMarketplace.Domain.Modules.Catalog;

public enum ArtworkAssetKind { PublicImage = 0, PrivateOriginal = 1, PrivateDocument = 2 }

public sealed class ArtworkAsset
{
    private ArtworkAsset() { }
    public ArtworkAsset(Guid id, Guid revisionId, ArtworkAssetKind kind, string cleanObjectKey,
        string sha256, long size, string contentType, string? versionId, string? publicDerivativeObjectKey, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || revisionId == Guid.Empty) throw new ArgumentException("Asset and revision identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (sha256.Length != 64 || sha256.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("A SHA-256 hex digest is required.", nameof(sha256));
        var cleanKey = Required(cleanObjectKey, 500);

        if (cleanKey.StartsWith("quarantine/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An artwork asset cannot reference quarantine storage.", nameof(cleanObjectKey));

        if (kind == ArtworkAssetKind.PublicImage && string.IsNullOrWhiteSpace(publicDerivativeObjectKey))
            throw new ArgumentException("A public image requires a sanitized public derivative.", nameof(publicDerivativeObjectKey));

        Id = id; ArtworkRevisionId = revisionId; Kind = kind;
        CleanObjectKey = cleanKey; Sha256 = sha256.ToUpperInvariant();
        Size = size; ContentType = Required(contentType, 100); VersionId = versionId;
        PublicDerivativeObjectKey = publicDerivativeObjectKey; CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ArtworkRevisionId { get; private set; }
    public ArtworkAssetKind Kind { get; private set; }
    public string CleanObjectKey { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public string? VersionId { get; private set; }
    public string? PublicDerivativeObjectKey { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    private static string Required(string value, int max) { var v=value.Trim(); if(v.Length is 0 || v.Length>max) throw new ArgumentException("Invalid value."); return v; }
}
