using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtMarketplace.Infrastructure.Persistence.Migrations;

[Migration("202610170001_InitialFoundation")]
public sealed class InitialFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "platform");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS platform;");
    }
}
