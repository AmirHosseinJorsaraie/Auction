using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ArtMarketplace.Infrastructure.Health;

public sealed class ObjectStorageHealthCheck(IAmazonS3 s3) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await s3.ListBucketsAsync(cancellationToken);
            return HealthCheckResult.Healthy("Object storage is reachable and authenticated.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Object storage is unreachable.", exception);
        }
    }
}
