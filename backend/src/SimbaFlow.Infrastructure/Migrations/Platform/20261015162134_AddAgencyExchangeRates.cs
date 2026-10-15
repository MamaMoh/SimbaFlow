using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddAgencyExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "public",
                table: "ExchangeRates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId_FromCurrency_ToCurrency_EffectiveDate",
                schema: "public",
                table: "ExchangeRates",
                columns: new[] { "TenantId", "FromCurrency", "ToCurrency", "EffectiveDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_TenantId_FromCurrency_ToCurrency_EffectiveDate",
                schema: "public",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "ExchangeRates");
        }
    }
}
