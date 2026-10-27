using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates small index column arrays in migration methods.

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8AdministrationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectUserId",
                schema: "identity",
                table: "bff_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE identity.bff_sessions
                SET "RevokedAtUtc" = COALESCE("RevokedAtUtc", NOW()), "Version" = "Version" + 1
                WHERE "SubjectUserId" IS NULL AND "RevokedAtUtc" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "content_pages",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_pages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "site_banners",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_banners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bff_sessions_SubjectUserId_RevokedAtUtc",
                schema: "identity",
                table: "bff_sessions",
                columns: new[] { "SubjectUserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_content_pages_Slug",
                schema: "administration",
                table: "content_pages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_banners_IsEnabled_StartsAtUtc",
                schema: "administration",
                table: "site_banners",
                columns: new[] { "IsEnabled", "StartsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_pages",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "site_banners",
                schema: "administration");

            migrationBuilder.DropIndex(
                name: "IX_bff_sessions_SubjectUserId_RevokedAtUtc",
                schema: "identity",
                table: "bff_sessions");

            migrationBuilder.DropColumn(
                name: "SubjectUserId",
                schema: "identity",
                table: "bff_sessions");
        }
    }
}
