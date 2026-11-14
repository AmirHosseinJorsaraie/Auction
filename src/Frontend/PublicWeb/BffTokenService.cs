using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ArtMarketplace.PublicWeb;

/// <summary>
/// Keeps OAuth tokens on the server and refreshes short-lived access tokens before
/// proxying an API request. The Blazor client never receives either OAuth token.
/// </summary>
public sealed class BffTokenService(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<BffTokenService> logger)
{
    private static readonly Action<ILogger, string, Exception?> LogRefreshFailure =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "PublicBffTokenRefreshFailed"),
            "OIDC access-token refresh failed for public BFF session {TraceId}");

    public async Task<string?> GetAccessTokenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var authentication = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null || authentication.Properties is null)
        {
            return null;
        }

        var accessToken = authentication.Properties.GetTokenValue("access_token");
        var expiresAt = ParseExpiration(authentication.Properties.GetTokenValue("expires_at"));
        if (!string.IsNullOrWhiteSpace(accessToken) && expiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return accessToken;
        }

        var refreshToken = authentication.Properties.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var refreshed = await RefreshAsync(refreshToken, cancellationToken);
        if (refreshed is null)
        {
            LogRefreshFailure(logger, context.TraceIdentifier, null);
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return null;
        }

        var tokens = authentication.Properties.GetTokens().ToList();
        SetToken(tokens, "access_token", refreshed.AccessToken);
        SetToken(tokens, "refresh_token", refreshed.RefreshToken ?? refreshToken);
        SetToken(tokens, "expires_at", DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn)
            .ToString("o", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(refreshed.IdToken))
        {
            SetToken(tokens, "id_token", refreshed.IdToken);
        }

        authentication.Properties.StoreTokens(tokens);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authentication.Principal,
            authentication.Properties);
        return refreshed.AccessToken;
    }

    private async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var authority = configuration["Authentication:InternalAuthority"]
            ?? configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication authority is required.");
        var clientId = configuration["Authentication:ClientId"]
            ?? throw new InvalidOperationException("Authentication client ID is required.");
        var clientSecret = configuration["Authentication:ClientSecret"]
            ?? throw new InvalidOperationException("Authentication client secret is required.");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(authority), "connect/token"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        using var response = await clients.CreateClient("OidcBackchannel").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
    }

    private static DateTimeOffset ParseExpiration(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    private static void SetToken(List<AuthenticationToken> tokens, string name, string value)
    {
        var existing = tokens.Find(token => token.Name == name);
        if (existing is null)
        {
            tokens.Add(new AuthenticationToken { Name = name, Value = value });
        }
        else
        {
            existing.Value = value;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
