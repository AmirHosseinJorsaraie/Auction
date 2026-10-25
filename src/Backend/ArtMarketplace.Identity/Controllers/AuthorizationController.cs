using System.Security.Claims;
using System.Globalization;
using ArtMarketplace.Identity.Authorization;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Infrastructure.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace ArtMarketplace.Identity.Controllers;

public sealed class AuthorizationController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AuthorizeEndpoint()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is unavailable.");

        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authentication.Succeeded)
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                },
                IdentityConstants.ApplicationScheme);
        }

        var user = await userManager.GetUserAsync(authentication.Principal!);
        if (user is null || !user.IsEnabled || !await signInManager.CanSignInAsync(user))
        {
            await signInManager.SignOutAsync();
            return Challenge(IdentityConstants.ApplicationScheme);
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            var roleResult = await userManager.AddToRoleAsync(user, MarketplaceRoles.User);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException("Unable to assign the baseline user role.");
            }

            roles = await userManager.GetRolesAsync(user);
        }

        if (roles.Any(MarketplaceRoles.RequiresMfa) && !HasMfa(authentication.Principal!))
        {
            return Forbid();
        }

        var principal = await CreatePrincipalAsync(user, request.GetScopes(), authentication.Principal!);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is unavailable.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("Only authorization code and refresh token grants are accepted.");
        }

        var authentication = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var user = await userManager.GetUserAsync(authentication.Principal!);

        if (user is null || !user.IsEnabled || !await signInManager.CanSignInAsync(user))
        {
            return InvalidGrant("The authorization grant is no longer valid.");
        }

        if (await signInManager.ValidateSecurityStampAsync(authentication.Principal!) is null)
        {
            return InvalidGrant("The account security state has changed. Sign in again.");
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Any(MarketplaceRoles.RequiresMfa) && !HasMfa(authentication.Principal!))
        {
            return InvalidGrant("Multi-factor authentication is required for staff access.");
        }

        var principal = await CreatePrincipalAsync(
            user,
            authentication.Principal!.GetScopes(),
            authentication.Principal!);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpPost("~/connect/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutEndpoint()
    {
        await signInManager.SignOutAsync();
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfo()
    {
        var authentication = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var principal = authentication.Principal;
        return Ok(new Dictionary<string, object?>
        {
            [OpenIddictConstants.Claims.Subject] = principal.GetClaim(OpenIddictConstants.Claims.Subject),
            [OpenIddictConstants.Claims.Name] = principal.GetClaim(OpenIddictConstants.Claims.Name),
            [OpenIddictConstants.Claims.Email] = principal.GetClaim(OpenIddictConstants.Claims.Email),
            [OpenIddictConstants.Claims.EmailVerified] = principal.GetClaim(OpenIddictConstants.Claims.EmailVerified),
            [OpenIddictConstants.Claims.Role] = principal.GetClaims(OpenIddictConstants.Claims.Role),
            [MarketplacePermissions.ClaimType] = principal.GetClaims(MarketplacePermissions.ClaimType),
            ["authorization_version"] = principal.GetClaim("authorization_version")
        });
    }

    private async Task<ClaimsPrincipal> CreatePrincipalAsync(
        ApplicationUser user,
        IEnumerable<string> scopes,
        ClaimsPrincipal source)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        identity.SetClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.SetClaim(OpenIddictConstants.Claims.Name, user.UserName ?? user.Email ?? user.Id.ToString());
        identity.SetClaim(OpenIddictConstants.Claims.Email, user.Email);
        identity.SetClaim(OpenIddictConstants.Claims.EmailVerified, user.EmailConfirmed.ToString().ToLowerInvariant());
        identity.SetClaim("authorization_version", user.AuthorizationVersion.ToString(CultureInfo.InvariantCulture));
        identity.SetClaim(
            OpenIddictConstants.Claims.AuthenticationTime,
            source.GetClaim(OpenIddictConstants.Claims.AuthenticationTime)
                ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64);
        identity.SetClaim("acr", HasMfa(source) ? "urn:artmarketplace:loa:2" : "urn:artmarketplace:loa:1");

        foreach (var authenticationMethod in source.FindAll("amr"))
        {
            identity.AddClaim(authenticationMethod);
        }

        principal.SetScopes(scopes);
        principal.SetResources("art-marketplace-api");
        principal.SetDestinations(GetDestinations);
        return principal;
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;

        if (claim.Type is OpenIddictConstants.Claims.Subject
            or OpenIddictConstants.Claims.Name
            or OpenIddictConstants.Claims.Email
            or OpenIddictConstants.Claims.EmailVerified
            or OpenIddictConstants.Claims.Role
            or MarketplacePermissions.ClaimType
            or "amr"
            or "acr"
            or OpenIddictConstants.Claims.AuthenticationTime)
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
    }

    private static bool HasMfa(ClaimsPrincipal principal) =>
        principal.GetClaims("amr")
            .Contains("mfa", StringComparer.Ordinal);

    private ForbidResult InvalidGrant(string description) =>
        Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
