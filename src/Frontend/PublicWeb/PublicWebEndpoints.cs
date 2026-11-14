using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace ArtMarketplace.PublicWeb;

internal static class PublicWebEndpoints
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS"];

    public static IEndpointRouteBuilder MapPublicWebEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", Login).AllowAnonymous();
        endpoints.MapGet("/auth/session", Session).RequireAuthorization();
        endpoints.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();

        endpoints.MapMethods("/api/public/{**path}", ["GET", "HEAD"], ProxyPublicAsync).AllowAnonymous();
        endpoints.MapMethods("/api/bff/{**path}", ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE"], ProxyPrivateAsync)
            .RequireAuthorization();
        endpoints.MapGet("/api/health", () => Results.Ok(new
        {
            service = "ArtMarketplace.PublicWeb",
            status = "ok",
            timestamp = DateTimeOffset.UtcNow
        })).AllowAnonymous();
        endpoints.MapGet("/sitemap.xml", Sitemap).AllowAnonymous();
        return endpoints;
    }

    private static IResult Login(HttpContext context, string? returnUrl)
    {
        // Reject cross-site login initiation so another origin cannot force an authentication flow.
        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0 && fetchSite is not "same-origin" and not "none")
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = SafeLocalReturnUrl(returnUrl) },
            [OpenIdConnectDefaults.AuthenticationScheme]);
    }

    private static IResult Session(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new
        {
            authenticated = true,
            displayName = context.User.Identity?.Name,
            csrfToken = tokens.RequestToken
        });
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAntiforgery antiforgery)
    {
        await antiforgery.ValidateRequestAsync(context);
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
    }

    private static async Task ProxyPublicAsync(
        HttpContext context,
        string? path,
        IHttpClientFactory clients,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedPublicPath(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var request = CreateUpstreamRequest(context, path!, accessToken: null);
        using var response = await clients.CreateClient("Api").SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        context.Response.Headers.CacheControl = path!.StartsWith("media/", StringComparison.OrdinalIgnoreCase)
            ? "public, max-age=300"
            : "no-store";
        await CopyResponseAsync(context, response, cancellationToken);
    }

    private static async Task ProxyPrivateAsync(
        HttpContext context,
        string? path,
        IAntiforgery antiforgery,
        BffTokenService tokens,
        IHttpClientFactory clients,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!SafeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            // ASP.NET Core validates both the antiforgery cookie and the request header.
            await antiforgery.ValidateRequestAsync(context);
        }

        var accessToken = await tokens.GetAccessTokenAsync(context, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var request = CreateUpstreamRequest(context, path, accessToken);
        using var response = await clients.CreateClient("Api").SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        context.Response.Headers.CacheControl = "no-store, max-age=0";
        await CopyResponseAsync(context, response, cancellationToken);
    }

    private static HttpRequestMessage CreateUpstreamRequest(HttpContext context, string path, string? accessToken)
    {
        var escapedPath = string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            $"api/{escapedPath}{context.Request.QueryString}");

        request.Headers.Accept.ParseAdd(context.Request.Headers.Accept.ToString() is { Length: > 0 } accept
            ? accept
            : "application/json");
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (context.Request.Headers.TryGetValue("traceparent", out var traceParent))
        {
            request.Headers.TryAddWithoutValidation("traceparent", traceParent.ToArray());
        }

        if (!SafeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }

        return request;
    }

    private static async Task CopyResponseAsync(
        HttpContext context,
        HttpResponseMessage upstream,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)upstream.StatusCode;
        if (upstream.Content.Headers.ContentType is { } contentType)
        {
            context.Response.ContentType = contentType.ToString();
        }

        await upstream.Content.CopyToAsync(context.Response.Body, cancellationToken);
    }

    private static bool IsAllowedPublicPath(string? path) => path is not null
        && (path.Equals("gallery", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("gallery/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("public/content/banner", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("public/content/pages/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("media/", StringComparison.OrdinalIgnoreCase));

    private static string SafeLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && Uri.TryCreate(returnUrl, UriKind.Relative, out _)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/";

    private static IResult Sitemap(IConfiguration configuration)
    {
        var site = (configuration["Site:BaseUrl"] ?? "https://marketplace.localhost").TrimEnd('/');
        var urls = new[] { "", "/gallery", "/artists", "/about", "/contact" };
        var body = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var path in urls)
        {
            body.Append("<url><loc>").Append(System.Net.WebUtility.HtmlEncode(site + path)).Append("</loc></url>");
        }

        body.Append("</urlset>");
        return Results.Text(body.ToString(), "application/xml", Encoding.UTF8);
    }
}
