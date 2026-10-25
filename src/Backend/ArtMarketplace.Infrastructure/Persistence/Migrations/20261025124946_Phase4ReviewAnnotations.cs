using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861

#nullable disable

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ReviewAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_annotations",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_annotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_annotations_review_decisions_ReviewDecisionId",
                        column: x => x.ReviewDecisionId,
                        principalSchema: "reviews",
                        principalTable: "review_decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_review_annotations_ReviewDecisionId_CreatedAtUtc",
                schema: "reviews",
                table: "review_annotations",
                columns: new[] { "ReviewDecisionId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_annotations",
                schema: "reviews");
        }
    }
}
