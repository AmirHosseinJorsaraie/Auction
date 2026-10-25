using System.Globalization;
using System.Threading.RateLimiting;
using ArtMarketplace.Identity.Security;
using ArtMarketplace.Identity.Seeding;
using ArtMarketplace.Identity.Services;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");

builder.Services.AddDbContext<MarketplaceDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseOpenIddict<Guid>();
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<MarketplaceDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-artmarketplace.identity";
    options.Cookie.Domain = null;
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;
});

var signingCertificate = CertificateLoader.LoadRequired(
    builder.Configuration.GetSection("OpenIddict:SigningCertificate"),
    "OpenIddict signing");
var encryptionCertificate = CertificateLoader.LoadRequired(
    builder.Configuration.GetSection("OpenIddict:EncryptionCertificate"),
    "OpenIddict encryption");
var issuer = builder.Configuration["OpenIddict:Issuer"]
    ?? throw new InvalidOperationException("OpenIddict:Issuer is required.");

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<MarketplaceDbContext>()
            .ReplaceDefaultEntities<Guid>();
    })
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(issuer, UriKind.Absolute));
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .SetEndSessionEndpointUris("connect/logout")
            .SetIntrospectionEndpointUris("connect/introspect")
            .SetRevocationEndpointUris("connect/revoke")
            .SetUserInfoEndpointUris("connect/userinfo");

        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles,
            "art-marketplace-api");

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
        options.UseReferenceAccessTokens();
        options.UseReferenceRefreshTokens();
        options.AddSigningCertificate(signingCertificate);
        options.AddEncryptionCertificate(encryptionCertificate);

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableStatusCodePagesIntegration();
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("identity", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    CultureInfo[] supportedCultures = [new("fa-IR"), new("en-US")];
    options.DefaultRequestCulture = new RequestCulture("fa-IR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHostedService<IdentitySeedService>();
builder.Services.AddHostedService<SellerVerificationExpirationService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("identity");
app.MapRazorPages().RequireRateLimiting("identity");

app.Run();

public partial class Program;
