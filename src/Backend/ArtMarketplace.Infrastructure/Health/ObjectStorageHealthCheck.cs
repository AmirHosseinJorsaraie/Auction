using ArtMarketplace.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ArtMarketplace.Infrastructure.Health;

public sealed class ObjectStorageHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<ObjectStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, options.Value.Endpoint);
            using var response = await httpClientFactory.CreateClient(nameof(ObjectStorageHealthCheck))
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Object storage is reachable.")
                : HealthCheckResult.Unhealthy($"Object storage returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Object storage is unreachable.", exception);
        }
    }
}
