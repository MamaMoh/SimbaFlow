using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCvFormFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "complexion",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "skill_arabic_cooking",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_computer",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_tutoring",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "complexion",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_arabic_cooking",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_computer",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_tutoring",
                table: "candidates");
        }
    }
}
