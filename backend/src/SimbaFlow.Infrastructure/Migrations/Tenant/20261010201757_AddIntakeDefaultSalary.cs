using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddIntakeDefaultSalary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "1000" rather than "": the registration form has always started at 1000 with the
            // figure hardcoded in it, so an agency that has never seen this setting should find
            // it holding what their form has been doing all along, not emptied out underneath
            // them.
            migrationBuilder.AddColumn<string>(
                name: "monthly_salary",
                table: "agency_intake_defaults",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "1000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "monthly_salary",
                table: "agency_intake_defaults");
        }
    }
}
