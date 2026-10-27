using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using System.Text.Json;

namespace ArtMarketplace.Backoffice;

public sealed class AdminApiClient(IHttpClientFactory clients, IHttpContextAccessor accessor)
{
    public Task<DashboardData?> DashboardAsync(CancellationToken ct) => GetAsync<DashboardData>("api/backoffice/administration/dashboard", ct);
    public Task<PagedResponse<UserRow>?> UsersAsync(int page, string? search, CancellationToken ct) => GetAsync<PagedResponse<UserRow>>($"api/backoffice/users?page={page}&pageSize=25&search={Uri.EscapeDataString(search ?? string.Empty)}", ct);
    public Task<PagedResponse<CatalogRow>?> CatalogAsync(int page, CancellationToken ct) => GetAsync<PagedResponse<CatalogRow>>($"api/backoffice/administration/catalog?page={page}&pageSize=25", ct);
    public Task<PagedResponse<OrderMonitorRow>?> OrdersAsync(int page, CancellationToken ct) => GetAsync<PagedResponse<OrderMonitorRow>>($"api/backoffice/administration/orders?page={page}&pageSize=25", ct);
    public Task<PagedResponse<PaymentMonitorRow>?> PaymentsAsync(int page, CancellationToken ct) => GetAsync<PagedResponse<PaymentMonitorRow>>($"api/backoffice/administration/payments?page={page}&pageSize=25", ct);
    public Task<PagedResponse<AuditRow>?> AuditAsync(int page, CancellationToken ct) => GetAsync<PagedResponse<AuditRow>>($"api/backoffice/audit?page={page}&pageSize=25", ct);
    public Task<IReadOnlyList<ReconciliationRow>?> ReconciliationsAsync(CancellationToken ct) => GetAsync<IReadOnlyList<ReconciliationRow>>("api/backoffice/payments/reconciliation", ct);
    public Task<IReadOnlyList<CategoryRow>?> CategoriesAsync(CancellationToken ct) => GetAsync<IReadOnlyList<CategoryRow>>("api/backoffice/categories", ct);
    public Task<IReadOnlyList<ContentPageRow>?> ContentPagesAsync(CancellationToken ct) => GetAsync<IReadOnlyList<ContentPageRow>>("api/backoffice/content/pages", ct);
    public Task<ConfigurationView?> ConfigurationAsync(CancellationToken ct) => GetAsync<ConfigurationView>("api/backoffice/configuration", ct);
    public async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object body, CancellationToken ct) { using var request = await CreateAsync(method, path); request.Content = JsonContent.Create(body); return await clients.CreateClient("Api").SendAsync(request, ct); }
    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) { using var request = await CreateAsync(HttpMethod.Get, path); using var response = await clients.CreateClient("Api").SendAsync(request, ct); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<T>(ct); }
    private async Task<HttpRequestMessage> CreateAsync(HttpMethod method, string path) { var context = accessor.HttpContext ?? throw new InvalidOperationException("The authenticated HTTP context is unavailable."); var token = await context.GetTokenAsync("access_token") ?? throw new InvalidOperationException("Sign in again."); var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return request; }
}

public sealed record DashboardData(int Users, int EnabledUsers, int PendingVerifications, int OpenReviews, int PublishedListings, int ReservedListings, int AwaitingPaymentOrders, int PaidOrders, int OpenReconciliations, int EventsToday);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public sealed record UserRow(Guid Id, string? Email, string? DisplayName, bool IsEnabled, bool EmailConfirmed, bool TwoFactorEnabled, long AuthorizationVersion, string?[] Roles);
public sealed record CatalogRow(Guid Id, Guid ArtworkId, Guid SellerUserId, decimal PriceAmount, string Currency, string Status, DateTimeOffset UpdatedAtUtc, long Version);
public sealed record OrderMonitorRow(Guid Id, decimal TotalAmount, string Currency, string Status, bool ReviewIntegrityHold, bool SellerEligibilityHold, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record PaymentMonitorRow(Guid Id, Guid OrderId, string ProviderKey, decimal Amount, string Currency, string Status, string? FailureCode, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record AuditRow(Guid Id, string EventType, Guid ActorUserId, Guid SubjectUserId, string? TraceId, DateTimeOffset CreatedAtUtc);
public sealed record ReconciliationRow(Guid Id, Guid OrderId, Guid VerifiedPaymentReceiptId, string ReasonCode, string Details, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? ResolvedAtUtc);
public sealed record CategoryRow(Guid Id, string Name, string Slug, bool IsActive, int DisplayOrder);
public sealed record ContentPageRow(Guid Id, string Slug, string Title, string Body, bool IsPublished, DateTimeOffset UpdatedAtUtc, long Version);
public sealed record ConfigurationView(string Environment, JsonElement Orders, JsonElement Payments, JsonElement Storage, JsonElement Authentication);
