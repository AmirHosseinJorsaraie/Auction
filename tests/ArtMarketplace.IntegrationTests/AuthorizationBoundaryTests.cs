using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ArtMarketplace.Application.Authorization;
using OpenIddict.Abstractions;

namespace ArtMarketplace.IntegrationTests;

public sealed class AuthorizationBoundaryTests
{
    [Fact]
    public async Task AccountEndpointReturns401WithoutAuthentication()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/account/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrderDetailReturns401WithoutAuthentication()
    {
        await using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri($"/api/orders/{Guid.NewGuid()}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerificationQueueReturns403ForOrdinaryAuthenticatedUser()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "ordinary");

        var response = await client.GetAsync(
            new Uri("/api/backoffice/seller-verifications/pending", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReviewQueueReturns403ForAdminRoleWithoutReviewsDecidePermission()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "admin-with-mfa");

        var response = await client.GetAsync(new Uri("/api/backoffice/reviews/queue", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("ordinary", "administration", HttpStatusCode.Forbidden)]
    [InlineData("ordinary", "reviews", HttpStatusCode.Forbidden)]
    [InlineData("expert-with-mfa", "reviews", HttpStatusCode.NoContent)]
    [InlineData("expert-with-mfa", "administration", HttpStatusCode.Forbidden)]
    [InlineData("admin-with-mfa", "administration", HttpStatusCode.NoContent)]
    [InlineData("admin-with-mfa", "users", HttpStatusCode.NoContent)]
    [InlineData("admin-with-mfa", "roles", HttpStatusCode.Forbidden)]
    [InlineData("superadmin-with-mfa", "administration", HttpStatusCode.NoContent)]
    [InlineData("superadmin-with-mfa", "users", HttpStatusCode.NoContent)]
    [InlineData("superadmin-with-mfa", "roles", HttpStatusCode.NoContent)]
    [InlineData("superadmin-with-mfa", "reviews", HttpStatusCode.NoContent)]
    public async Task FourRolePermissionMatrixIsEnforced(string identity, string capability, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory(); using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-User", identity);
        var response = await client.GetAsync(new Uri($"/api/backoffice/authorization/probe/{capability}", UriKind.Relative));
        Assert.Equal(expected, response.StatusCode);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });
            });
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "integration-test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var testUser))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = testUser.ToString(); var staff = identity is "expert-with-mfa" or "admin-with-mfa" or "superadmin-with-mfa";
            var role = identity switch { "expert-with-mfa" => "Expert", "admin-with-mfa" => "Admin", "superadmin-with-mfa" => "SuperAdmin", _ => "User" };
            var claims = new List<Claim>
            {
                new("sub", Guid.NewGuid().ToString()),
                new("name", "Test user"),
                new("role", role),
                new("scope", "art-marketplace-api"),
                new("amr", staff ? "mfa" : "pwd")
            };
            if (identity == "expert-with-mfa") claims.Add(new(MarketplacePermissions.ClaimType, MarketplacePermissions.ReviewsDecide));
            if (identity is "admin-with-mfa" or "superadmin-with-mfa")
                foreach (var permission in new[] { MarketplacePermissions.AdministrationRead, MarketplacePermissions.UsersManage, MarketplacePermissions.CatalogManage, MarketplacePermissions.ContentManage, MarketplacePermissions.AuditRead, MarketplacePermissions.PaymentsRead }) claims.Add(new(MarketplacePermissions.ClaimType, permission));
            if (identity == "superadmin-with-mfa")
            {
                claims.Add(new(MarketplacePermissions.ClaimType, MarketplacePermissions.RolesManage));
                claims.Add(new(MarketplacePermissions.ClaimType, MarketplacePermissions.ReviewsDecide));
            }
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "role"));
            principal.SetScopes("art-marketplace-api");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
