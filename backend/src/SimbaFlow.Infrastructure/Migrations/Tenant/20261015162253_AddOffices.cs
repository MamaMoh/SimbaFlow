using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddOffices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "office_id",
                table: "candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "offices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidates_office_id",
                table: "candidates",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_offices_name",
                table: "offices",
                column: "name",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.AddForeignKey(
                name: "fk_candidates_offices_office_id",
                table: "candidates",
                column: "office_id",
                principalTable: "offices",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_candidates_offices_office_id",
                table: "candidates");

            migrationBuilder.DropTable(
                name: "offices");

            migrationBuilder.DropIndex(
                name: "ix_candidates_office_id",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "office_id",
                table: "candidates");
        }
    }
}
