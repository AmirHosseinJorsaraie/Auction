using System.Security.Claims;
using System.Text.Json;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Api;

internal static class Phase4Endpoints
{
    public static IEndpointRouteBuilder MapPhase4Endpoints(this IEndpointRouteBuilder endpoints)
    {
        var reviews = endpoints.MapGroup("/api/backoffice/reviews")
            .RequireAuthorization(MarketplacePolicies.ReviewsDecide)
            .RequireRateLimiting("baseline")
            .WithTags("Backoffice reviews");

        reviews.MapGet("/queue", GetQueueAsync);
        reviews.MapGet("/{caseId:guid}", GetDetailAsync);
        reviews.MapPost("/{caseId:guid}/rounds/{roundId:guid}/assign", AssignAsync);
        reviews.MapPost("/{caseId:guid}/rounds/{roundId:guid}/findings", AddFindingAsync);
        reviews.MapPost("/{caseId:guid}/rounds/{roundId:guid}/decision", DecideAsync);
        reviews.MapPost("/decisions/{decisionId:guid}/annotations", AddAnnotationAsync);

        endpoints.MapPost("/api/backoffice/reviews/decisions/{decisionId:guid}/supersede", SupersedeAsync)
            .RequireAuthorization(MarketplacePolicies.ReviewsOverride)
            .RequireRateLimiting("baseline")
            .WithTags("Backoffice reviews");

        endpoints.MapGet("/api/artworks/{artworkId:guid}/review-result", GetSellerResultAsync)
            .RequireAuthorization()
            .RequireRateLimiting("baseline")
            .WithTags("Artwork reviews");
        return endpoints;
    }

