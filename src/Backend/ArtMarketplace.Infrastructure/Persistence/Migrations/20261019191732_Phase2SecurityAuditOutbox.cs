using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace ArtMarketplace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MarketplaceDbContext))]
[Migration("20261019191732_Phase2SecurityAuditOutbox")]
public sealed class Phase2SecurityAuditOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "platform");
        migrationBuilder.CreateTable(name: "bff_sessions", schema: "identity", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), Surface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            HandleDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false), ProtectedTicket = table.Column<byte[]>(type: "bytea", nullable: false),
            CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false), ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true), Version = table.Column<long>(type: "bigint", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_bff_sessions", x => x.Id));
        migrationBuilder.CreateTable(name: "outbox_messages", schema: "platform", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
            PayloadJson = table.Column<string>(type: "jsonb", nullable: false), OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true), Attempts = table.Column<int>(type: "integer", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_outbox_messages", x => x.Id));
        migrationBuilder.CreateTable(name: "security_audit_events", schema: "identity", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            ActorUserId = table.Column<Guid>(type: "uuid", nullable: false), SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false), DataJson = table.Column<string>(type: "jsonb", nullable: false),
            TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true), CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_security_audit_events", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_bff_sessions_ExpiresAtUtc", schema: "identity", table: "bff_sessions", column: "ExpiresAtUtc");
        migrationBuilder.CreateIndex(name: "IX_bff_sessions_Surface_HandleDigest", schema: "identity", table: "bff_sessions", columns: new[] { "Surface", "HandleDigest" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_outbox_messages_PublishedAtUtc_OccurredAtUtc", schema: "platform", table: "outbox_messages", columns: new[] { "PublishedAtUtc", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_security_audit_events_SubjectUserId_CreatedAtUtc", schema: "identity", table: "security_audit_events", columns: new[] { "SubjectUserId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bff_sessions", schema: "identity");
        migrationBuilder.DropTable(name: "outbox_messages", schema: "platform");
        migrationBuilder.DropTable(name: "security_audit_events", schema: "identity");
    }
}
