using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates small index column arrays in migration methods.

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7PaymentAbstraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "payment_attempts",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MerchantReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MerchantAccount = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    RedirectUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_attempts_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_events",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MerchantReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MerchantAccount = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderOccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_events_payment_attempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalSchema: "payments",
                        principalTable: "payment_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "verified_receipts",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProviderEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verified_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_verified_receipts_payment_attempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalSchema: "payments",
                        principalTable: "payment_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_verified_receipts_provider_events_SourceProviderEventId",
                        column: x => x.SourceProviderEventId,
                        principalSchema: "payments",
                        principalTable: "provider_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_payment_applications",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedPaymentReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_payment_applications_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_payment_applications_verified_receipts_VerifiedPaymen~",
                        column: x => x.VerifiedPaymentReceiptId,
                        principalSchema: "payments",
                        principalTable: "verified_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_cases",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedPaymentReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reconciliation_cases_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_cases_verified_receipts_VerifiedPaymentRecei~",
                        column: x => x.VerifiedPaymentReceiptId,
                        principalSchema: "payments",
                        principalTable: "verified_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ownership_transfers",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderPaymentApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ownership_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalSchema: "catalog",
                        principalTable: "artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_marketplace_listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "orders",
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_order_payment_applications_OrderPayment~",
                        column: x => x.OrderPaymentApplicationId,
                        principalSchema: "payments",
                        principalTable: "order_payment_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_applications_OrderId",
                schema: "payments",
                table: "order_payment_applications",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_applications_VerifiedPaymentReceiptId",
                schema: "payments",
                table: "order_payment_applications",
                column: "VerifiedPaymentReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_ArtworkId",
                schema: "orders",
                table: "ownership_transfers",
                column: "ArtworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_ListingId",
                schema: "orders",
                table: "ownership_transfers",
                column: "ListingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_OrderId",
                schema: "orders",
                table: "ownership_transfers",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_OrderItemId",
                schema: "orders",
                table: "ownership_transfers",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_OrderPaymentApplicationId",
                schema: "orders",
                table: "ownership_transfers",
                column: "OrderPaymentApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_OrderId_IdempotencyKey",
                schema: "payments",
                table: "payment_attempts",
                columns: new[] { "OrderId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_ProviderKey_MerchantReference",
                schema: "payments",
                table: "payment_attempts",
                columns: new[] { "ProviderKey", "MerchantReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_ProviderKey_ProviderTransactionId",
                schema: "payments",
                table: "payment_attempts",
                columns: new[] { "ProviderKey", "ProviderTransactionId" },
                unique: true,
                filter: "\"ProviderTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_provider_events_PaymentAttemptId",
                schema: "payments",
                table: "provider_events",
                column: "PaymentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_events_ProviderKey_EventId",
                schema: "payments",
                table: "provider_events",
                columns: new[] { "ProviderKey", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_cases_OrderId",
                schema: "payments",
                table: "reconciliation_cases",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_cases_Status_CreatedAtUtc",
                schema: "payments",
                table: "reconciliation_cases",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_cases_VerifiedPaymentReceiptId",
                schema: "payments",
                table: "reconciliation_cases",
                column: "VerifiedPaymentReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_verified_receipts_PaymentAttemptId",
                schema: "payments",
                table: "verified_receipts",
                column: "PaymentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_verified_receipts_ProviderKey_ProviderTransactionId",
                schema: "payments",
                table: "verified_receipts",
                columns: new[] { "ProviderKey", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_verified_receipts_SourceProviderEventId",
                schema: "payments",
                table: "verified_receipts",
                column: "SourceProviderEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ownership_transfers",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "reconciliation_cases",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "order_payment_applications",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "verified_receipts",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "provider_events",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "payment_attempts",
                schema: "payments");
        }
    }
}
