using ArtMarketplace.Identity.Authorization;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Infrastructure.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Identity.Seeding;

internal sealed partial class IdentitySeedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<IdentitySeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in MarketplaceRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                EnsureSucceeded(result, $"create the '{roleName}' role");
            }
        }

        await EnsurePermissionAsync(roleManager, MarketplaceRoles.Expert, MarketplacePermissions.ReviewsDecide);
        await EnsurePermissionAsync(roleManager, MarketplaceRoles.SuperAdmin, MarketplacePermissions.ReviewsDecide);
        await EnsurePermissionAsync(roleManager, MarketplaceRoles.SuperAdmin, MarketplacePermissions.ReviewsOverride);

        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await EnsureClientAsync(applicationManager, "Public", cancellationToken);
        await EnsureClientAsync(applicationManager, "Backoffice", cancellationToken);
        await EnsureResourceServerAsync(applicationManager, cancellationToken);
        await PromoteExistingSuperAdminAsync(scope.ServiceProvider);
    }

    private async Task EnsureResourceServerAsync(
        IOpenIddictApplicationManager manager,
        CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("OpenIddict:Clients:Api");
        var clientId = Required(section, "ClientId");
        if (await manager.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = Required(section, "ClientSecret"),
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = "Art Marketplace API introspection client"
        };
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
        await manager.CreateAsync(descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsurePermissionAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName, string permission)
    {
        var role = await roleManager.FindByNameAsync(roleName) ?? throw new InvalidOperationException($"Role '{roleName}' was not found.");
        var claims = await roleManager.GetClaimsAsync(role);
        if (!claims.Any(claim => claim.Type == MarketplacePermissions.ClaimType && claim.Value == permission))
        {
            EnsureSucceeded(await roleManager.AddClaimAsync(role, new Claim(MarketplacePermissions.ClaimType, permission)), $"grant '{permission}' to '{roleName}'");
        }
    }

    private async Task EnsureClientAsync(
        IOpenIddictApplicationManager manager,
        string sectionName,
        CancellationToken cancellationToken)
    {
        var section = configuration.GetSection($"OpenIddict:Clients:{sectionName}");
        var clientId = Required(section, "ClientId");
        var clientSecret = Required(section, "ClientSecret");
        var redirectUri = new Uri(Required(section, "RedirectUri"), UriKind.Absolute);
        var postLogoutRedirectUri = new Uri(Required(section, "PostLogoutRedirectUri"), UriKind.Absolute);

        if (await manager.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = $"Art Marketplace {sectionName} BFF"
        };

        descriptor.RedirectUris.Add(redirectUri);
        descriptor.PostLogoutRedirectUris.Add(postLogoutRedirectUri);
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.EndSession,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.Revocation,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles,
            OpenIddictConstants.Permissions.Prefixes.Scope + "art-marketplace-api"
        ]);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        await manager.CreateAsync(descriptor, cancellationToken);
    }

    private async Task PromoteExistingSuperAdminAsync(IServiceProvider services)
    {
        var email = configuration["Bootstrap:SuperAdminEmail"];
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            LogMissingSuperAdmin(logger, email);
            return;
        }

        if (!user.EmailConfirmed || !user.TwoFactorEnabled)
        {
            LogIneligibleSuperAdmin(logger, email);
            return;
        }

        if (!await userManager.IsInRoleAsync(user, MarketplaceRoles.SuperAdmin))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, MarketplaceRoles.SuperAdmin),
                $"promote '{email}' to SuperAdmin");
        }
    }

    private static string Required(IConfigurationSection section, string key) =>
        section[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{section.Path}:{key} is required.");

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "SuperAdmin bootstrap skipped because no existing account has email {Email}. No default account or password was created.")]
    private static partial void LogMissingSuperAdmin(ILogger logger, string email);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "SuperAdmin bootstrap skipped for {Email}; a confirmed email and MFA enrollment are required.")]
    private static partial void LogIneligibleSuperAdmin(ILogger logger, string email);
}
