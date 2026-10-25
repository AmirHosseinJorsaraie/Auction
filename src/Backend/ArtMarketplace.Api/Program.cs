using System.Threading.RateLimiting;
using ArtMarketplace.Api;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Contracts.System;
using ArtMarketplace.Infrastructure;
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
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(TimeProvider.System);
var identityIssuer = builder.Configuration["Authentication:Issuer"]
    ?? throw new InvalidOperationException("Authentication:Issuer is required.");
var introspectionClientId = builder.Configuration["Authentication:Introspection:ClientId"]
    ?? throw new InvalidOperationException("Authentication:Introspection:ClientId is required.");
var introspectionClientSecret = builder.Configuration["Authentication:Introspection:ClientSecret"]
    ?? throw new InvalidOperationException("Authentication:Introspection:ClientSecret is required.");

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
        .RequireAssertion(context => context.User.GetClaims("amr").Contains("mfa", StringComparer.Ordinal)));
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
});

var app = builder.Build();

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

public partial class Program;
