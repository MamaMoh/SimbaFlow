using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCandidateStageEnteredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "stage_entered_at",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidates_current_stage_id_stage_entered_at",
                table: "candidates",
                columns: new[] { "current_stage_id", "stage_entered_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_candidates_current_stage_id_stage_entered_at",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "stage_entered_at",
                table: "candidates");
        }
    }
}
