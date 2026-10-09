using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Work experience becomes a list, the application number goes, and a removed candidate
    /// keeps the name of whoever removed them.
    ///
    /// Written by hand over what the scaffolder produced, which saw application_no disappear and
    /// deleted_by appear and guessed that one had been renamed to the other. That would have put
    /// "APP-20260918-0007" in the deleted-by column of every candidate on the books.
    ///
    /// Two backfills run before the column is dropped, because one of them reads it:
    ///
    /// The sample candidates were marked by an SMP- application number, which is how "Remove
    /// sample data" found them again. That marker moves to the reference number, or sixteen
    /// demonstration records become permanent.
    ///
    /// A candidate with a country and a term on the old single-value columns gets one row in the
    /// new table, so the form opens showing the history the record already had rather than a
    /// blank list that would overwrite it on the next save. Including the ones whose recorded
    /// country matches where they are now being sent — someone who worked in Saudi Arabia and is
    /// going back there has exactly that history, and dropping it as a duplicate of the
    /// destination would delete the most common case on this desk.
    /// </summary>
    public partial class AddWorkExperienceAndRemovalAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_work_experiences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    occupation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    years = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_work_experiences", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_work_experiences_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_work_experiences_candidate_id",
                table: "candidate_work_experiences",
                column: "candidate_id");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE candidates
                SET reference_no = application_no
                WHERE application_no LIKE 'SMP-%'
                  AND (reference_no IS NULL OR reference_no = '');");

            migrationBuilder.Sql(@"
                INSERT INTO candidate_work_experiences
                    (id, candidate_id, country, occupation, years, sort_order,
                     created_at, is_deleted)
                SELECT gen_random_uuid(), c.id, btrim(c.works_in), c.occupation,
                       c.experience_abroad_years, 0, now() AT TIME ZONE 'utc', FALSE
                FROM candidates c
                WHERE c.works_in IS NOT NULL
                  AND btrim(c.works_in) <> '';");

            migrationBuilder.DropColumn(
                name: "application_no",
                table: "candidates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "application_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "candidates");

            migrationBuilder.DropTable(
                name: "candidate_work_experiences");
        }
    }
}
