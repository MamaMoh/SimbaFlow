using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RenameOfficeNameToPartnerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_candidates_office_id",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "office_id",
                table: "candidates");

            migrationBuilder.RenameColumn(
                name: "office_name",
                table: "commissions",
                newName: "partner_name");

            migrationBuilder.RenameColumn(
                name: "office_name",
                table: "candidates",
                newName: "partner_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "partner_name",
                table: "commissions",
                newName: "office_name");

            migrationBuilder.RenameColumn(
                name: "partner_name",
                table: "candidates",
                newName: "office_name");

            migrationBuilder.AddColumn<Guid>(
                name: "office_id",
                table: "candidates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_candidates_office_id",
                table: "candidates",
                column: "office_id");
        }
    }
}
