using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;

namespace ArtMarketplace.Backoffice;

public sealed class ReviewApiClient(IHttpClientFactory clients, IHttpContextAccessor accessor)
{
    public async Task<IReadOnlyList<ReviewQueueItem>> GetQueueAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAsync(HttpMethod.Get, "api/backoffice/reviews/queue");
        using var response = await clients.CreateClient("Api").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ReviewQueueItem>>(cancellationToken) ?? [];
    }

    public async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = await CreateAsync(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await clients.CreateClient("Api").SendAsync(request, cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateAsync(HttpMethod method, string path)
    {
        var context = accessor.HttpContext ?? throw new InvalidOperationException("The authenticated HTTP context is unavailable.");
        var token = await context.GetTokenAsync("access_token") ?? throw new InvalidOperationException("The staff access token is unavailable. Sign in again.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

public sealed record ReviewQueueItem(Guid Id, Guid ArtworkId, Guid RoundId, Guid RevisionId, string Title,
    int Number, long Version, DateTimeOffset CreatedAtUtc, Guid? AssignedExpertUserId, bool CanAct);
