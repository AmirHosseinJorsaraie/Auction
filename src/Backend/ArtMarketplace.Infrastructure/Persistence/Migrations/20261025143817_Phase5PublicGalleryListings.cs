using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5PublicGalleryListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_marketplace_listings_ArtworkId",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.RenameColumn(
                name: "SellerEligibilityVersion",
                schema: "marketplace",
                table: "marketplace_listings",
                newName: "SellerEligibilityVersionAtPublish");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAtUtc",
                schema: "marketplace",
                table: "marketplace_listings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "marketplace",
                table: "marketplace_listings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                schema: "marketplace",
                table: "marketplace_listings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "marketplace",
                table: "marketplace_listings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql("UPDATE marketplace.marketplace_listings SET \"UpdatedAtUtc\" = \"CreatedAtUtc\"");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_listings_ArtworkId",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "ArtworkId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'Published', 'Suspended', 'Reserved', 'Sold')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_marketplace_listings_ArtworkId",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.RenameColumn(
                name: "SellerEligibilityVersionAtPublish",
                schema: "marketplace",
                table: "marketplace_listings",
                newName: "SellerEligibilityVersion");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_listings_ArtworkId",
                schema: "marketplace",
                table: "marketplace_listings",
                column: "ArtworkId",
                unique: true,
                filter: "\"Status\" = 'Draft'");
        }
    }
}
