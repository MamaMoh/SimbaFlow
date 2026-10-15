using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddAuditTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "public",
                table: "ReadAuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "public",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            // Rows written before this column existed belong to whichever agency their author
            // belongs to. A platform administrator's old rows have no agency to give them, so they
            // stay null and out of every agency's trail.
            migrationBuilder.Sql("""
                UPDATE public."AuditLogs" AS a
                SET "TenantId" = u."TenantId"
                FROM public."AspNetUsers" AS u
                WHERE a."TenantId" IS NULL AND a."UserId" = u."Id"::text AND u."TenantId" IS NOT NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE public."ReadAuditLogs" AS r
                SET "TenantId" = u."TenantId"
                FROM public."AspNetUsers" AS u
                WHERE r."TenantId" IS NULL AND r."UserId" = u."Id"::text AND u."TenantId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ReadAuditLogs_TenantId_AccessedAt",
                schema: "public",
                table: "ReadAuditLogs",
                columns: new[] { "TenantId", "AccessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                schema: "public",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReadAuditLogs_TenantId_AccessedAt",
                schema: "public",
                table: "ReadAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                schema: "public",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "ReadAuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "AuditLogs");
        }
    }
}
