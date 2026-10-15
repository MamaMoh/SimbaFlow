using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddTenantOffboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastExportedAt",
                schema: "public",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OffboardedAt",
                schema: "public",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OffboardedBy",
                schema: "public",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OffboardingReason",
                schema: "public",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurgedAt",
                schema: "public",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            // Agencies removed with the old Delete were hidden, not removed: their schemas, files
            // and accounts are all still there. Bring them back into the list as offboarded so they
            // can be exported, reactivated or purged like any other.
            migrationBuilder.Sql("""
                UPDATE public."Tenants"
                SET "IsDeleted" = false,
                    "OffboardedAt" = COALESCE("UpdatedAt", now()),
                    "OffboardingReason" = 'Deleted from the Agencies page before offboarding existed'
                WHERE "IsDeleted" = true AND "SubscriptionStatus" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastExportedAt",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OffboardedAt",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OffboardedBy",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OffboardingReason",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PurgedAt",
                schema: "public",
                table: "Tenants");
        }
    }
}
