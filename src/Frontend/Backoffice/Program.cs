using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Backoffice.Components;
using ArtMarketplace.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using ArtMarketplace.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using ArtMarketplace.Backoffice;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
    ?? throw new InvalidOperationException("DataProtection:KeysPath is required.");
var oidcAuthority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Authentication:Authority is required.");
var oidcClientId = builder.Configuration["Authentication:ClientId"]
    ?? throw new InvalidOperationException("Authentication:ClientId is required.");
var oidcClientSecret = builder.Configuration["Authentication:ClientSecret"]
    ?? throw new InvalidOperationException("Authentication:ClientSecret is required.");

Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddBffSessionPersistence(connectionString);
builder.Services.AddDataProtection()
    .SetApplicationName("ArtMarketplace.Backoffice.Bff")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
builder.Services.AddOptions<BffSessionStoreOptions>()
    .Bind(builder.Configuration.GetRequiredSection(BffSessionStoreOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("Api", client => client.BaseAddress = new Uri(
    builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl is required.")));
builder.Services.AddScoped<ReviewApiClient>();
builder.Services.AddScoped<AdminApiClient>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-artmarketplace.backoffice";
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
        options.Authority = oidcAuthority;
        options.ClientId = oidcClientId;
        options.ClientSecret = oidcClientSecret;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("roles");
        options.Scope.Add("art-marketplace-api");
        options.Scope.Add("offline_access");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role",
            ValidateIssuer = true,
            ValidateAudience = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
        options.CorrelationCookie.Name = "__Host-artmarketplace.backoffice.correlation";
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.None;
    });

builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<PostgresTicketStore>((options, ticketStore) => options.SessionStore = ticketStore);

var authorization = builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("Expert", "Admin", "SuperAdmin")
        .RequireClaim("amr", "mfa")
        .Build())
    .AddPolicy(MarketplacePolicies.SuperAdminWithMfa, policy => policy
        .RequireRole("SuperAdmin")
        .RequireClaim("amr", "mfa"));
foreach (var (policyName, permission) in new[]
{
    (MarketplacePolicies.AdministrationRead, MarketplacePermissions.AdministrationRead),
    (MarketplacePolicies.UsersManage, MarketplacePermissions.UsersManage),
    (MarketplacePolicies.RolesManage, MarketplacePermissions.RolesManage),
    (MarketplacePolicies.CatalogManage, MarketplacePermissions.CatalogManage),
    (MarketplacePolicies.ContentManage, MarketplacePermissions.ContentManage),
    (MarketplacePolicies.AuditRead, MarketplacePermissions.AuditRead),
    (MarketplacePolicies.PaymentsRead, MarketplacePermissions.PaymentsRead),
    (MarketplacePolicies.ReviewsDecide, MarketplacePermissions.ReviewsDecide)
})
{
    authorization.AddPolicy(policyName, policy => policy.RequireClaim(MarketplacePermissions.ClaimType, permission).RequireClaim("amr", "mfa"));
}
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/auth/login", (HttpContext context, string? returnUrl) =>
    {
        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0 && fetchSite is not "same-origin" and not "none")
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = SafeLocalReturnUrl(returnUrl) },
            [OpenIdConnectDefaults.AuthenticationScheme]);
    })
    .AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery) =>
    {
        await antiforgery.ValidateRequestAsync(context);
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
    })
    .RequireAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();
app.MapReviewProxyEndpoints();
app.MapAdminProxyEndpoints();

app.Run();

static string SafeLocalReturnUrl(string? returnUrl) =>
    !string.IsNullOrWhiteSpace(returnUrl)
        && Uri.TryCreate(returnUrl, UriKind.Relative, out _)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/";

public partial class Program;
