using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtMarketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6OrderConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_positive_eligibility_version",
                schema: "orders",
                table: "orders",
                sql: "\"SellerEligibilityVersionAtCheckout\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_order_items_positive_price",
                schema: "orders",
                table: "order_items",
                sql: "\"PriceAmount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_cart_items_positive_price",
                schema: "orders",
                table: "cart_items",
                sql: "\"PriceAmountAtAdd\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_positive_eligibility_version",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_order_items_positive_price",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_cart_items_positive_price",
                schema: "orders",
                table: "cart_items");
        }
    }
}
