using Microsoft.AspNetCore.Antiforgery;

namespace ArtMarketplace.Backoffice;

internal static class ReviewProxyEndpoints
{
    public static IEndpointRouteBuilder MapReviewProxyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/operations/reviews/{caseId:guid}/{roundId:guid}/assign", AssignAsync).RequireAuthorization();
        endpoints.MapPost("/operations/reviews/{caseId:guid}/{roundId:guid}/decision", DecideAsync).RequireAuthorization();
        endpoints.MapPost("/operations/reviews/{caseId:guid}/{roundId:guid}/finding", FindingAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> AssignAsync(Guid caseId, Guid roundId, HttpContext context, IAntiforgery antiforgery, ReviewApiClient api, CancellationToken ct)
    {
        await antiforgery.ValidateRequestAsync(context);
        using var response = await api.SendJsonAsync(HttpMethod.Post, $"api/backoffice/reviews/{caseId}/rounds/{roundId}/assign", new { }, ct);
        return Redirect(response, "/reviews");
    }

    private static async Task<IResult> DecideAsync(Guid caseId, Guid roundId, HttpContext context, IAntiforgery antiforgery, ReviewApiClient api, CancellationToken ct)
    {
        await antiforgery.ValidateRequestAsync(context); var form = await context.Request.ReadFormAsync(ct);
        _ = long.TryParse(form["expectedVersion"], out var expectedVersion);
        using var response = await api.SendJsonAsync(HttpMethod.Post, $"api/backoffice/reviews/{caseId}/rounds/{roundId}/decision", new
        {
            decision = form["decision"].ToString(), expectedVersion, reason = form["reason"].ToString(),
            publicComment = form["publicComment"].ToString(), privateNote = form["privateNote"].ToString()
        }, ct);
        return Redirect(response, "/reviews");
    }

    private static async Task<IResult> FindingAsync(Guid caseId, Guid roundId, HttpContext context, IAntiforgery antiforgery, ReviewApiClient api, CancellationToken ct)
    {
        await antiforgery.ValidateRequestAsync(context); var form = await context.Request.ReadFormAsync(ct);
        using var response = await api.SendJsonAsync(HttpMethod.Post, $"api/backoffice/reviews/{caseId}/rounds/{roundId}/findings", new
        {
            category = form["category"].ToString(), message = form["message"].ToString(), evidenceReference = form["evidenceReference"].ToString(), sellerVisible = form["sellerVisible"] == "true"
        }, ct);
        return Redirect(response, "/reviews");
    }

    private static IResult Redirect(HttpResponseMessage response, string successPath) => response.IsSuccessStatusCode
        ? Results.Redirect(successPath)
        : Results.Redirect($"{successPath}?error={Uri.EscapeDataString($"API returned {(int)response.StatusCode}")}");
}
