using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCandidatePhotoVisaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agent_name",
                table: "candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "full_photo_path",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sponsor_address",
                table: "candidates",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sponsor_arabic_name",
                table: "candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sponsor_id_number",
                table: "candidates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sponsor_name",
                table: "candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sponsor_phone",
                table: "candidates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visa_number",
                table: "candidates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visa_type",
                table: "candidates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agent_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "full_photo_path",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "sponsor_address",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "sponsor_arabic_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "sponsor_id_number",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "sponsor_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "sponsor_phone",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "visa_number",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "visa_type",
                table: "candidates");
        }
    }
}
