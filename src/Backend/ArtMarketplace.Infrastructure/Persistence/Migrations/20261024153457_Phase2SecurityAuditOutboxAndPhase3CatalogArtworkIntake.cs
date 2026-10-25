using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Generated migration uses immutable column-name arrays.

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3CatalogArtworkIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "marketplace");

            migrationBuilder.EnsureSchema(
                name: "reviews");

            migrationBuilder.CreateTable(
                name: "artists",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Biography = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "artworks",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentOwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InternalCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_listings",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SellerEligibilityVersion = table.Column<long>(type: "bigint", nullable: false),
                    PublishedArtworkRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_listings", x => x.Id);
                    table.CheckConstraint("CK_marketplace_listings_positive_price", "\"PriceAmount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "review_cases",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmitterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_cases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "review_rounds",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_rounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "upload_intents",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpectedSize = table.Column<long>(type: "bigint", nullable: false),
                    QuarantineObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_intents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "artwork_revisions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CreatedByOwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtistAttribution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Medium = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dimensions = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Condition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DiscardReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FrozenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artwork_revisions_artists_ArtistId",
                        column: x => x.ArtistId,
                        principalSchema: "catalog",
                        principalTable: "artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artwork_revisions_artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalSchema: "catalog",
                        principalTable: "artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artwork_revisions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "artwork_assets",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CleanObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VersionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublicDerivativeObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artwork_assets_artwork_revisions_ArtworkRevisionId",
                        column: x => x.ArtworkRevisionId,
                        principalSchema: "catalog",
                        principalTable: "artwork_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artists_Slug",
                schema: "catalog",
                table: "artists",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artwork_assets_ArtworkRevisionId_Kind",
                schema: "catalog",
                table: "artwork_assets",
                columns: new[] { "ArtworkRevisionId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_artwork_assets_CleanObjectKey",
                schema: "catalog",
                table: "artwork_assets",
                column: "CleanObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artwork_assets_PublicDerivativeObjectKey",
                schema: "catalog",
                table: "artwork_assets",
                column: "PublicDerivativeObjectKey",
                unique: true,
                filter: "\"PublicDerivativeObjectKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_artwork_revisions_ArtistId",
                schema: "catalog",
                table: "artwork_revisions",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_artwork_revisions_ArtworkId",
                schema: "catalog",
                table: "artwork_revisions",
                column: "ArtworkId",
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_artwork_revisions_ArtworkId_Number",
                schema: "catalog",
                table: "artwork_revisions",
                columns: new[] { "ArtworkId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artwork_revisions_CategoryId",
                schema: "catalog",
                table: "artwork_revisions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_artworks_CurrentOwnerUserId",
                schema: "catalog",
                table: "artworks",
                column: "CurrentOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_artworks_InternalCode",
                schema: "catalog",
                table: "artworks",
                column: "InternalCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_Slug",
                schema: "catalog",
                table: "categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_listings_ArtworkId",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "ArtworkId",
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_review_cases_ArtworkId",
                schema: "reviews",
                table: "review_cases",
                column: "ArtworkId",
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_review_rounds_ReviewCaseId",
                schema: "reviews",
                table: "review_rounds",
                column: "ReviewCaseId",
                unique: true,
                filter: "\"Status\" = 'PendingReview'");

            migrationBuilder.CreateIndex(
                name: "IX_review_rounds_ReviewCaseId_Number",
                schema: "reviews",
                table: "review_rounds",
                columns: new[] { "ReviewCaseId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upload_intents_QuarantineObjectKey",
                schema: "catalog",
                table: "upload_intents",
                column: "QuarantineObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upload_intents_UserId_CreatedAtUtc",
                schema: "catalog",
                table: "upload_intents",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artwork_assets",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "marketplace_listings",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "review_cases",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review_rounds",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "upload_intents",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "artwork_revisions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "artists",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "artworks",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");
        }
    }
}
