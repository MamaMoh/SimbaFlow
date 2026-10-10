using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// The reference number goes, and sample data gets a flag of its own.
    ///
    /// The demonstration candidates were marked by an SMP- prefix on a field the desk could also
    /// type into — first the application number, then the reference number — and each time one
    /// of those fields was dropped, "Remove sample data" lost the only way it had of finding
    /// them. Sixteen demonstration records then stayed on the books for good. The flag is set
    /// from the old prefix before the column goes, so the ones already seeded are still
    /// removable, and nothing can drop it by accident again.
    /// </summary>
    public partial class RemoveReferenceNoAddSampleFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_sample_data",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Before the drop, because it reads the column being dropped.
            migrationBuilder.Sql(@"
                UPDATE candidates
                SET is_sample_data = TRUE
                WHERE reference_no LIKE 'SMP-%';");

            migrationBuilder.DropColumn(
                name: "reference_no",
                table: "candidates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_sample_data",
                table: "candidates");

            migrationBuilder.AddColumn<string>(
                name: "reference_no",
                table: "candidates",
                type: "text",
                nullable: true);
        }
    }
}
