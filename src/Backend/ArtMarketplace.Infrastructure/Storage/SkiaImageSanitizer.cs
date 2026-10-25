using ArtMarketplace.Application.Storage;
using SkiaSharp;

namespace ArtMarketplace.Infrastructure.Storage;

public sealed class SkiaImageSanitizer : IImageSanitizer
{
    public Task<SanitizedFile> SanitizeAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var input = SKData.Create(content);
        using var codec = SKCodec.Create(input) ?? throw new InvalidDataException("Image decoder rejected the file.");
        if (codec.Info.Width is < 1 or > 12000 || codec.Info.Height is < 1 or > 12000)
            throw new InvalidDataException("Image dimensions are outside the permitted range.");
        using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Image pixels could not be decoded.");
        using var image = SKImage.FromBitmap(bitmap);
        var format = contentType == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        using var encoded = image.Encode(format, 90) ?? throw new InvalidDataException("Image could not be re-encoded.");
        var output = new MemoryStream(encoded.ToArray(), writable: false);
        return Task.FromResult(new SanitizedFile(output, format == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg"));
    }
}
