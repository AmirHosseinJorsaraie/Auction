using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Profiles;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArtMarketplace.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(
    DbContextOptions<MarketplaceDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<SavedAddress> SavedAddresses => Set<SavedAddress>();

    public DbSet<SellerVerificationCase> SellerVerificationCases => Set<SellerVerificationCase>();

    public DbSet<SellerEligibility> SellerEligibilities => Set<SellerEligibility>();

    public DbSet<SellerEligibilityInvalidation> SellerEligibilityInvalidations => Set<SellerEligibilityInvalidation>();

    public DbSet<SellerEligibilityInvalidationResolution> SellerEligibilityInvalidationResolutions =>
        Set<SellerEligibilityInvalidationResolution>();

    public DbSet<BffSession> BffSessions => Set<BffSession>();

    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Artwork> Artworks => Set<Artwork>();
    public DbSet<ArtworkRevision> ArtworkRevisions => Set<ArtworkRevision>();
    public DbSet<ArtworkAsset> ArtworkAssets => Set<ArtworkAsset>();
    public DbSet<UploadIntent> UploadIntents => Set<UploadIntent>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ReviewCase> ReviewCases => Set<ReviewCase>();
    public DbSet<ReviewRound> ReviewRounds => Set<ReviewRound>();
    public DbSet<ReviewAssignment> ReviewAssignments => Set<ReviewAssignment>();
    public DbSet<ReviewFinding> ReviewFindings => Set<ReviewFinding>();
    public DbSet<ReviewDecision> ReviewDecisions => Set<ReviewDecision>();
    public DbSet<ReviewDecisionSupersession> ReviewDecisionSupersessions => Set<ReviewDecisionSupersession>();
    public DbSet<ReviewAnnotation> ReviewAnnotations => Set<ReviewAnnotation>();
    public DbSet<ReviewConflict> ReviewConflicts => Set<ReviewConflict>();
    public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict<Guid>();

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users", "identity");
            entity.Property(user => user.IsEnabled).IsRequired();
            entity.Property(user => user.AuthorizationVersion).IsConcurrencyToken();
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("roles", "identity");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles", "identity");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", "identity");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", "identity");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims", "identity");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", "identity");

        builder.Entity<BffSession>(entity =>
        {
            entity.ToTable("bff_sessions", "identity");
            entity.HasKey(session => session.Id);
            entity.HasIndex(session => new { session.Surface, session.HandleDigest }).IsUnique();
            entity.HasIndex(session => session.ExpiresAtUtc);
            entity.Property(session => session.Surface).HasMaxLength(32);
            entity.Property(session => session.HandleDigest).HasMaxLength(64);
            entity.Property(session => session.Version).IsConcurrencyToken();
        });

        builder.Entity<SecurityAuditEvent>(entity =>
        {
            entity.ToTable("security_audit_events", "identity");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.SubjectUserId, item.CreatedAtUtc });
            entity.Property(item => item.EventType).HasMaxLength(120);
            entity.Property(item => item.TraceId).HasMaxLength(128);
            entity.Property(item => item.DataJson).HasColumnType("jsonb");
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages", "platform");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.PublishedAtUtc, item.OccurredAtUtc });
            entity.Property(item => item.Type).HasMaxLength(200);
            entity.Property(item => item.PayloadJson).HasColumnType("jsonb");
        });

        builder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles", "profiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.DisplayName).HasMaxLength(120);
            entity.Property(profile => profile.Locale).HasMaxLength(16);
            entity.Property(profile => profile.PublicSellerBio).HasMaxLength(1_000);
        });

        builder.Entity<SavedAddress>(entity =>
        {
            entity.ToTable("saved_addresses", "profiles");
            entity.HasKey(address => address.Id);
            entity.HasIndex(address => address.UserId);
            entity.Property(address => address.Label).HasMaxLength(60);
            entity.Property(address => address.RecipientName).HasMaxLength(120);
            entity.Property(address => address.AddressLine).HasMaxLength(500);
            entity.Property(address => address.PostalCode).HasMaxLength(32);
        });

        builder.Entity<SellerVerificationCase>(entity =>
        {
            entity.ToTable("seller_verification_cases", "profiles");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ApplicantUserId, item.Status });
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.DecisionReason).HasMaxLength(1_000);
        });

        builder.Entity<SellerEligibility>(entity =>
        {
            entity.ToTable("seller_eligibilities", "profiles");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.EligibilityVersion).IsConcurrencyToken();
        });

        builder.Entity<SellerEligibilityInvalidation>(entity =>
        {
            entity.ToTable("seller_eligibility_invalidations", "profiles");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.UserId, item.InvalidatedVersion }).IsUnique();
            entity.Property(item => item.Reason).HasMaxLength(1_000);
            entity.HasMany(item => item.Resolutions)
                .WithOne()
                .HasForeignKey(item => item.InvalidationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SellerEligibilityInvalidationResolution>(entity =>
        {
            entity.ToTable("seller_eligibility_invalidation_resolutions", "profiles");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.InvalidationId, item.TargetType, item.TargetId }).IsUnique();
            entity.Property(item => item.TargetType).HasMaxLength(80);
            entity.Property(item => item.Reason).HasMaxLength(1_000);
            entity.Property(item => item.Evidence).HasMaxLength(2_000);
        });

        builder.Entity<Artwork>(entity =>
        {
            entity.ToTable("artworks", "catalog"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InternalCode).IsUnique(); entity.HasIndex(x => x.CurrentOwnerUserId);
            entity.Property(x => x.InternalCode).HasMaxLength(64); entity.Property(x => x.Version).IsConcurrencyToken();
        });
        builder.Entity<ArtworkRevision>(entity =>
        {
            entity.ToTable("artwork_revisions", "catalog"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ArtworkId, x.Number }).IsUnique();
            entity.HasIndex(x => x.ArtworkId).IsUnique().HasFilter("\"Status\" = 'Draft'");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Title).HasMaxLength(200); entity.Property(x => x.ArtistAttribution).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(5_000); entity.Property(x => x.Medium).HasMaxLength(200);
            entity.Property(x => x.Dimensions).HasMaxLength(200); entity.Property(x => x.Condition).HasMaxLength(1_000);
            entity.Property(x => x.DiscardReason).HasMaxLength(500); entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<Artwork>().WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Artist>().WithMany().HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ArtworkAsset>(entity =>
        {
            entity.ToTable("artwork_assets", "catalog"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CleanObjectKey).IsUnique(); entity.HasIndex(x => new { x.ArtworkRevisionId, x.Kind });
            entity.HasIndex(x => x.PublicDerivativeObjectKey).IsUnique().HasFilter("\"PublicDerivativeObjectKey\" IS NOT NULL");
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32); entity.Property(x => x.CleanObjectKey).HasMaxLength(500);
            entity.Property(x => x.Sha256).HasMaxLength(64); entity.Property(x => x.ContentType).HasMaxLength(100); entity.Property(x => x.VersionId).HasMaxLength(200);
            entity.Property(x => x.PublicDerivativeObjectKey).HasMaxLength(500);
            entity.HasOne<ArtworkRevision>().WithMany().HasForeignKey(x => x.ArtworkRevisionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<UploadIntent>(entity =>
        {
            entity.ToTable("upload_intents", "catalog"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.QuarantineObjectKey).IsUnique(); entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32); entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.FileName).HasMaxLength(255); entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.QuarantineObjectKey).HasMaxLength(500); entity.Property(x => x.FailureReason).HasMaxLength(1_000);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<Artwork>().WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArtworkRevision>().WithMany().HasForeignKey(x => x.ArtworkRevisionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Artist>(entity =>
        {
            entity.ToTable("artists", "catalog"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200); entity.Property(x => x.Slug).HasMaxLength(160); entity.Property(x => x.Biography).HasMaxLength(2_000);
        });
        builder.Entity<Category>(entity =>
        {
            entity.ToTable("categories", "catalog"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120); entity.Property(x => x.Slug).HasMaxLength(120);
        });
        builder.Entity<ReviewCase>(entity =>
        {
            entity.ToTable("review_cases", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ArtworkId).IsUnique().HasFilter("\"Status\" = 'Open'");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24); entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<Artwork>().WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReviewRound>(entity =>
        {
            entity.ToTable("review_rounds", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ReviewCaseId, x.Number }).IsUnique();
            entity.HasIndex(x => x.ReviewCaseId).IsUnique().HasFilter("\"Status\" = 'PendingReview'");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<ReviewCase>().WithMany().HasForeignKey(x => x.ReviewCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArtworkRevision>().WithMany().HasForeignKey(x => x.ArtworkRevisionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReviewAssignment>(entity =>
        {
            entity.ToTable("review_assignments", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ReviewRoundId).IsUnique(); entity.HasIndex(x => x.ExpertUserId);
            entity.HasOne<ReviewRound>().WithMany().HasForeignKey(x => x.ReviewRoundId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReviewFinding>(entity =>
        {
            entity.ToTable("review_findings", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ReviewRoundId, x.CreatedAtUtc });
            entity.Property(x => x.Category).HasMaxLength(80); entity.Property(x => x.Message).HasMaxLength(2_000);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500); entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(24);
            entity.HasOne<ReviewRound>().WithMany().HasForeignKey(x => x.ReviewRoundId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReviewDecision>(entity =>
        {
            entity.ToTable("review_decisions", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ReviewRoundId).IsUnique(); entity.HasIndex(x => new { x.ArtworkRevisionId, x.CreatedAtUtc });
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24); entity.Property(x => x.Reason).HasMaxLength(1_000);
            entity.Property(x => x.PublicComment).HasMaxLength(2_000); entity.Property(x => x.PrivateNote).HasMaxLength(2_000);
            entity.HasOne<ReviewCase>().WithMany().HasForeignKey(x => x.ReviewCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReviewRound>().WithMany().HasForeignKey(x => x.ReviewRoundId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArtworkRevision>().WithMany().HasForeignKey(x => x.ArtworkRevisionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReviewDecisionSupersession>(entity =>
        {
            entity.ToTable("review_decision_supersessions", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ReviewDecisionId).IsUnique(); entity.Property(x => x.Reason).HasMaxLength(1_000);
            entity.HasOne<ReviewDecision>().WithMany().HasForeignKey(x => x.ReviewDecisionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReviewAnnotation>(entity =>
        {
            entity.ToTable("review_annotations", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ReviewDecisionId, x.CreatedAtUtc }); entity.Property(x => x.Message).HasMaxLength(2_000);
            entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(24);
            entity.HasOne<ReviewDecision>().WithMany().HasForeignKey(x => x.ReviewDecisionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReviewConflict>(entity =>
        {
            entity.ToTable("review_conflicts", "reviews"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ArtworkId, x.ExpertUserId }).IsUnique(); entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasOne<Artwork>().WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<MarketplaceListing>(entity =>
        {
            entity.ToTable("marketplace_listings", "marketplace"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ArtworkId).IsUnique().HasFilter("\"Status\" = 'Draft'");
            entity.Property(x => x.PriceAmount).HasPrecision(18, 2); entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.ToTable(table => table.HasCheckConstraint("CK_marketplace_listings_positive_price", "\"PriceAmount\" > 0"));
            entity.HasOne<Artwork>().WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArtworkRevision>().WithMany().HasForeignKey(x => x.PublishedArtworkRevisionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAppendOnlyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceAppendOnlyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceAppendOnlyAudit()
    {
        if (ChangeTracker.Entries<SecurityAuditEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Security audit events are append-only.");
        }
        if (ChangeTracker.Entries().Any(entry =>
                entry.Entity is ReviewFinding or ReviewDecision or ReviewDecisionSupersession or ReviewAnnotation or ReviewConflict
                && entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Review evidence and decision history are append-only.");
        }
    }
}
