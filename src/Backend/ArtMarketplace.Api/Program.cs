using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using ArtMarketplace.Api;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Contracts.System;
using ArtMarketplace.Infrastructure;
using ArtMarketplace.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ArtMarketplace.Api")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddTrustedProxyForwarding(builder.Configuration, builder.Environment);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ReservationExpiryService>();
var identityIssuer = builder.Configuration["Authentication:Issuer"]
    ?? throw new InvalidOperationException("Authentication:Issuer is required.");
var introspectionClientId = builder.Configuration["Authentication:Introspection:ClientId"]
    ?? throw new InvalidOperationException("Authentication:Introspection:ClientId is required.");
var introspectionClientSecret = builder.Configuration["Authentication:Introspection:ClientSecret"]
    ?? throw new InvalidOperationException("Authentication:Introspection:ClientSecret is required.");
if (builder.Environment.IsProduction())
{
    ProductionConfigurationGuard.RequireHttpsUri(builder.Configuration, "Authentication:Issuer");
    ProductionConfigurationGuard.RequireHttpsUri(builder.Configuration, "Payments:PublicBaseUrl");
    ProductionConfigurationGuard.RequireSecret(builder.Configuration, "Authentication:Introspection:ClientSecret");
    ProductionConfigurationGuard.RequireSecret(builder.Configuration, "Payments:CallbackSecret");
    ProductionConfigurationGuard.RequireSecret(builder.Configuration, "Storage:SecretKey");
}

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(new Uri(identityIssuer, UriKind.Absolute));
        options.AddAudiences("art-marketplace-api");
        options.UseIntrospection()
            .SetClientId(introspectionClientId)
            .SetClientSecret(introspectionClientSecret);
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(MarketplacePolicies.StaffWithMfa, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Expert", "Admin", "SuperAdmin")
        .RequireAssertion(context => context.User.GetClaims("amr").Contains("mfa", StringComparer.Ordinal)))
    .AddPolicy(MarketplacePolicies.VerifySellers, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Expert", "Admin", "SuperAdmin")
        .RequireAssertion(context => context.User.HasScope("art-marketplace-api"))
        .RequireAssertion(context => context.User.GetClaims("amr").Contains("mfa", StringComparer.Ordinal)))
    .AddPolicy(MarketplacePolicies.SuperAdminWithMfa, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("SuperAdmin")
        .RequireAssertion(context => context.User.GetClaims("amr").Contains("mfa", StringComparer.Ordinal)))
    .AddPolicy(MarketplacePolicies.ReviewsDecide, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(MarketplacePermissions.ClaimType, MarketplacePermissions.ReviewsDecide)
        .RequireAssertion(context => context.User.HasScope("art-marketplace-api"))
        .RequireAssertion(context => context.User.GetClaims("amr").Contains("mfa", StringComparer.Ordinal)))
    .AddPolicy(MarketplacePolicies.ReviewsOverride, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(MarketplacePermissions.ClaimType, MarketplacePermissions.ReviewsOverride)
        .RequireAssertion(context => context.User.HasScope("art-marketplace-api"))
        .RequireAssertion(context => context.User.GetClaims("amr").Contains("mfa", StringComparer.Ordinal)))
    .AddPolicy(MarketplacePolicies.AdministrationRead, policy => ConfigurePermissionPolicy(policy, MarketplacePermissions.AdministrationRead))
    .AddPolicy(MarketplacePolicies.UsersManage, policy => ConfigurePermissionPolicy(policy, MarketplacePermissions.UsersManage))
    .AddPolicy(MarketplacePolicies.RolesManage, policy => ConfigurePermissionPolicy(policy, MarketplacePermissions.RolesManage))
    .AddPolicy(MarketplacePolicies.CatalogManage, policy => ConfigurePermissionPolicy(policy, MarketplacePermissions.CatalogManage))
    .AddPolicy(MarketplacePolicies.ContentManage, policy => ConfigurePermissionPolicy(policy, MarketplacePermissions.ContentManage))
    .AddPolicy(MarketplacePolicies.AuditRead, policy => ConfigurePermissionPolicy(policy, MarketplacePermissions.AuditRead))
    .AddPolicy(MarketplacePolicies.PaymentsRead, policy => ConfigurePermissionPolicy(policy, MarketplacePermissions.PaymentsRead));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("baseline", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
        limiter.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60));
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("upload-intent", limiter =>
    {
        limiter.PermitLimit = 20; limiter.Window = TimeSpan.FromMinutes(10); limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("upload-finalize", limiter =>
    {
        limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(10); limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("private-download", limiter =>
    {
        limiter.PermitLimit = 30; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("checkout", limiter =>
    {
        limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("payment", limiter =>
    {
        limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("payment-callback", limiter =>
    {
        limiter.PermitLimit = 120; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseHttpsRedirection());
}
app.UseMarketplaceSecurityHeaders(path =>
    path.StartsWithSegments("/api/account")
    || path.StartsWithSegments("/api/backoffice")
    || (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/public")));
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi().DisableRateLimiting();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Art Marketplace API v1"));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponseAsync,
}).DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync,
}).DisableRateLimiting();

app.MapGet("/api/system/status", (TimeProvider timeProvider) =>
        TypedResults.Ok(new ServiceStatusResponse("ArtMarketplace.Api", "ok", timeProvider.GetUtcNow())))
    .RequireRateLimiting("baseline")
    .WithName("GetServiceStatus")
    .WithTags("System");

app.MapGet("/api/account/me", (HttpContext context) => Results.Ok(new
    {
        subject = context.User.GetClaim(OpenIddictConstants.Claims.Subject),
        name = context.User.GetClaim(OpenIddictConstants.Claims.Name),
        email = context.User.GetClaim(OpenIddictConstants.Claims.Email),
        roles = context.User.GetClaims(OpenIddictConstants.Claims.Role),
        authorizationVersion = context.User.GetClaim("authorization_version")
    }))
    .RequireAuthorization()
    .RequireRateLimiting("baseline")
    .WithTags("Account");

app.MapPhase2Endpoints();
app.MapPhase3Endpoints();
app.MapPhase4Endpoints();
app.MapPhase5Endpoints();
app.MapPhase6Endpoints();
app.MapPhase7Endpoints();
app.MapPhase8Endpoints();

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
        }),
    });
}

static void ConfigurePermissionPolicy(Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder policy, string permission) => policy
    .RequireAuthenticatedUser()
    .RequireClaim(MarketplacePermissions.ClaimType, permission)
    .RequireAssertion(context => context.User.HasScope("art-marketplace-api"))
    .RequireAssertion(context => context.User.GetClaims("amr").Contains("mfa", StringComparer.Ordinal));

public partial class Program;
