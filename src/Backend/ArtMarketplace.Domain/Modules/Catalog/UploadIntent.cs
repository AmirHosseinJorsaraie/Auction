namespace ArtMarketplace.Domain.Modules.Catalog;

public enum UploadIntentStatus { PendingUpload = 0, Processing = 1, Completed = 2, Rejected = 3, Expired = 4 }

public sealed class UploadIntent
{
    private UploadIntent() { }
    public UploadIntent(Guid id, Guid userId, Guid artworkId, Guid revisionId, ArtworkAssetKind kind,
        string fileName, string contentType, long expectedSize, string quarantineObjectKey,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty || userId == Guid.Empty || artworkId == Guid.Empty || revisionId == Guid.Empty)
            throw new ArgumentException("Upload identifiers are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAtUtc, createdAtUtc);
        Id=id; UserId=userId; ArtworkId=artworkId; ArtworkRevisionId=revisionId; Kind=kind;
        FileName=fileName; ContentType=contentType; ExpectedSize=expectedSize;
        QuarantineObjectKey=quarantineObjectKey; CreatedAtUtc=createdAtUtc; ExpiresAtUtc=expiresAtUtc;
        Status=UploadIntentStatus.PendingUpload; Version=1;
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ArtworkId { get; private set; }
    public Guid ArtworkRevisionId { get; private set; }
    public ArtworkAssetKind Kind { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ExpectedSize { get; private set; }
    public string QuarantineObjectKey { get; private set; } = string.Empty;
    public UploadIntentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public long Version { get; private set; }
    public void BeginProcessing(Guid userId, DateTimeOffset now)
    {
        if (UserId != userId) throw new InvalidOperationException("Upload intent owner mismatch.");
        if (Status != UploadIntentStatus.PendingUpload) throw new InvalidOperationException("Upload intent has already been consumed.");
        if (ExpiresAtUtc <= now) { Status=UploadIntentStatus.Expired; Version++; throw new InvalidOperationException("Upload intent expired."); }
        Status=UploadIntentStatus.Processing; Version++;
    }
    public void Complete(DateTimeOffset now) { if(Status!=UploadIntentStatus.Processing) throw new InvalidOperationException("Upload is not processing."); Status=UploadIntentStatus.Completed; ConsumedAtUtc=now; Version++; }
    public void Reject(string reason, DateTimeOffset now) { if(Status!=UploadIntentStatus.Processing) throw new InvalidOperationException("Upload is not processing."); Status=UploadIntentStatus.Rejected; FailureReason=reason[..Math.Min(reason.Length,1000)]; ConsumedAtUtc=now; Version++; }
}
