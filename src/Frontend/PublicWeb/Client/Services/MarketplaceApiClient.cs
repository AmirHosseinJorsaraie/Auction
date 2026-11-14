using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArtMarketplace.PublicWeb.Client.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace ArtMarketplace.PublicWeb.Client.Services;

/// <summary>
/// The single browser-side gateway for public and authenticated HTTP calls.
/// Authenticated mutations obtain an ASP.NET Core antiforgery token automatically.
/// </summary>
public sealed class MarketplaceApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private string? csrfToken;

    public Task<ApiResult<T>> GetPublicAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, $"api/public/{path.TrimStart('/')}", null, false, cancellationToken);

    public Task<ApiResult<T>> GetPrivateAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, $"api/bff/{path.TrimStart('/')}", null, false, cancellationToken);

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(HttpMethod.Post, $"api/bff/{path.TrimStart('/')}", JsonContent.Create(body), true, cancellationToken);

    public Task<ApiResult<TResponse>> PostAsync<TResponse>(
        string path,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(HttpMethod.Post, $"api/bff/{path.TrimStart('/')}", null, true, cancellationToken);

    public Task<ApiResult<object>> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/bff/{path.TrimStart('/')}", null, true, cancellationToken);

    public async Task<ApiResult<object>> PutFileAsync(
        string uploadUrl,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        request.Content = new StreamContent(file.OpenReadStream(25 * 1024 * 1024, cancellationToken));
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        request.Headers.TryAddWithoutValidation("x-amz-meta-expected-length", file.Size.ToString(CultureInfo.InvariantCulture));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return new ApiResult<object>(response.StatusCode, response.IsSuccessStatusCode ? new object() : null,
            response.IsSuccessStatusCode ? null : "ارسال فایل به مخزن موقت ناموفق بود.");
    }

    public static string MediaUrl(Guid? assetId) =>
        assetId is { } id ? $"/api/public/media/{id}" : "/artwork-placeholder.svg";

    public static string Money(decimal amount, string currency)
    {
        try
        {
            return string.Format(CultureInfo.GetCultureInfo("fa-IR"), "{0:C0}", amount)
                .Replace("ریال", currency, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return $"{amount:N0} {currency}";
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        HttpContent? content,
        bool requiresAntiforgery,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (requiresAntiforgery)
        {
            csrfToken ??= await GetAntiforgeryTokenAsync(cancellationToken);
            if (csrfToken is null)
            {
                return new ApiResult<T>(HttpStatusCode.Unauthorized, default, "ابتدا وارد حساب شوید.");
            }

            request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            if (response.Content.Headers.ContentLength == 0)
            {
                return new ApiResult<T>(response.StatusCode, default, null);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return new ApiResult<T>(response.StatusCode, value, null);
        }

        return new ApiResult<T>(response.StatusCode, default, await ReadErrorAsync(response, cancellationToken));
    }

    private async Task<string?> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("auth/session", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var session = await response.Content.ReadFromJsonAsync<SessionView>(JsonOptions, cancellationToken);
        return session?.CsrfToken;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            foreach (var property in new[] { "message", "detail", "title", "error" })
            {
                if (document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON upstream failures are presented as a stable localized message.
        }

        return $"درخواست ناموفق بود (کد {(int)response.StatusCode}).";
    }
}
