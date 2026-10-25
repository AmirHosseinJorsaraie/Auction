namespace ArtMarketplace.Application.Storage;

public interface IFileSafetyScanner
{
    Task<FileScanResult> ScanAsync(Stream content, string contentType, CancellationToken cancellationToken);
}

public sealed record FileScanResult(bool IsClean, string Reason)
{
    public static FileScanResult Clean() => new(true, "Clean.");
    public static FileScanResult Rejected(string reason) => new(false, reason);
}
