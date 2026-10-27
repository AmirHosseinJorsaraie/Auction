using ArtMarketplace.Domain.Modules.Identity;
using ArtMarketplace.Domain.Modules.Profiles;
using ArtMarketplace.Domain.Modules.Catalog;
using ArtMarketplace.Domain.Modules.Reviews;
using ArtMarketplace.Domain.Modules.Marketplace;
using ArtMarketplace.Domain.Modules.Orders;
using ArtMarketplace.Domain.Modules.Payments;
using ArtMarketplace.Domain.Modules.Administration;
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
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<OwnershipTransfer> OwnershipTransfers => Set<OwnershipTransfer>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<PaymentProviderEvent> PaymentProviderEvents => Set<PaymentProviderEvent>();
    public DbSet<VerifiedPaymentReceipt> VerifiedPaymentReceipts => Set<VerifiedPaymentReceipt>();
    public DbSet<OrderPaymentApplication> OrderPaymentApplications => Set<OrderPaymentApplication>();
    public DbSet<PaymentReconciliationCase> PaymentReconciliationCases => Set<PaymentReconciliationCase>();
    public DbSet<ManagedContentPage> ManagedContentPages => Set<ManagedContentPage>();
    public DbSet<SiteBanner> SiteBanners => Set<SiteBanner>();

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
            entity.HasIndex(session => new { session.SubjectUserId, session.RevokedAtUtc });
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
            entity.HasIndex(x => x.ArtworkId).IsUnique().HasFilter("\"Status\" IN ('Draft', 'Published', 'Suspended', 'Reserved', 'Sold')");
            entity.Property(x => x.PriceAmount).HasPrecision(18, 2); entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.SuspensionReason).HasMaxLength(1_000); entity.Property(x => x.Version).IsConcurrencyToken();
            entity.ToTable(table => table.HasCheckConstraint("CK_marketplace_listings_positive_price", "\"PriceAmount\" > 0"));
            entity.HasOne<Artwork>().WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArtworkRevision>().WithMany().HasForeignKey(x => x.PublishedArtworkRevisionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Cart>(entity =>
        {
            entity.ToTable("carts", "orders"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.BuyerUserId).IsUnique();
            entity.Property(x => x.Currency).HasMaxLength(3); entity.Property(x => x.Version).IsConcurrencyToken();
        });
        builder.Entity<CartItem>(entity =>
        {
            entity.ToTable("cart_items", "orders"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.CartId, x.ListingId }).IsUnique();
            entity.Property(x => x.PriceAmountAtAdd).HasPrecision(18, 2); entity.Property(x => x.CurrencyAtAdd).HasMaxLength(3);
            entity.HasOne<Cart>().WithMany().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MarketplaceListing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_cart_items_positive_price", "\"PriceAmountAtAdd\" > 0"));
        });
        builder.Entity<Order>(entity =>
        {
            entity.ToTable("orders", "orders"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BuyerUserId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.ReservationExpiresAtUtc }); entity.HasIndex(x => x.SellerUserId);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2); entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100); entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.RecipientName).HasMaxLength(120); entity.Property(x => x.AddressLine).HasMaxLength(500);
            entity.Property(x => x.PostalCode).HasMaxLength(32); entity.Property(x => x.HoldReason).HasMaxLength(1_000);
            entity.Property(x => x.TrackingCode).HasMaxLength(120); entity.Property(x => x.Version).IsConcurrencyToken();
            entity.ToTable(table => { table.HasCheckConstraint("CK_orders_positive_total", "\"TotalAmount\" > 0"); table.HasCheckConstraint("CK_orders_positive_eligibility_version", "\"SellerEligibilityVersionAtCheckout\" > 0"); });
        });
        builder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items", "orders"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.OrderId, x.ListingId }).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(200); entity.Property(x => x.PriceAmount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3); entity.Property(x => x.SellerDisplayName).HasMaxLength(120);
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MarketplaceListing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Artwork>().WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArtworkRevision>().WithMany().HasForeignKey(x => x.ArtworkRevisionId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_order_items_positive_price", "\"PriceAmount\" > 0"));
        });
        builder.Entity<Reservation>(entity =>
        {
            entity.ToTable("reservations", "orders"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => x.ListingId).IsUnique().HasFilter("\"Status\" = 'Active'");
            entity.HasIndex(x => new { x.OrderId, x.Status }); entity.Property(x => x.Token).HasMaxLength(100);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderItem>().WithOne().HasForeignKey<Reservation>(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MarketplaceListing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<OwnershipTransfer>(entity =>
        {
            entity.ToTable("ownership_transfers", "orders"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderItemId).IsUnique(); entity.HasIndex(x => x.ListingId).IsUnique(); entity.HasIndex(x => x.ArtworkId).IsUnique();
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderItem>().WithOne().HasForeignKey<OwnershipTransfer>(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MarketplaceListing>().WithOne().HasForeignKey<OwnershipTransfer>(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Artwork>().WithOne().HasForeignKey<OwnershipTransfer>(x => x.ArtworkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderPaymentApplication>().WithMany().HasForeignKey(x => x.OrderPaymentApplicationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PaymentAttempt>(entity =>
        {
            entity.ToTable("payment_attempts", "payments"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrderId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.ProviderKey, x.MerchantReference }).IsUnique();
            entity.HasIndex(x => new { x.ProviderKey, x.ProviderTransactionId }).IsUnique().HasFilter("\"ProviderTransactionId\" IS NOT NULL");
            entity.Property(x => x.ProviderKey).HasMaxLength(40); entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.MerchantReference).HasMaxLength(100); entity.Property(x => x.MerchantAccount).HasMaxLength(120);
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(160); entity.Property(x => x.RedirectUrl).HasMaxLength(2_000);
            entity.Property(x => x.FailureCode).HasMaxLength(120); entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3); entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PaymentProviderEvent>(entity =>
        {
            entity.ToTable("provider_events", "payments"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProviderKey, x.EventId }).IsUnique();
            entity.Property(x => x.ProviderKey).HasMaxLength(40); entity.Property(x => x.EventId).HasMaxLength(160);
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(160); entity.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Amount).HasPrecision(18, 2); entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.MerchantReference).HasMaxLength(100); entity.Property(x => x.MerchantAccount).HasMaxLength(120);
            entity.Property(x => x.PayloadDigest).HasMaxLength(64);
            entity.HasOne<PaymentAttempt>().WithMany().HasForeignKey(x => x.PaymentAttemptId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<VerifiedPaymentReceipt>(entity =>
        {
            entity.ToTable("verified_receipts", "payments"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SourceProviderEventId).IsUnique(); entity.HasIndex(x => new { x.ProviderKey, x.ProviderTransactionId }).IsUnique();
            entity.Property(x => x.ProviderKey).HasMaxLength(40); entity.Property(x => x.ProviderTransactionId).HasMaxLength(160);
            entity.Property(x => x.Amount).HasPrecision(18, 2); entity.Property(x => x.Currency).HasMaxLength(3);
            entity.HasOne<PaymentAttempt>().WithMany().HasForeignKey(x => x.PaymentAttemptId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PaymentProviderEvent>().WithOne().HasForeignKey<VerifiedPaymentReceipt>(x => x.SourceProviderEventId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<OrderPaymentApplication>(entity =>
        {
            entity.ToTable("order_payment_applications", "payments"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderId).IsUnique(); entity.HasIndex(x => x.VerifiedPaymentReceiptId).IsUnique();
            entity.HasOne<Order>().WithOne().HasForeignKey<OrderPaymentApplication>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<VerifiedPaymentReceipt>().WithOne().HasForeignKey<OrderPaymentApplication>(x => x.VerifiedPaymentReceiptId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PaymentReconciliationCase>(entity =>
        {
            entity.ToTable("reconciliation_cases", "payments"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.VerifiedPaymentReceiptId).IsUnique(); entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            entity.Property(x => x.ReasonCode).HasMaxLength(80); entity.Property(x => x.Details).HasMaxLength(1_000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<VerifiedPaymentReceipt>().WithOne().HasForeignKey<PaymentReconciliationCase>(x => x.VerifiedPaymentReceiptId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ManagedContentPage>(entity =>
        {
            entity.ToTable("content_pages", "administration"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Slug).HasMaxLength(120); entity.Property(x => x.Title).HasMaxLength(200); entity.Property(x => x.Body).HasMaxLength(10_000);
            entity.Property(x => x.Version).IsConcurrencyToken();
        });
        builder.Entity<SiteBanner>(entity =>
        {
            entity.ToTable("site_banners", "administration"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.IsEnabled, x.StartsAtUtc });
            entity.Property(x => x.Message).HasMaxLength(500); entity.Property(x => x.Version).IsConcurrencyToken();
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
        if (ChangeTracker.Entries().Any(entry =>
                entry.Entity is PaymentProviderEvent or VerifiedPaymentReceipt or OrderPaymentApplication or OwnershipTransfer
                && entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Payment facts and ownership transfers are append-only.");
        }
    }
}
