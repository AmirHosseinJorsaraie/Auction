using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6CartCheckoutOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "orders");

            migrationBuilder.CreateTable(
                name: "carts",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SellerEligibilityVersionAtCheckout = table.Column<long>(type: "bigint", nullable: false),
                    ReservationExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewIntegrityHold = table.Column<bool>(type: "boolean", nullable: false),
                    SellerEligibilityHold = table.Column<bool>(type: "boolean", nullable: false),
                    HoldReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TrackingCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.CheckConstraint("CK_orders_positive_total", "\"TotalAmount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceAmountAtAdd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyAtAdd = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cart_items_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "orders",
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cart_items_marketplace_listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtworkRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PriceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SellerDisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_artwork_revisions_ArtworkRevisionId",
                        column: x => x.ArtworkRevisionId,
                        principalSchema: "catalog",
                        principalTable: "artwork_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalSchema: "catalog",
                        principalTable: "artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_marketplace_listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TerminalAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reservations_marketplace_listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "marketplace",
                        principalTable: "marketplace_listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservations_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "orders",
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservations_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_CartId_ListingId",
                schema: "orders",
                table: "cart_items",
                columns: new[] { "CartId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_ListingId",
                schema: "orders",
                table: "cart_items",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_carts_BuyerUserId",
                schema: "orders",
                table: "carts",
                column: "BuyerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ArtworkId",
                schema: "orders",
                table: "order_items",
                column: "ArtworkId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ArtworkRevisionId",
                schema: "orders",
                table: "order_items",
                column: "ArtworkRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ListingId",
                schema: "orders",
                table: "order_items",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId_ListingId",
                schema: "orders",
                table: "order_items",
                columns: new[] { "OrderId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_BuyerUserId_IdempotencyKey",
                schema: "orders",
                table: "orders",
                columns: new[] { "BuyerUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_SellerUserId",
                schema: "orders",
                table: "orders",
                column: "SellerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_ReservationExpiresAtUtc",
                schema: "orders",
                table: "orders",
                columns: new[] { "Status", "ReservationExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ListingId",
                schema: "orders",
                table: "reservations",
                column: "ListingId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_OrderId_Status",
                schema: "orders",
                table: "reservations",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_OrderItemId",
                schema: "orders",
                table: "reservations",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_Token",
                schema: "orders",
                table: "reservations",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_items",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "carts",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "orders");
        }
    }
}