    private static async Task<IResult> GetQueueAsync(ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out var userId)) return Results.Unauthorized();
        var queue = await (
            from reviewCase in db.ReviewCases.AsNoTracking()
            join round in db.ReviewRounds.AsNoTracking() on reviewCase.Id equals round.ReviewCaseId
            join revision in db.ArtworkRevisions.AsNoTracking() on round.ArtworkRevisionId equals revision.Id
            join assignment in db.ReviewAssignments.AsNoTracking() on round.Id equals assignment.ReviewRoundId into assignments
            from assignment in assignments.DefaultIfEmpty()
            where reviewCase.Status == ReviewCaseStatus.Open && round.Status == ReviewRoundStatus.PendingReview
            orderby round.CreatedAtUtc
            select new
            {
                reviewCase.Id, reviewCase.ArtworkId, roundId = round.Id, revisionId = revision.Id,
                revision.Title, round.Number, round.Version, round.CreatedAtUtc,
                assignedExpertUserId = assignment == null ? (Guid?)null : assignment.ExpertUserId,
                canAct = assignment == null || assignment.ExpertUserId == userId
            }).Take(100).ToListAsync(ct);
        return Results.Ok(queue);
    }

    private static async Task<IResult> GetDetailAsync(Guid caseId, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out _)) return Results.Unauthorized();
        var header = await (
            from reviewCase in db.ReviewCases.AsNoTracking()
            join artwork in db.Artworks.AsNoTracking() on reviewCase.ArtworkId equals artwork.Id
            where reviewCase.Id == caseId
            select new { reviewCase.Id, reviewCase.ArtworkId, reviewCase.SubmitterUserId, reviewCase.Status, artwork.CurrentOwnerUserId }).SingleOrDefaultAsync(ct);
        if (header is null) return Results.NotFound();
        var rounds = await db.ReviewRounds.AsNoTracking().Where(x => x.ReviewCaseId == caseId).OrderBy(x => x.Number).ToListAsync(ct);
        var roundIds = rounds.Select(x => x.Id).ToArray();
        var revisions = await db.ArtworkRevisions.AsNoTracking().Where(x => rounds.Select(r => r.ArtworkRevisionId).Contains(x.Id)).ToListAsync(ct);
        var assignments = await db.ReviewAssignments.AsNoTracking().Where(x => roundIds.Contains(x.ReviewRoundId)).ToListAsync(ct);
        var findings = await db.ReviewFindings.AsNoTracking().Where(x => roundIds.Contains(x.ReviewRoundId)).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var decisions = await db.ReviewDecisions.AsNoTracking().Where(x => x.ReviewCaseId == caseId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var decisionIds = decisions.Select(x => x.Id).ToArray();
        var supersessions = await db.ReviewDecisionSupersessions.AsNoTracking().Where(x => decisionIds.Contains(x.ReviewDecisionId)).ToListAsync(ct);
        return Results.Ok(new { header, rounds, revisions, assignments, findings, decisions, supersessions });
    }

    private static async Task<IResult> AssignAsync(Guid caseId, Guid roundId, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var expertId)) return Results.Unauthorized();
        var artworkId = await db.ReviewCases.AsNoTracking().Where(x => x.Id == caseId).Select(x => (Guid?)x.ArtworkId).SingleOrDefaultAsync(ct);
        if (artworkId is null) return Results.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var artwork = await LockArtwork(db, artworkId.Value, ct);
        var reviewCase = await LockCase(db, caseId, ct); var round = await LockRound(db, roundId, ct);
        if (artwork is null || reviewCase is null || round is null || round.ReviewCaseId != caseId) return Results.NotFound();
        if (reviewCase.Status != ReviewCaseStatus.Open || round.Status != ReviewRoundStatus.PendingReview) return Conflict("Only a pending round can be assigned.");
        if (reviewCase.SubmitterUserId == expertId || artwork.CurrentOwnerUserId == expertId) return Results.Forbid();
        if (await db.ReviewConflicts.AnyAsync(x => x.ArtworkId == reviewCase.ArtworkId && x.ExpertUserId == expertId, ct)) return Results.Forbid();
        var existing = await db.ReviewAssignments.SingleOrDefaultAsync(x => x.ReviewRoundId == roundId, ct);
        if (existing is not null) return existing.ExpertUserId == expertId ? Results.Ok(new { existing.Id }) : Conflict("This review is assigned to another expert.");
        var assignment = new ReviewAssignment(Guid.NewGuid(), roundId, expertId, expertId, time.GetUtcNow());
        db.ReviewAssignments.Add(assignment); AddAudit(db, "review.assigned", expertId, reviewCase.ArtworkId, time.GetUtcNow(), new { caseId, roundId });
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { assignment.Id }); }
        catch (DbUpdateException) { return Conflict("A concurrent assignment already won."); }
    }

    private static async Task<IResult> AddFindingAsync(Guid caseId, Guid roundId, FindingRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var expertId)) return Results.Unauthorized();
        var context = await AssignedContext(db, caseId, roundId, expertId, ct); if (context is null) return Results.Forbid();
        try
        {
            var finding = new ReviewFinding(Guid.NewGuid(), roundId, expertId, request.Category, request.Message, request.EvidenceReference, request.SellerVisible ? ReviewFindingVisibility.SellerVisible : ReviewFindingVisibility.Private, time.GetUtcNow());
            db.ReviewFindings.Add(finding); AddAudit(db, "review.finding-added", expertId, context.Value.ArtworkId, time.GetUtcNow(), new { caseId, roundId, findingId = finding.Id });
            await db.SaveChangesAsync(ct); return Results.Created($"/api/backoffice/reviews/{caseId}", new { finding.Id });
        }
        catch (ArgumentException ex) { return Validation(ex.Message); }
    }

    private static async Task<IResult> DecideAsync(Guid caseId, Guid roundId, DecisionRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var expertId)) return Results.Unauthorized();
        if (!Enum.TryParse<ReviewDecisionKind>(request.Decision, true, out var kind)) return Validation("Decision must be Approve, Reject, or RequestChanges.");
        var artworkId = await db.ReviewCases.AsNoTracking().Where(x => x.Id == caseId).Select(x => (Guid?)x.ArtworkId).SingleOrDefaultAsync(ct);
        if (artworkId is null) return Results.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var artwork = await LockArtwork(db, artworkId.Value, ct);
        var reviewCase = await LockCase(db, caseId, ct); var round = await LockRound(db, roundId, ct);
        if (artwork is null || reviewCase is null || round is null || round.ReviewCaseId != caseId) return Results.NotFound();
        var assignment = await db.ReviewAssignments.SingleOrDefaultAsync(x => x.ReviewRoundId == roundId && x.ExpertUserId == expertId, ct);
        if (assignment is null || reviewCase.SubmitterUserId == expertId || artwork.CurrentOwnerUserId == expertId) return Results.Forbid();
        if (await db.ReviewConflicts.AnyAsync(x => x.ArtworkId == reviewCase.ArtworkId && x.ExpertUserId == expertId, ct)) return Results.Forbid();
        try
        {
            round.Decide(kind, request.ExpectedVersion, time.GetUtcNow());
            var decision = new ReviewDecision(Guid.NewGuid(), caseId, roundId, round.ArtworkRevisionId, expertId, kind, request.Reason, request.PublicComment, request.PrivateNote, time.GetUtcNow());
            db.ReviewDecisions.Add(decision);
            if (kind is ReviewDecisionKind.Approve or ReviewDecisionKind.Reject) reviewCase.Close(time.GetUtcNow());
            AddAudit(db, "review.decided", expertId, reviewCase.ArtworkId, time.GetUtcNow(), new { caseId, roundId, decisionId = decision.Id, kind });
            AddOutbox(db, "ArtworkReviewDecided", reviewCase.ArtworkId, new { reviewCase.SubmitterUserId, caseId, roundId, decisionId = decision.Id, kind }, time.GetUtcNow());
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(new { decision.Id, decision.Kind, round.Version });
        }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (ArgumentException ex) { return Validation(ex.Message); }
        catch (DbUpdateConcurrencyException) { return Conflict("A concurrent decision already won. Reload the review."); }
        catch (DbUpdateException) { return Conflict("A concurrent decision already won. Reload the review."); }
    }

    private static async Task<IResult> SupersedeAsync(Guid decisionId, SupersedeRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var actorId)) return Results.Unauthorized();
        var location = await (from item in db.ReviewDecisions.AsNoTracking() join parent in db.ReviewCases.AsNoTracking() on item.ReviewCaseId equals parent.Id where item.Id == decisionId select new { parent.ArtworkId, item.ReviewCaseId }).SingleOrDefaultAsync(ct);
        if (location is null) return Results.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        _ = await LockArtwork(db, location.ArtworkId, ct) ?? throw new InvalidOperationException("Artwork is missing.");
        var reviewCase = await LockCase(db, location.ReviewCaseId, ct) ?? throw new InvalidOperationException("Review case is missing.");
        var decision = await db.ReviewDecisions.FromSqlInterpolated($"SELECT * FROM reviews.review_decisions WHERE \"Id\" = {decisionId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (decision is null) return Results.NotFound();
        if (await db.ReviewDecisionSupersessions.AnyAsync(x => x.ReviewDecisionId == decisionId, ct)) return Conflict("This decision is already superseded.");
        try
        {
            var supersession = new ReviewDecisionSupersession(Guid.NewGuid(), decisionId, actorId, request.Reason, time.GetUtcNow());
            db.ReviewDecisionSupersessions.Add(supersession);
            var publishedListing = await db.MarketplaceListings.FromSqlInterpolated($"SELECT * FROM marketplace.marketplace_listings WHERE \"ArtworkId\" = {reviewCase.ArtworkId} AND \"Status\" = 'Published' FOR UPDATE").SingleOrDefaultAsync(ct);
            publishedListing?.Suspend("The effective review approval was superseded.", time.GetUtcNow());
            var activeCase = await db.ReviewCases.SingleOrDefaultAsync(x => x.ArtworkId == reviewCase.ArtworkId && x.Status == ReviewCaseStatus.Open, ct);
            if (activeCase is null)
            {
                activeCase = new ReviewCase(Guid.NewGuid(), reviewCase.ArtworkId, reviewCase.SubmitterUserId, time.GetUtcNow()); db.ReviewCases.Add(activeCase);
                db.ReviewRounds.Add(new ReviewRound(Guid.NewGuid(), activeCase.Id, decision.ArtworkRevisionId, 1, time.GetUtcNow()));
            }
            AddAudit(db, "review.decision-superseded", actorId, reviewCase.ArtworkId, time.GetUtcNow(), new { decisionId, supersessionId = supersession.Id, safeguardCaseId = activeCase.Id });
            AddOutbox(db, "ArtworkReviewSuperseded", reviewCase.ArtworkId, new { decisionId, reviewCase.SubmitterUserId, safeguardCaseId = activeCase.Id }, time.GetUtcNow());
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { supersession.Id, safeguardCaseId = activeCase.Id });
        }
        catch (ArgumentException ex) { return Validation(ex.Message); }
        catch (DbUpdateException) { return Conflict("A concurrent supersession or submission won. Reload the review."); }
    }

    private static async Task<IResult> AddAnnotationAsync(Guid decisionId, AnnotationRequest request, ClaimsPrincipal principal, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!UserId(principal, out var actorId)) return Results.Unauthorized();
        var decision = await db.ReviewDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == decisionId, ct);
        if (decision is null) return Results.NotFound();
        var canOverride = principal.HasClaim(MarketplacePermissions.ClaimType, MarketplacePermissions.ReviewsOverride);
        if (decision.ExpertUserId != actorId && !canOverride) return Results.Forbid();
        try
        {
            var annotation = new ReviewAnnotation(Guid.NewGuid(), decisionId, actorId, request.Message, request.SellerVisible ? ReviewAnnotationVisibility.SellerVisible : ReviewAnnotationVisibility.Private, time.GetUtcNow());
            db.ReviewAnnotations.Add(annotation); AddAudit(db, "review.annotation-added", actorId, (await db.ReviewCases.AsNoTracking().SingleAsync(x => x.Id == decision.ReviewCaseId, ct)).ArtworkId, time.GetUtcNow(), new { decisionId, annotationId = annotation.Id });
            await db.SaveChangesAsync(ct); return Results.Created($"/api/backoffice/reviews/decisions/{decisionId}/annotations", new { annotation.Id });
        }
        catch (ArgumentException ex) { return Validation(ex.Message); }
    }

    private static async Task<IResult> GetSellerResultAsync(Guid artworkId, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if (!UserId(principal, out var sellerId)) return Results.Unauthorized();
        if (!await db.Artworks.AsNoTracking().AnyAsync(x => x.Id == artworkId && x.CurrentOwnerUserId == sellerId, ct)) return Results.NotFound();
        var cases = await db.ReviewCases.AsNoTracking().Where(x => x.ArtworkId == artworkId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var caseIds = cases.Select(x => x.Id).ToArray();
        var rounds = await db.ReviewRounds.AsNoTracking().Where(x => caseIds.Contains(x.ReviewCaseId)).OrderBy(x => x.Number).ToListAsync(ct);
        var roundIds = rounds.Select(x => x.Id).ToArray();
        var findings = await db.ReviewFindings.AsNoTracking().Where(x => roundIds.Contains(x.ReviewRoundId) && x.Visibility == ReviewFindingVisibility.SellerVisible).Select(x => new { x.Id, x.ReviewRoundId, x.Category, x.Message, x.EvidenceReference, x.CreatedAtUtc }).ToListAsync(ct);
        var decisions = await db.ReviewDecisions.AsNoTracking().Where(x => caseIds.Contains(x.ReviewCaseId)).Select(x => new { x.Id, x.ReviewCaseId, x.ReviewRoundId, x.ArtworkRevisionId, x.Kind, x.Reason, x.PublicComment, x.CreatedAtUtc }).ToListAsync(ct);
        var decisionIds = decisions.Select(x => x.Id).ToArray();
        var supersededIds = await db.ReviewDecisionSupersessions.AsNoTracking().Where(x => decisionIds.Contains(x.ReviewDecisionId)).Select(x => x.ReviewDecisionId).ToListAsync(ct);
        var annotations = await db.ReviewAnnotations.AsNoTracking().Where(x => decisionIds.Contains(x.ReviewDecisionId) && x.Visibility == ReviewAnnotationVisibility.SellerVisible).Select(x => new { x.Id, x.ReviewDecisionId, x.Message, x.CreatedAtUtc }).ToListAsync(ct);
        return Results.Ok(new { cases, rounds, findings, decisions, annotations, supersededDecisionIds = supersededIds });
    }

    private static async Task<(Guid ArtworkId, Guid RevisionId)?> AssignedContext(MarketplaceDbContext db, Guid caseId, Guid roundId, Guid expertId, CancellationToken ct)
    {
        var value = await (from c in db.ReviewCases.AsNoTracking()
                           join r in db.ReviewRounds.AsNoTracking() on c.Id equals r.ReviewCaseId
                           join a in db.ReviewAssignments.AsNoTracking() on r.Id equals a.ReviewRoundId
                           where c.Id == caseId && r.Id == roundId && c.Status == ReviewCaseStatus.Open
                                 && r.Status == ReviewRoundStatus.PendingReview && a.ExpertUserId == expertId
                           select new { c.ArtworkId, RevisionId = r.ArtworkRevisionId }).SingleOrDefaultAsync(ct);
        return value is null ? null : (value.ArtworkId, value.RevisionId);
    }
    private static Task<ReviewCase?> LockCase(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.ReviewCases.FromSqlInterpolated($"SELECT * FROM reviews.review_cases WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<ReviewRound?> LockRound(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.ReviewRounds.FromSqlInterpolated($"SELECT * FROM reviews.review_rounds WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<Artwork?> LockArtwork(MarketplaceDbContext db, Guid id, CancellationToken ct) => db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static bool UserId(ClaimsPrincipal principal, out Guid id) => Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out id);
    private static IResult Validation(string message) => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });
    private static IResult Conflict(string message) => Results.Conflict(new { message });
    private static void AddAudit(MarketplaceDbContext db, string type, Guid actor, Guid artworkId, DateTimeOffset now, object data) => db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(), type, actor, actor, JsonSerializer.Serialize(new { artworkId, data }), null, now));
    private static void AddOutbox(MarketplaceDbContext db, string type, Guid aggregateId, object payload, DateTimeOffset now) => db.OutboxMessages.Add(new OutboxMessage { Id = Guid.NewGuid(), Type = type, AggregateId = aggregateId, PayloadJson = JsonSerializer.Serialize(payload), OccurredAtUtc = now });

    internal sealed record FindingRequest(string Category, string Message, string? EvidenceReference, bool SellerVisible);
    internal sealed record DecisionRequest(string Decision, long ExpectedVersion, string Reason, string PublicComment, string? PrivateNote);
    internal sealed record SupersedeRequest(string Reason);
    internal sealed record AnnotationRequest(string Message, bool SellerVisible);
}
