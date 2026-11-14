using ArtMarketplace.Infrastructure;
using ArtMarketplace.Infrastructure.Health;
using ArtMarketplace.Infrastructure.Identity;
using ArtMarketplace.Infrastructure.Security;
using ArtMarketplace.PublicWeb;
using ArtMarketplace.PublicWeb.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
    ?? throw new InvalidOperationException("DataProtection:KeysPath is required.");
var authority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Authentication:Authority is required.");
var clientId = builder.Configuration["Authentication:ClientId"]
    ?? throw new InvalidOperationException("Authentication:ClientId is required.");
var clientSecret = builder.Configuration["Authentication:ClientSecret"]
    ?? throw new InvalidOperationException("Authentication:ClientSecret is required.");

builder.Services.AddTrustedProxyForwarding(builder.Configuration, builder.Environment);
if (builder.Environment.IsProduction())
{
    ProductionConfigurationGuard.RequireHttpsUri(builder.Configuration, "Authentication:Authority");
    ProductionConfigurationGuard.RequireSecret(builder.Configuration, "Authentication:ClientSecret");
    ProductionConfigurationGuard.RequireSecret(builder.Configuration, "BffSession:HandleHmacSecret");
}

Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddBffSessionPersistence(connectionString);
builder.Services.AddDataProtection()
    .SetApplicationName("ArtMarketplace.PublicWeb.Bff")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
builder.Services.AddOptions<BffSessionStoreOptions>()
    .Bind(builder.Configuration.GetRequiredSection(BffSessionStoreOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient("Api", client =>
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is required.")));
builder.Services.AddHttpClient("OidcBackchannel");
builder.Services.AddScoped<BffTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        // The browser receives only an opaque session handle. Tokens stay in PostgreSQL.
        options.Cookie.Name = "__Host-artmarketplace.public";
        options.Cookie.Domain = null;
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.Scope.Clear();
        string[] requestedScopes = ["openid", "profile", "email", "roles", "art-marketplace-api", "offline_access"];
        foreach (var scope in requestedScopes)
        {
            options.Scope.Add(scope);
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role",
            ValidateIssuer = true,
            ValidateAudience = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
        options.CorrelationCookie.Name = "__Host-artmarketplace.public.correlation";
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.None;

        var internalAuthority = builder.Configuration["Authentication:InternalAuthority"];
        if (Uri.TryCreate(internalAuthority, UriKind.Absolute, out var internalUri))
        {
            // Discovery still validates the public issuer; only server-to-server transport is rewritten.
            options.BackchannelHttpHandler = new AuthorityRewriteHandler(
                new Uri(authority), internalUri, new HttpClientHandler());
        }
    });

builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<PostgresTicketStore>((options, store) => options.SessionStore = store);
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-artmarketplace.public.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMarketplaceSecurityHeaders(path => path.StartsWithSegments("/api/bff") || path.StartsWithSegments("/auth"));
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"),
        branch => branch.UseHttpsRedirection());
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = item => item.Tags.Contains("live") }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = item => item.Tags.Contains("ready") }).AllowAnonymous();
app.MapPublicWebEndpoints();
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ArtMarketplace.PublicWeb.Client._Imports).Assembly)
    .AllowAnonymous();

app.Run();

public partial class Program;
