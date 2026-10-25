using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861

#nullable disable

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ExpertReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAtUtc",
                schema: "reviews",
                table: "review_rounds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "reviews",
                table: "review_rounds",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                schema: "reviews",
                table: "review_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "review_assignments",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_assignments_review_rounds_ReviewRoundId",
                        column: x => x.ReviewRoundId,
                        principalSchema: "reviews",
                        principalTable: "review_rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_conflicts",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_conflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_conflicts_artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalSchema: "catalog",
                        principalTable: "artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_decisions",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PublicComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PrivateNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_decisions_artwork_revisions_ArtworkRevisionId",
                        column: x => x.ArtworkRevisionId,
                        principalSchema: "catalog",
                        principalTable: "artwork_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_decisions_review_cases_ReviewCaseId",
                        column: x => x.ReviewCaseId,
                        principalSchema: "reviews",
                        principalTable: "review_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_decisions_review_rounds_ReviewRoundId",
                        column: x => x.ReviewRoundId,
                        principalSchema: "reviews",
                        principalTable: "review_rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_findings",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_findings_review_rounds_ReviewRoundId",
                        column: x => x.ReviewRoundId,
                        principalSchema: "reviews",
                        principalTable: "review_rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_decision_supersessions",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_decision_supersessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_decision_supersessions_review_decisions_ReviewDecisi~",
                        column: x => x.ReviewDecisionId,
                        principalSchema: "reviews",
                        principalTable: "review_decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_ExpertUserId",
                schema: "reviews",
                table: "review_assignments",
                column: "ExpertUserId");

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_ReviewRoundId",
                schema: "reviews",
                table: "review_assignments",
                column: "ReviewRoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_conflicts_ArtworkId_ExpertUserId",
                schema: "reviews",
                table: "review_conflicts",
                columns: new[] { "ArtworkId", "ExpertUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_decision_supersessions_ReviewDecisionId",
                schema: "reviews",
                table: "review_decision_supersessions",
                column: "ReviewDecisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_decisions_ArtworkRevisionId_CreatedAtUtc",
                schema: "reviews",
                table: "review_decisions",
                columns: new[] { "ArtworkRevisionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_review_decisions_ReviewCaseId",
                schema: "reviews",
                table: "review_decisions",
                column: "ReviewCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_review_decisions_ReviewRoundId",
                schema: "reviews",
                table: "review_decisions",
                column: "ReviewRoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_findings_ReviewRoundId_CreatedAtUtc",
                schema: "reviews",
                table: "review_findings",
                columns: new[] { "ReviewRoundId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_assignments",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review_conflicts",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review_decision_supersessions",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review_findings",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review_decisions",
                schema: "reviews");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                schema: "reviews",
                table: "review_rounds");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "reviews",
                table: "review_rounds");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                schema: "reviews",
                table: "review_cases");
        }
    }
}
