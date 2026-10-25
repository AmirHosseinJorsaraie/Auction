using System.Text;
using ArtMarketplace.Application.Storage;

namespace ArtMarketplace.Infrastructure.Storage;

public sealed class DevelopmentFileSafetyScanner : IFileSafetyScanner
{
    private static readonly byte[] EicarMarker = Encoding.ASCII.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE");
    public async Task<FileScanResult> ScanAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        content.Position = 0;
        return memory.ToArray().AsSpan().IndexOf(EicarMarker) >= 0
            ? FileScanResult.Rejected("Malware test signature detected.")
            : FileScanResult.Clean();
    }
}

public sealed class FailClosedFileSafetyScanner : IFileSafetyScanner
{
    public Task<FileScanResult> ScanAsync(Stream content, string contentType, CancellationToken cancellationToken) =>
        Task.FromResult(FileScanResult.Rejected("No production malware scanner is configured."));
}
