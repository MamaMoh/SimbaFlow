using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPaymentVoidAndReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "receipt_number",
                table: "payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "void_journal_entry_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "void_reason",
                table: "payments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "voided_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "voided_by_user_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "next_receipt_number",
                table: "finance_counters",
                type: "integer",
                nullable: false,
                // Receipts start at RC-000001; the startup backfill numbers existing payments.
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receipt_number",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "void_journal_entry_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "void_reason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "voided_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "voided_by_user_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "next_receipt_number",
                table: "finance_counters");
        }
    }
}
