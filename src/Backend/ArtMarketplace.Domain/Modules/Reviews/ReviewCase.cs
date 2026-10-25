namespace ArtMarketplace.Domain.Modules.Reviews;

public enum ReviewCaseStatus { Open = 0, Closed = 1, Withdrawn = 2 }
public enum ReviewRoundStatus { PendingReview = 0, Approved = 1, Rejected = 2, ChangesRequested = 3, Withdrawn = 4 }
public enum ReviewDecisionKind { Approve = 0, Reject = 1, RequestChanges = 2 }
public enum ReviewFindingVisibility { SellerVisible = 0, Private = 1 }
public enum ReviewAnnotationVisibility { SellerVisible = 0, Private = 1 }

public sealed class ReviewCase
{
    private ReviewCase() { }
    public ReviewCase(Guid id, Guid artworkId, Guid submitterUserId, DateTimeOffset now)
    {
        if (id == Guid.Empty || artworkId == Guid.Empty || submitterUserId == Guid.Empty) throw new ArgumentException("Case, artwork, and submitter identifiers are required.");
        Id = id; ArtworkId = artworkId; SubmitterUserId = submitterUserId; Status = ReviewCaseStatus.Open; CreatedAtUtc = now; Version = 1;
    }
    public Guid Id { get; private set; }
    public Guid ArtworkId { get; private set; }
    public Guid SubmitterUserId { get; private set; }
    public ReviewCaseStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public long Version { get; private set; }
    public void Close(DateTimeOffset now)
    {
        if (Status != ReviewCaseStatus.Open) throw new InvalidOperationException("Only an open review case can be closed.");
        Status = ReviewCaseStatus.Closed; ClosedAtUtc = now; Version = checked(Version + 1);
    }
}

public sealed class ReviewRound
{
    private ReviewRound() { }
    public ReviewRound(Guid id, Guid caseId, Guid revisionId, int number, DateTimeOffset now)
    {
        if (id == Guid.Empty || caseId == Guid.Empty || revisionId == Guid.Empty) throw new ArgumentException("Round, case, and revision identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        Id = id; ReviewCaseId = caseId; ArtworkRevisionId = revisionId; Number = number; Status = ReviewRoundStatus.PendingReview; CreatedAtUtc = now; Version = 1;
    }
    public Guid Id { get; private set; }
    public Guid ReviewCaseId { get; private set; }
    public Guid ArtworkRevisionId { get; private set; }
    public int Number { get; private set; }
    public ReviewRoundStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public long Version { get; private set; }
    public void Decide(ReviewDecisionKind kind, long expectedVersion, DateTimeOffset now)
    {
        if (Status != ReviewRoundStatus.PendingReview) throw new InvalidOperationException("The review round is already final.");
        if (Version != expectedVersion) throw new InvalidOperationException("The review changed. Reload before deciding.");
        Status = kind switch { ReviewDecisionKind.Approve => ReviewRoundStatus.Approved, ReviewDecisionKind.Reject => ReviewRoundStatus.Rejected, ReviewDecisionKind.RequestChanges => ReviewRoundStatus.ChangesRequested, _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
        DecidedAtUtc = now; Version = checked(Version + 1);
    }
}

public sealed class ReviewAssignment
{
    private ReviewAssignment() { }
    public ReviewAssignment(Guid id, Guid reviewRoundId, Guid expertUserId, Guid assignedByUserId, DateTimeOffset now)
    {
        if (id == Guid.Empty || reviewRoundId == Guid.Empty || expertUserId == Guid.Empty || assignedByUserId == Guid.Empty) throw new ArgumentException("Assignment identifiers are required.");
        Id = id; ReviewRoundId = reviewRoundId; ExpertUserId = expertUserId; AssignedByUserId = assignedByUserId; AssignedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ReviewRoundId { get; private set; }
    public Guid ExpertUserId { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }
}

public sealed class ReviewFinding
{
    private ReviewFinding() { }
    public ReviewFinding(Guid id, Guid reviewRoundId, Guid authorUserId, string category, string message, string? evidenceReference, ReviewFindingVisibility visibility, DateTimeOffset now)
    {
        if (id == Guid.Empty || reviewRoundId == Guid.Empty || authorUserId == Guid.Empty) throw new ArgumentException("Finding identifiers are required.");
        Id = id; ReviewRoundId = reviewRoundId; AuthorUserId = authorUserId; Category = Text.Required(category, 80); Message = Text.Required(message, 2_000); EvidenceReference = Text.Optional(evidenceReference, 500); Visibility = visibility; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ReviewRoundId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? EvidenceReference { get; private set; }
    public ReviewFindingVisibility Visibility { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class ReviewDecision
{
    private ReviewDecision() { }
    public ReviewDecision(Guid id, Guid reviewCaseId, Guid reviewRoundId, Guid artworkRevisionId, Guid expertUserId, ReviewDecisionKind kind, string reason, string publicComment, string? privateNote, DateTimeOffset now)
    {
        if (id == Guid.Empty || reviewCaseId == Guid.Empty || reviewRoundId == Guid.Empty || artworkRevisionId == Guid.Empty || expertUserId == Guid.Empty) throw new ArgumentException("Decision identifiers are required.");
        Id = id; ReviewCaseId = reviewCaseId; ReviewRoundId = reviewRoundId; ArtworkRevisionId = artworkRevisionId; ExpertUserId = expertUserId; Kind = kind; Reason = Text.Required(reason, 1_000); PublicComment = Text.Required(publicComment, 2_000); PrivateNote = Text.Optional(privateNote, 2_000); CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ReviewCaseId { get; private set; }
    public Guid ReviewRoundId { get; private set; }
    public Guid ArtworkRevisionId { get; private set; }
    public Guid ExpertUserId { get; private set; }
    public ReviewDecisionKind Kind { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string PublicComment { get; private set; } = string.Empty;
    public string? PrivateNote { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class ReviewDecisionSupersession
{
    private ReviewDecisionSupersession() { }
    public ReviewDecisionSupersession(Guid id, Guid decisionId, Guid actorUserId, string reason, DateTimeOffset now)
    {
        if (id == Guid.Empty || decisionId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Supersession identifiers are required.");
        Id = id; ReviewDecisionId = decisionId; ActorUserId = actorUserId; Reason = Text.Required(reason, 1_000); CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ReviewDecisionId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class ReviewAnnotation
{
    private ReviewAnnotation() { }
    public ReviewAnnotation(Guid id, Guid decisionId, Guid actorUserId, string message, ReviewAnnotationVisibility visibility, DateTimeOffset now)
    {
        if (id == Guid.Empty || decisionId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Annotation identifiers are required.");
        Id = id; ReviewDecisionId = decisionId; ActorUserId = actorUserId; Message = Text.Required(message, 2_000); Visibility = visibility; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ReviewDecisionId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public ReviewAnnotationVisibility Visibility { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class ReviewConflict
{
    private ReviewConflict() { }
    public ReviewConflict(Guid id, Guid artworkId, Guid expertUserId, string reason, DateTimeOffset now)
    {
        if (id == Guid.Empty || artworkId == Guid.Empty || expertUserId == Guid.Empty) throw new ArgumentException("Conflict identifiers are required.");
        Id = id; ArtworkId = artworkId; ExpertUserId = expertUserId; Reason = Text.Required(reason, 500); CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ArtworkId { get; private set; }
    public Guid ExpertUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

file static class Text
{
    public static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value must contain 1 to {max} characters.") : value.Trim();
    public static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? throw new ArgumentException($"Value must not exceed {max} characters.") : value.Trim();
}
