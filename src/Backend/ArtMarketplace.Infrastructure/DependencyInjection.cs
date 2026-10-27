using ArtMarketplace.Application.Common;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Infrastructure.Health;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using ArtMarketplace.Infrastructure.Storage;
using ArtMarketplace.Infrastructure.Time;
using ArtMarketplace.Application.Storage;
using ArtMarketplace.Application.Payments;
using ArtMarketplace.Infrastructure.Payments;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArtMarketplace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBffSessionPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MarketplaceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<PostgresTicketStore>();
        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Microsoft.Extensions.Hosting.IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");

        services.AddDbContext<MarketplaceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ISellerEligibilityGate, SellerEligibilityGate>();
        services.AddOptions<PaymentProviderOptions>()
            .Bind(configuration.GetRequiredSection(PaymentProviderOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => !environment.IsProduction() || !string.Equals(options.Provider, "Fake", StringComparison.OrdinalIgnoreCase), "The Fake payment provider cannot run in Production.")
            .Validate(options => string.Equals(options.Provider, "Fake", StringComparison.OrdinalIgnoreCase), "No real payment provider is configured in V1.")
            .ValidateOnStart();
        services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetRequiredSection(ObjectStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var storage = configuration.GetRequiredSection(ObjectStorageOptions.SectionName)
            .Get<ObjectStorageOptions>() ?? throw new InvalidOperationException("Storage configuration is required.");
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = storage.Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            }));
        services.AddSingleton<IArtworkObjectStorage, S3ArtworkObjectStorage>();
        services.AddSingleton<IImageSanitizer, SkiaImageSanitizer>();
        if (environment.IsDevelopment())
            services.AddSingleton<IFileSafetyScanner, DevelopmentFileSafetyScanner>();
        else
            services.AddSingleton<IFileSafetyScanner, FailClosedFileSafetyScanner>();
        services.AddHttpClient(nameof(ObjectStorageHealthCheck), client =>
            client.Timeout = TimeSpan.FromSeconds(3));

        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"])
            .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"]);

        return services;
    }
}
