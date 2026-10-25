using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3RelationalConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_upload_intents_ArtworkId",
                schema: "catalog",
                table: "upload_intents",
                column: "ArtworkId");

            migrationBuilder.CreateIndex(
                name: "IX_upload_intents_ArtworkRevisionId",
                schema: "catalog",
                table: "upload_intents",
                column: "ArtworkRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_review_rounds_ArtworkRevisionId",
                schema: "reviews",
                table: "review_rounds",
                column: "ArtworkRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_listings_PublishedArtworkRevisionId",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "PublishedArtworkRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_marketplace_listings_artwork_revisions_PublishedArtworkRevi~",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "PublishedArtworkRevisionId",
                principalSchema: "catalog",
                principalTable: "artwork_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_marketplace_listings_artworks_ArtworkId",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "ArtworkId",
                principalSchema: "catalog",
                principalTable: "artworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_review_cases_artworks_ArtworkId",
                schema: "reviews",
                table: "review_cases",
                column: "ArtworkId",
                principalSchema: "catalog",
                principalTable: "artworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_review_rounds_artwork_revisions_ArtworkRevisionId",
                schema: "reviews",
                table: "review_rounds",
                column: "ArtworkRevisionId",
                principalSchema: "catalog",
                principalTable: "artwork_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_review_rounds_review_cases_ReviewCaseId",
                schema: "reviews",
                table: "review_rounds",
                column: "ReviewCaseId",
                principalSchema: "reviews",
                principalTable: "review_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_upload_intents_artwork_revisions_ArtworkRevisionId",
                schema: "catalog",
                table: "upload_intents",
                column: "ArtworkRevisionId",
                principalSchema: "catalog",
                principalTable: "artwork_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_upload_intents_artworks_ArtworkId",
                schema: "catalog",
                table: "upload_intents",
                column: "ArtworkId",
                principalSchema: "catalog",
                principalTable: "artworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_marketplace_listings_artwork_revisions_PublishedArtworkRevi~",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropForeignKey(
                name: "FK_marketplace_listings_artworks_ArtworkId",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropForeignKey(
                name: "FK_review_cases_artworks_ArtworkId",
                schema: "reviews",
                table: "review_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_review_rounds_artwork_revisions_ArtworkRevisionId",
                schema: "reviews",
                table: "review_rounds");

            migrationBuilder.DropForeignKey(
                name: "FK_review_rounds_review_cases_ReviewCaseId",
                schema: "reviews",
                table: "review_rounds");

            migrationBuilder.DropForeignKey(
                name: "FK_upload_intents_artwork_revisions_ArtworkRevisionId",
                schema: "catalog",
                table: "upload_intents");

            migrationBuilder.DropForeignKey(
                name: "FK_upload_intents_artworks_ArtworkId",
                schema: "catalog",
                table: "upload_intents");

            migrationBuilder.DropIndex(
                name: "IX_upload_intents_ArtworkId",
                schema: "catalog",
                table: "upload_intents");

            migrationBuilder.DropIndex(
                name: "IX_upload_intents_ArtworkRevisionId",
                schema: "catalog",
                table: "upload_intents");

            migrationBuilder.DropIndex(
                name: "IX_review_rounds_ArtworkRevisionId",
                schema: "reviews",
                table: "review_rounds");

            migrationBuilder.DropIndex(
                name: "IX_marketplace_listings_PublishedArtworkRevisionId",
                schema: "marketplace",
                table: "marketplace_listings");
        }
    }
}
