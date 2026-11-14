namespace ArtMarketplace.PublicWeb;

/// <summary>
/// Rewrites OIDC back-channel traffic to a container-network address while preserving
/// the public issuer and endpoints used by the browser and token validation.
/// </summary>
internal sealed class AuthorityRewriteHandler(
    Uri publicAuthority,
    Uri internalAuthority,
    HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } target && SameAuthority(target, publicAuthority))
        {
            request.RequestUri = new Uri(internalAuthority, target.PathAndQuery.TrimStart('/'));
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static bool SameAuthority(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}
