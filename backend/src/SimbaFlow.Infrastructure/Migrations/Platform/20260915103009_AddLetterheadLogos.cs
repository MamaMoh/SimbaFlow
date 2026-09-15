using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddLetterheadLogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                schema: "public",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                schema: "public",
                table: "PartnerAgencies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoPath",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LogoPath",
                schema: "public",
                table: "PartnerAgencies");
        }
    }
}
