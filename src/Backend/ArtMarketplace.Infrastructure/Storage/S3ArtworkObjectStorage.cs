using Amazon.S3;
using Amazon.S3.Model;
using ArtMarketplace.Application.Storage;
using Microsoft.Extensions.Options;

namespace ArtMarketplace.Infrastructure.Storage;

public sealed class S3ArtworkObjectStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> options)
    : IArtworkObjectStorage
{
    private readonly ObjectStorageOptions _options = options.Value;

    public string CreateQuarantineUploadUrl(string objectKey, string contentType, long contentLength, DateTimeOffset expiresAtUtc)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.QuarantineBucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = expiresAtUtc.UtcDateTime,
            ContentType = contentType
        };
        request.Metadata["expected-length"] = contentLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return s3.GetPreSignedURL(request);
    }

    public async Task<StoredObject> ReadQuarantineAsync(string objectKey, long maximumBytes, CancellationToken cancellationToken)
    {
        using var response = await s3.GetObjectAsync(_options.QuarantineBucket, objectKey, cancellationToken);
        if (response.ContentLength <= 0 || response.ContentLength > maximumBytes)
            throw new InvalidDataException("Uploaded object size is outside the permitted range.");

        var memory = new MemoryStream((int)response.ContentLength);
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        if (memory.Length != response.ContentLength) { memory.Dispose(); throw new InvalidDataException("Uploaded object length changed during read."); }
        memory.Position = 0;
        return new StoredObject(memory, memory.Length, response.VersionId);
    }

    public async Task<PromotedObject> PutCleanIfAbsentAsync(string objectKey, Stream content, string contentType, string sha256, CancellationToken cancellationToken)
        => await PutIfAbsentAsync(_options.CleanPrivateBucket, objectKey, content, contentType, sha256, cancellationToken);

    public async Task<PromotedObject> PutPublicDerivativeIfAbsentAsync(string objectKey, Stream content, string contentType, string sha256, CancellationToken cancellationToken)
        => await PutIfAbsentAsync(_options.PublicDerivativeBucket, objectKey, content, contentType, sha256, cancellationToken);

    private async Task<PromotedObject> PutIfAbsentAsync(string bucket, string objectKey, Stream content, string contentType, string sha256, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            IfNoneMatch = "*"
        };
        request.Metadata["sha256"] = sha256;
        var response = await s3.PutObjectAsync(request, cancellationToken);
        return new PromotedObject(objectKey, response.VersionId);
    }

    public Task DeleteQuarantineAsync(string objectKey, CancellationToken cancellationToken) =>
        s3.DeleteObjectAsync(_options.QuarantineBucket, objectKey, cancellationToken);

    public string CreatePrivateDownloadUrl(string objectKey, DateTimeOffset expiresAtUtc) =>
        s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.CleanPrivateBucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAtUtc.UtcDateTime
        });

    public string CreatePublicReadUrl(string objectKey, DateTimeOffset expiresAtUtc) =>
        s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.PublicDerivativeBucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAtUtc.UtcDateTime
        });
}
