using Microsoft.AspNetCore.Antiforgery;

namespace ArtMarketplace.Backoffice;

internal static class AdminProxyEndpoints
{
    public static void MapAdminProxyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/operations/users/{userId:guid}/status", UserStatusAsync).RequireAuthorization();
        endpoints.MapPost("/operations/users/{userId:guid}/role", RoleAsync).RequireAuthorization();
        endpoints.MapPost("/operations/categories", CategoryAsync).RequireAuthorization();
        endpoints.MapPost("/operations/content/pages", ContentPageAsync).RequireAuthorization();
        endpoints.MapPost("/operations/listings/{listingId:guid}/suspend", SuspendListingAsync).RequireAuthorization();
    }
    private static async Task<IResult> UserStatusAsync(Guid userId, HttpContext context, IAntiforgery antiforgery, AdminApiClient api, CancellationToken ct)
    { await antiforgery.ValidateRequestAsync(context); var form = await context.Request.ReadFormAsync(ct); using var response = await api.SendJsonAsync(HttpMethod.Post, $"api/backoffice/users/{userId}/status", new { enabled = form["enabled"] == "true", reason = form["reason"].ToString() }, ct); return Redirect(response, "/users"); }
    private static async Task<IResult> RoleAsync(Guid userId, HttpContext context, IAntiforgery antiforgery, AdminApiClient api, CancellationToken ct)
    { await antiforgery.ValidateRequestAsync(context); var form = await context.Request.ReadFormAsync(ct); var role = form["role"].ToString(); using var response = await api.SendJsonAsync(HttpMethod.Put, $"api/backoffice/users/{userId}/roles/{Uri.EscapeDataString(role)}", new { assign = form["assign"] == "true", reason = form["reason"].ToString() }, ct); return Redirect(response, "/users"); }
    private static async Task<IResult> CategoryAsync(HttpContext context, IAntiforgery antiforgery, AdminApiClient api, CancellationToken ct)
    { await antiforgery.ValidateRequestAsync(context); var form = await context.Request.ReadFormAsync(ct); _ = int.TryParse(form["displayOrder"], out var order); using var response = await api.SendJsonAsync(HttpMethod.Post, "api/backoffice/categories", new { name = form["name"].ToString(), slug = form["slug"].ToString(), displayOrder = order, isActive = true }, ct); return Redirect(response, "/content"); }
    private static async Task<IResult> ContentPageAsync(HttpContext context, IAntiforgery antiforgery, AdminApiClient api, CancellationToken ct)
    { await antiforgery.ValidateRequestAsync(context); var form = await context.Request.ReadFormAsync(ct); using var response = await api.SendJsonAsync(HttpMethod.Post, "api/backoffice/content/pages", new { slug = form["slug"].ToString(), title = form["title"].ToString(), body = form["body"].ToString(), isPublished = form["isPublished"] == "true", expectedVersion = 0 }, ct); return Redirect(response, "/content"); }
    private static async Task<IResult> SuspendListingAsync(Guid listingId, HttpContext context, IAntiforgery antiforgery, AdminApiClient api, CancellationToken ct)
    { await antiforgery.ValidateRequestAsync(context); var form=await context.Request.ReadFormAsync(ct); using var response=await api.SendJsonAsync(HttpMethod.Post,$"api/backoffice/administration/listings/{listingId}/suspend",new{reason=form["reason"].ToString()},ct);return Redirect(response,"/operations"); }
    private static IResult Redirect(HttpResponseMessage response, string path) => response.IsSuccessStatusCode ? Results.Redirect($"{path}?notice=saved") : Results.Redirect($"{path}?error={Uri.EscapeDataString($"API returned {(int)response.StatusCode}")}");
}
