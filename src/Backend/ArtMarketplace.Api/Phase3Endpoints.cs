using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ArtMarketplace.Application.Authorization;
using ArtMarketplace.Application.Storage;
using ArtMarketplace.Domain.Common;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Infrastructure.Persistence;
using ArtMarketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace ArtMarketplace.Api;

internal static class Phase3Endpoints
{
    private const long MaxImageBytes = 20 * 1024 * 1024;
    private const long MaxDocumentBytes = 25 * 1024 * 1024;

    public static IEndpointRouteBuilder MapPhase3Endpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/categories", async (MarketplaceDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder)
                .Select(x => new { x.Id, x.Name, x.Slug }).ToListAsync(ct)))
            .RequireRateLimiting("baseline").WithTags("Catalog");
        endpoints.MapGet("/api/media/{assetId:guid}", PublicMediaAsync)
            .RequireRateLimiting("baseline").WithTags("Catalog media");

        endpoints.MapPost("/api/backoffice/categories", CreateCategoryAsync)
            .RequireAuthorization(MarketplacePolicies.SuperAdminWithMfa).RequireRateLimiting("baseline").WithTags("Catalog");

        var artworks = endpoints.MapGroup("/api/artworks").RequireAuthorization().WithTags("Artwork intake");
        artworks.MapGet("/mine", GetMineAsync).RequireRateLimiting("baseline");
        artworks.MapGet("/{artworkId:guid}", GetAsync).RequireRateLimiting("baseline");
        artworks.MapPost("/", CreateAsync).RequireRateLimiting("baseline");
        artworks.MapPut("/{artworkId:guid}/draft", UpdateDraftAsync).RequireRateLimiting("baseline");
        artworks.MapPost("/{artworkId:guid}/revisions", CreateRevisionAsync).RequireRateLimiting("baseline");
        artworks.MapPost("/{artworkId:guid}/submit", SubmitAsync).RequireRateLimiting("baseline");
        artworks.MapPost("/{artworkId:guid}/listing-draft", CreateListingDraftAsync).RequireRateLimiting("baseline");
        artworks.MapPost("/{artworkId:guid}/uploads/intents", CreateUploadIntentAsync).RequireRateLimiting("upload-intent");
        artworks.MapPost("/{artworkId:guid}/uploads/{intentId:guid}/finalize", FinalizeUploadAsync).RequireRateLimiting("upload-finalize");
        artworks.MapGet("/{artworkId:guid}/assets/{assetId:guid}/download", PrivateDownloadAsync).RequireRateLimiting("private-download");
        return endpoints;
    }

    private static async Task<IResult> CreateCategoryAsync(CategoryRequest request, MarketplaceDbContext db, CancellationToken ct)
    {
        try { var item=new Category(Guid.NewGuid(),request.Name,request.Slug,request.DisplayOrder); db.Categories.Add(item); await db.SaveChangesAsync(ct); return Results.Created($"/api/categories/{item.Id}",new {item.Id}); }
        catch (ArgumentException ex) { return Validation(ex.Message); }
        catch (DbUpdateException) { return Results.Conflict(new { message="Category slug already exists." }); }
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if(!UserId(principal,out var userId)) return Results.Unauthorized();
        var result=await db.Artworks.AsNoTracking().Where(x=>x.CurrentOwnerUserId==userId)
            .OrderByDescending(x=>x.CreatedAtUtc).Select(x=>new{x.Id,x.InternalCode,x.CreatedAtUtc}).ToListAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAsync(Guid artworkId, ClaimsPrincipal principal, MarketplaceDbContext db, CancellationToken ct)
    {
        if(!UserId(principal,out var userId)) return Results.Unauthorized();
        var artwork=await db.Artworks.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==artworkId&&x.CurrentOwnerUserId==userId,ct);
        if(artwork is null) return Results.NotFound();
        var revisions=await db.ArtworkRevisions.AsNoTracking().Where(x=>x.ArtworkId==artworkId).OrderByDescending(x=>x.Number).ToListAsync(ct);
        var ids=revisions.Select(x=>x.Id).ToArray();
        var assets=await db.ArtworkAssets.AsNoTracking().Where(x=>ids.Contains(x.ArtworkRevisionId))
            .Select(x=>new{x.Id,x.ArtworkRevisionId,x.Kind,x.ContentType,x.Size,x.Sha256}).ToListAsync(ct);
        return Results.Ok(new{artwork.Id,artwork.InternalCode,artwork.CurrentOwnerUserId,revisions,assets});
    }

    private static async Task<IResult> CreateAsync(ArtworkDraftRequest request, ClaimsPrincipal principal,
        ISellerEligibilityGate gate, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if(!UserId(principal,out var userId)) return Results.Unauthorized();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var now=time.GetUtcNow(); var artworkId=Guid.NewGuid();
        var eligibility=await LockSellerGuard(db,userId,now,ct); if(!eligibility.Allowed) return Results.Forbid();
        if(!await db.Categories.AnyAsync(x=>x.Id==request.CategoryId&&x.IsActive,ct)) return Validation("An active category is required.");
        try
        {
            var artwork=new Artwork(artworkId,userId,$"ART-{artworkId:N}"[..16],now);
            var revision=new ArtworkRevision(Guid.NewGuid(),artworkId,1,userId,request.Title,request.ArtistAttribution,
                request.Description,request.CategoryId,request.Year,request.Medium,request.Dimensions,request.Condition,now);
            db.AddRange(artwork,revision); AddAudit(db,"artwork.created",userId,artworkId,now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Created($"/api/artworks/{artworkId}",new{artworkId,revisionId=revision.Id});
        }
        catch(ArgumentException ex){return Validation(ex.Message);}
    }

    private static async Task<IResult> UpdateDraftAsync(Guid artworkId, ArtworkDraftRequest request, ClaimsPrincipal principal,
        ISellerEligibilityGate gate, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if(!UserId(principal,out var userId)) return Results.Unauthorized();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(!(await LockSellerGuard(db,userId,time.GetUtcNow(),ct)).Allowed) return Results.Forbid();
        var artwork=await LockArtwork(db,artworkId,ct); if(artwork is null||artwork.CurrentOwnerUserId!=userId) return Results.NotFound();
        _=await LockOpenReviewCase(db,artworkId,ct);
        if(!await db.Categories.AnyAsync(x=>x.Id==request.CategoryId&&x.IsActive,ct)) return Validation("An active category is required.");
        var revision=await db.ArtworkRevisions.SingleOrDefaultAsync(x=>x.ArtworkId==artworkId&&x.Status==ArtworkRevisionStatus.Draft,ct);
        if(revision is null) return Results.Conflict(new{message="No mutable draft exists; create a new revision."});
        try { revision.Update(userId,request.Title,request.ArtistAttribution,request.Description,request.CategoryId,request.Year,request.Medium,request.Dimensions,request.Condition,time.GetUtcNow()); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent(); }
        catch(ArgumentException ex){return Validation(ex.Message);} catch(InvalidOperationException ex){return Results.Conflict(new{message=ex.Message});}
    }

    private static async Task<IResult> CreateRevisionAsync(Guid artworkId, ArtworkDraftRequest request, ClaimsPrincipal principal,
        MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if(!UserId(principal,out var userId)) return Results.Unauthorized();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(!(await LockSellerGuard(db,userId,time.GetUtcNow(),ct)).Allowed) return Results.Forbid();
        var artwork=await LockArtwork(db,artworkId,ct); if(artwork is null||artwork.CurrentOwnerUserId!=userId) return Results.NotFound();
        var review=await LockOpenReviewCase(db,artworkId,ct);
        if(review is null) return Results.Conflict(new{message="A new revision is only allowed for an open change-request workflow."});
        var latestRound=await db.ReviewRounds.Where(x=>x.ReviewCaseId==review.Id).OrderByDescending(x=>x.Number).FirstAsync(ct);
        if(latestRound.Status!=ReviewRoundStatus.ChangesRequested) return Results.Conflict(new{message="The latest review round has not requested changes."});
        if(await db.ArtworkRevisions.AnyAsync(x=>x.ArtworkId==artworkId&&x.Status==ArtworkRevisionStatus.Draft,ct)) return Results.Conflict(new{message="A mutable draft already exists."});
        if(!await db.Categories.AnyAsync(x=>x.Id==request.CategoryId&&x.IsActive,ct)) return Validation("An active category is required.");
        var number=(await db.ArtworkRevisions.Where(x=>x.ArtworkId==artworkId).MaxAsync(x=>(int?)x.Number,ct)??0)+1;
        try
        {
            var revision=new ArtworkRevision(Guid.NewGuid(),artworkId,number,userId,request.Title,request.ArtistAttribution,request.Description,request.CategoryId,request.Year,request.Medium,request.Dimensions,request.Condition,time.GetUtcNow());
            db.ArtworkRevisions.Add(revision);AddAudit(db,"artwork.revision-created",userId,artworkId,time.GetUtcNow());
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Created($"/api/artworks/{artworkId}",new{revisionId=revision.Id,revision.Number});
        }
        catch(ArgumentException ex){return Validation(ex.Message);}catch(DbUpdateException){return Results.Conflict(new{message="A concurrent draft already exists."});}
    }

    private static async Task<IResult> SubmitAsync(Guid artworkId, ClaimsPrincipal principal, ISellerEligibilityGate gate,
        MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if(!UserId(principal,out var userId)) return Results.Unauthorized();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(!(await LockSellerGuard(db,userId,time.GetUtcNow(),ct)).Allowed) return Results.Forbid();
        var artwork=await LockArtwork(db,artworkId,ct); if(artwork is null||artwork.CurrentOwnerUserId!=userId) return Results.NotFound();
        var revision=await db.ArtworkRevisions.SingleOrDefaultAsync(x=>x.ArtworkId==artworkId&&x.Status==ArtworkRevisionStatus.Draft,ct);
        if(revision is null) return Results.Conflict(new{message="No mutable draft exists."});
        if(!await db.ArtworkAssets.AnyAsync(x=>x.ArtworkRevisionId==revision.Id&&x.Kind==ArtworkAssetKind.PublicImage,ct)) return Validation("At least one clean public image is required.");
        var review=await LockOpenReviewCase(db,artworkId,ct);
        var now=time.GetUtcNow();
        try
        {
            revision.Freeze(userId,now);
            if(review is null){review=new ReviewCase(Guid.NewGuid(),artworkId,userId,now);db.ReviewCases.Add(review);}
            var roundNumber=await db.ReviewRounds.Where(x=>x.ReviewCaseId==review.Id).MaxAsync(x=>(int?)x.Number,ct)??0;
            db.ReviewRounds.Add(new ReviewRound(Guid.NewGuid(),review.Id,revision.Id,roundNumber+1,now));
            AddAudit(db,"artwork.submitted",userId,artworkId,now); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Accepted($"/api/artworks/{artworkId}",new{artworkId,revisionId=revision.Id,reviewCaseId=review.Id});
        }
        catch(DbUpdateException){return Results.Conflict(new{message="A concurrent submission already won. Reload the artwork."});}
    }

    private static async Task<IResult> CreateListingDraftAsync(Guid artworkId, ListingDraftRequest request, ClaimsPrincipal principal,
        ISellerEligibilityGate gate, MarketplaceDbContext db, TimeProvider time, CancellationToken ct)
    {
        if(!UserId(principal,out var userId)) return Results.Unauthorized();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var eligibility=await LockSellerGuard(db,userId,time.GetUtcNow(),ct);if(!eligibility.Allowed)return Results.Forbid();
        var artwork=await LockArtwork(db,artworkId,ct); if(artwork is null||artwork.CurrentOwnerUserId!=userId)return Results.NotFound();
        try { var listing=new MarketplaceListing(Guid.NewGuid(),artworkId,userId,new Money(request.Amount,request.Currency),eligibility.Version,time.GetUtcNow()); db.MarketplaceListings.Add(listing); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Created($"/api/artworks/{artworkId}/listing-draft",new{listing.Id}); }
        catch(ArgumentException ex){return Validation(ex.Message);}catch(DbUpdateException){return Results.Conflict(new{message="A non-final listing already exists."});}
    }

    private static async Task<IResult> CreateUploadIntentAsync(Guid artworkId, UploadIntentRequest request, ClaimsPrincipal principal,
        ISellerEligibilityGate gate, MarketplaceDbContext db, IArtworkObjectStorage storage, IOptions<ObjectStorageOptions> options,
        TimeProvider time, CancellationToken ct)
    {
        if(!UserId(principal,out var userId))return Results.Unauthorized();
        var artwork=await db.Artworks.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==artworkId&&x.CurrentOwnerUserId==userId,ct);if(artwork is null)return Results.NotFound();
        if(!(await gate.EvaluateAsync(userId,null,ct)).Allowed)return Results.Forbid();
        var revision=await db.ArtworkRevisions.AsNoTracking().SingleOrDefaultAsync(x=>x.ArtworkId==artworkId&&x.Status==ArtworkRevisionStatus.Draft,ct);if(revision is null)return Results.Conflict(new{message="Uploads require a mutable draft."});
        if(!ValidateUpload(request,out var kind,out var reason))return Validation(reason);
        var count=await db.ArtworkAssets.CountAsync(x=>x.ArtworkRevisionId==revision.Id&&x.Kind==kind,ct);
        if(count >= (kind==ArtworkAssetKind.PublicImage?8:5)) return Validation("The file count limit was reached.");
        var now=time.GetUtcNow();var expires=now.AddMinutes(options.Value.UploadUrlMinutes);var id=Guid.NewGuid();
        var key=$"quarantine/{userId:N}/{id:N}";var intent=new UploadIntent(id,userId,artworkId,revision.Id,kind,request.FileName,request.ContentType,request.Size,key,now,expires);
        db.UploadIntents.Add(intent);await db.SaveChangesAsync(ct);
        return Results.Ok(new{id,uploadUrl=storage.CreateQuarantineUploadUrl(key,request.ContentType,request.Size,expires),expiresAtUtc=expires,requiredContentType=request.ContentType,requiredMetadata=new{expectedLength=request.Size}});
    }

    private static async Task<IResult> FinalizeUploadAsync(Guid artworkId,Guid intentId,ClaimsPrincipal principal,MarketplaceDbContext db,
        IArtworkObjectStorage storage,IFileSafetyScanner scanner,IImageSanitizer sanitizer,TimeProvider time,CancellationToken ct)
    {
        if(!UserId(principal,out var userId))return Results.Unauthorized();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var intent=await db.UploadIntents.FromSqlInterpolated($"SELECT * FROM catalog.upload_intents WHERE \"Id\" = {intentId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(intent is null||intent.ArtworkId!=artworkId||intent.UserId!=userId)return Results.NotFound();
        var artwork=await LockArtwork(db,artworkId,ct);if(artwork is null||artwork.CurrentOwnerUserId!=userId)return Results.NotFound();
        var revision=await db.ArtworkRevisions.SingleOrDefaultAsync(x=>x.Id==intent.ArtworkRevisionId&&x.Status==ArtworkRevisionStatus.Draft,ct);if(revision is null)return Results.Conflict(new{message="The target revision is no longer mutable."});
        var now=time.GetUtcNow();
        try
        {
            intent.BeginProcessing(userId,now);
            await using var source=await storage.ReadQuarantineAsync(intent.QuarantineObjectKey,intent.ExpectedSize,ct);
            if(source.Length!=intent.ExpectedSize)throw new InvalidDataException("Uploaded size does not match the intent.");
            if(!SignatureMatches(source.Content,intent.ContentType))throw new InvalidDataException("File signature does not match its declared MIME type.");
            source.Content.Position=0;var verdict=await scanner.ScanAsync(source.Content,intent.ContentType,ct);if(!verdict.IsClean)throw new InvalidDataException(verdict.Reason);
            source.Content.Position=0;Stream clean=source.Content;SanitizedFile? sanitized=null;
            if(intent.ContentType.StartsWith("image/",StringComparison.Ordinal)){sanitized=await sanitizer.SanitizeAsync(source.Content,intent.ContentType,ct);clean=sanitized.Content;}
            await using var cleanup=sanitized;clean.Position=0;var hash=Convert.ToHexString(await SHA256.HashDataAsync(clean,ct));clean.Position=0;
            var extension=intent.ContentType switch{"image/jpeg"=>"jpg","image/png"=>"png","application/pdf"=>"pdf",_=>throw new InvalidDataException("Unsupported content type.")};
            var assetId=Guid.NewGuid();var cleanKey=$"artworks/{artworkId:N}/revisions/{revision.Id:N}/{assetId:N}.{extension}";
            var promoted=await storage.PutCleanIfAbsentAsync(cleanKey,clean,intent.ContentType,hash,ct);
            string? derivativeKey=null;
            if(intent.Kind==ArtworkAssetKind.PublicImage)
            {
                derivativeKey=$"public/artworks/{artworkId:N}/{assetId:N}.{extension}";
                clean.Position=0; await storage.PutPublicDerivativeIfAbsentAsync(derivativeKey,clean,intent.ContentType,hash,ct);
            }
            db.ArtworkAssets.Add(new ArtworkAsset(assetId,revision.Id,intent.Kind,promoted.ObjectKey,hash,clean.Length,intent.ContentType,promoted.VersionId,derivativeKey,now));
            intent.Complete(now);AddAudit(db,"artwork.asset-finalized",userId,artworkId,now);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            try{await storage.DeleteQuarantineAsync(intent.QuarantineObjectKey,ct);}catch{ /* idempotent cleanup job may retry */ }
            return Results.Ok(new{assetId,sha256=hash,size=clean.Length});
        }
        catch(Exception ex) when(ex is InvalidDataException or InvalidOperationException)
        {
            if(intent.Status==UploadIntentStatus.Processing){intent.Reject(ex.Message,now);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}return Validation(ex.Message);
        }
    }

    private static async Task<IResult> PrivateDownloadAsync(Guid artworkId,Guid assetId,ClaimsPrincipal principal,MarketplaceDbContext db,
        IArtworkObjectStorage storage,IOptions<ObjectStorageOptions> options,TimeProvider time,CancellationToken ct)
    {
        if(!UserId(principal,out var userId))return Results.Unauthorized();
        var asset=await (from a in db.ArtworkAssets.AsNoTracking() join r in db.ArtworkRevisions.AsNoTracking() on a.ArtworkRevisionId equals r.Id join w in db.Artworks.AsNoTracking() on r.ArtworkId equals w.Id where a.Id==assetId&&w.Id==artworkId select new{Asset=a,Owner=w.CurrentOwnerUserId}).SingleOrDefaultAsync(ct);
        if(asset is null)return Results.NotFound();
        var staff=(principal.IsInRole("Expert")||principal.IsInRole("Admin")||principal.IsInRole("SuperAdmin"))
            && principal.GetClaims("amr").Contains("mfa",StringComparer.Ordinal);
        if(asset.Owner!=userId&&!staff)return Results.NotFound();
        if(asset.Asset.Kind==ArtworkAssetKind.PublicImage)return Validation("Public derivatives are served through the public media path.");
        var expires=time.GetUtcNow().AddMinutes(options.Value.PrivateDownloadUrlMinutes);
        return Results.Ok(new{downloadUrl=storage.CreatePrivateDownloadUrl(asset.Asset.CleanObjectKey,expires),expiresAtUtc=expires});
    }

    private static async Task<IResult> PublicMediaAsync(Guid assetId,MarketplaceDbContext db,IArtworkObjectStorage storage,TimeProvider time,CancellationToken ct)
    {
        var key=await db.ArtworkAssets.AsNoTracking().Where(x=>x.Id==assetId&&x.Kind==ArtworkAssetKind.PublicImage)
            .Select(x=>x.PublicDerivativeObjectKey).SingleOrDefaultAsync(ct);
        if(string.IsNullOrWhiteSpace(key))return Results.NotFound();
        return Results.Redirect(storage.CreatePublicReadUrl(key,time.GetUtcNow().AddMinutes(5)));
    }

    private static Task<Artwork?> LockArtwork(MarketplaceDbContext db,Guid id,CancellationToken ct)=>db.Artworks.FromSqlInterpolated($"SELECT * FROM catalog.artworks WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static Task<ReviewCase?> LockOpenReviewCase(MarketplaceDbContext db,Guid artworkId,CancellationToken ct)=>db.ReviewCases.FromSqlInterpolated($"SELECT * FROM reviews.review_cases WHERE \"ArtworkId\" = {artworkId} AND \"Status\" = 'Open' FOR UPDATE").SingleOrDefaultAsync(ct);
    private static async Task<(bool Allowed,long Version)> LockSellerGuard(MarketplaceDbContext db,Guid userId,DateTimeOffset now,CancellationToken ct)
    {
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(user is null||!user.IsEnabled)return (false,0);
        var eligibility=await db.SellerEligibilities.FromSqlInterpolated($"SELECT * FROM profiles.seller_eligibilities WHERE \"UserId\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        return eligibility is not null&&eligibility.CanSellAt(now)?(true,eligibility.EligibilityVersion):(false,eligibility?.EligibilityVersion??0);
    }
    private static bool ValidateUpload(UploadIntentRequest r,out ArtworkAssetKind kind,out string reason)
    {
        kind=r.Kind.Trim().ToLowerInvariant() switch{"public-image"=>ArtworkAssetKind.PublicImage,"private-original"=>ArtworkAssetKind.PrivateOriginal,"private-document"=>ArtworkAssetKind.PrivateDocument,_=>(ArtworkAssetKind)(-1)};
        var extension=Path.GetExtension(r.FileName).ToLowerInvariant();var valid=(r.ContentType,extension) switch{("image/jpeg",".jpg" or ".jpeg")=>true,("image/png",".png")=>true,("application/pdf",".pdf")=>true,_=>false};
        var max=r.ContentType.StartsWith("image/",StringComparison.Ordinal)?MaxImageBytes:MaxDocumentBytes;
        if((int)kind<0){reason="Unknown asset kind.";return false;}if(!valid){reason="Only matching JPEG, PNG, and PDF files are accepted.";return false;}if(r.Size<=0||r.Size>max){reason=$"File size must be between 1 and {max} bytes.";return false;}if(kind==ArtworkAssetKind.PublicImage&&!r.ContentType.StartsWith("image/",StringComparison.Ordinal)){reason="Public assets must be images.";return false;}reason=string.Empty;return true;
    }
    private static bool SignatureMatches(Stream stream,string type){Span<byte> h=stackalloc byte[8];var read=stream.Read(h);stream.Position=0;return type switch{"image/jpeg"=>read>=3&&h[0]==0xFF&&h[1]==0xD8&&h[2]==0xFF,"image/png"=>read==8&&h.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),"application/pdf"=>read>=5&&h[..5].SequenceEqual("%PDF-"u8),_=>false};}
    private static bool UserId(ClaimsPrincipal p,out Guid id)=>Guid.TryParse(p.GetClaim(OpenIddictConstants.Claims.Subject),out id);
    private static void AddAudit(MarketplaceDbContext db,string type,Guid actor,Guid aggregate,DateTimeOffset now)=>db.SecurityAuditEvents.Add(new SecurityAuditEvent(Guid.NewGuid(),type,actor,actor,JsonSerializer.Serialize(new{aggregateId=aggregate}),null,now));
    private static IResult Validation(string message)=>Results.ValidationProblem(new Dictionary<string,string[]>{{"request",[message]}});
    internal sealed record CategoryRequest(string Name,string Slug,int DisplayOrder);
    internal sealed record ArtworkDraftRequest(string Title,string ArtistAttribution,string Description,Guid CategoryId,int? Year,string Medium,string Dimensions,string Condition);
    internal sealed record UploadIntentRequest(string FileName,string ContentType,long Size,string Kind);
    internal sealed record ListingDraftRequest(decimal Amount,string Currency);
}
