using System.Net;

namespace ArtMarketplace.PublicWeb.Client.Services;

public sealed record ApiResult<T>(HttpStatusCode StatusCode, T? Value, string? Error)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
}

public enum LoadState
{
    Loading,
    Ready,
    Unauthenticated,
    Error
}
