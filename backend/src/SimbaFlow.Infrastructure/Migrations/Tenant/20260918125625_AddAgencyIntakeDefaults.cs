using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddAgencyIntakeDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agency_intake_defaults",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    singleton_key = table.Column<int>(type: "integer", nullable: false),
                    gender = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    occupation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    religion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nationality = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    passport_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    marital_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    country_of_travel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    contract_period = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cv_template = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cooking_level = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agency_intake_defaults", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agency_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    built_in_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_built_in = table.Column<bool>(type: "boolean", nullable: false),
                    is_default_selected = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_agency_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_selected = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_skills_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agency_intake_defaults_singleton_key",
                table: "agency_intake_defaults",
                column: "singleton_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agency_skills_built_in_key",
                table: "agency_skills",
                column: "built_in_key",
                unique: true,
                filter: "built_in_key IS NOT NULL AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_agency_skills_name",
                table: "agency_skills",
                column: "name",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_skills_candidate_id_name",
                table: "candidate_skills",
                columns: new[] { "candidate_id", "name" },
                unique: true,
                filter: "is_deleted = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agency_intake_defaults");

            migrationBuilder.DropTable(
                name: "agency_skills");

            migrationBuilder.DropTable(
                name: "candidate_skills");
        }
    }
}
