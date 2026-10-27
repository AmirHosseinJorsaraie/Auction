using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArtMarketplace.Infrastructure.Security;

public static class ProductionConfigurationGuard
{
    private static readonly string[] PlaceholderFragments =
        ["change-me", "replace-me", "development-only", "example.invalid"];

    public static void RequireSecret(IConfiguration configuration, string key, int minimumLength = 32)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || PlaceholderFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Production configuration '{key}' must be supplied by the secret store and cannot contain a placeholder.");
        }
    }

    public static void RequireHttpsUri(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || PlaceholderFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Production configuration '{key}' must be an absolute HTTPS URI.");
        }
    }

    public static IServiceCollection AddTrustedProxyForwarding(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configured = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        if (environment.IsProduction() && configured.Length == 0)
        {
            throw new InvalidOperationException("Production requires at least one ReverseProxy:KnownProxies address.");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var value in configured)
            {
                if (!IPAddress.TryParse(value, out var address))
                    throw new InvalidOperationException($"ReverseProxy:KnownProxies contains invalid IP address '{value}'.");
                options.KnownProxies.Add(address);
            }
        });
        return services;
    }

    public static IApplicationBuilder UseMarketplaceSecurityHeaders(
        this IApplicationBuilder app,
        Func<PathString, bool>? containsPrivateData = null)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                headers.ContentSecurityPolicy = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
                headers["X-Trace-Id"] = context.TraceIdentifier;
                if (containsPrivateData?.Invoke(context.Request.Path) == true)
                    headers.CacheControl = "no-store, max-age=0";
                return Task.CompletedTask;
            });
            await next(context);
        });
    }
}
