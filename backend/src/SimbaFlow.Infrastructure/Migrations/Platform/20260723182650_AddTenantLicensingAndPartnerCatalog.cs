using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddTenantLicensingAndPartnerCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgencyLevel",
                schema: "public",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<decimal>(
                name: "BondUsd",
                schema: "public",
                table: "Tenants",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapitalEtb",
                schema: "public",
                table: "Tenants",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenseExpiresAt",
                schema: "public",
                table: "Tenants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenseIssuedAt",
                schema: "public",
                table: "Tenants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                schema: "public",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LicenseStatus",
                schema: "public",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LicensedCountries",
                schema: "public",
                table: "Tenants",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "PartnerAgencies",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CountryName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ForeignLicenseId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CapacityTier = table.Column<int>(type: "integer", nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerAgencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerLinks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerAgencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementStart = table.Column<DateOnly>(type: "date", nullable: false),
                    AgreementEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerLinks_PartnerAgencies_PartnerAgencyId",
                        column: x => x.PartnerAgencyId,
                        principalSchema: "public",
                        principalTable: "PartnerAgencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_AgencyLevel",
                schema: "public",
                table: "Tenants",
                column: "AgencyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerAgencies_CountryCode",
                schema: "public",
                table: "PartnerAgencies",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerAgencies_Name",
                schema: "public",
                table: "PartnerAgencies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerLinks_PartnerAgencyId",
                schema: "public",
                table: "PartnerLinks",
                column: "PartnerAgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerLinks_TenantId_PartnerAgencyId",
                schema: "public",
                table: "PartnerLinks",
                columns: new[] { "TenantId", "PartnerAgencyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerLinks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PartnerAgencies",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_AgencyLevel",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AgencyLevel",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BondUsd",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CapitalEtb",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LicenseExpiresAt",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LicenseIssuedAt",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LicenseStatus",
                schema: "public",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LicensedCountries",
                schema: "public",
                table: "Tenants");
        }
    }
}
