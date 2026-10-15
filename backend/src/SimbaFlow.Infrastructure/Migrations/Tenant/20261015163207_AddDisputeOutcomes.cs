using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddDisputeOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "adjustment_amount_etb",
                table: "disputes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "adjustment_journal_entry_id",
                table: "disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "disputed_amount_etb",
                table: "disputes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "outcome",
                table: "disputes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_adjusted_amount",
                table: "commissions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adjustment_amount_etb",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "adjustment_journal_entry_id",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "disputed_amount_etb",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "outcome",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "total_adjusted_amount",
                table: "commissions");
        }
    }
}
