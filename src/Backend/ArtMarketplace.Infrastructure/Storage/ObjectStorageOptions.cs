using System.ComponentModel.DataAnnotations;

namespace ArtMarketplace.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "Storage";

    [Required, Url]
    public string Endpoint { get; init; } = string.Empty;

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    public string QuarantineBucket { get; init; } = string.Empty;

    [Required]
    public string CleanPrivateBucket { get; init; } = string.Empty;

    [Required]
    public string PublicDerivativeBucket { get; init; } = string.Empty;

    [Range(1, 60)]
    public int UploadUrlMinutes { get; init; } = 10;

    [Range(1, 15)]
    public int PrivateDownloadUrlMinutes { get; init; } = 2;
}
