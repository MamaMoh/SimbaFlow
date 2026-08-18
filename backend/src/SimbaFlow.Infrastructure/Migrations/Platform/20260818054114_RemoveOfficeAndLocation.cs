using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <inheritdoc />
    public partial class RemoveOfficeAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffProfiles_Locations_PrimaryLocationId",
                schema: "public",
                table: "StaffProfiles");

            migrationBuilder.DropTable(
                name: "StaffLocationMappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Locations",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_PrimaryLocationId",
                schema: "public",
                table: "StaffProfiles");

            migrationBuilder.DropColumn(
                name: "PrimaryLocationId",
                schema: "public",
                table: "StaffProfiles");

            migrationBuilder.DropColumn(
                name: "ActiveLocationId",
                schema: "public",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OfficeId",
                schema: "public",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryLocationId",
                schema: "public",
                table: "StaffProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveLocationId",
                schema: "public",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeId",
                schema: "public",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Locations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Building = table.Column<string>(type: "text", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Floor = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Locations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalSchema: "public",
                        principalTable: "Locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffLocationMappings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Schedule = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffLocationMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffLocationMappings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "public",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffLocationMappings_StaffProfiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalSchema: "public",
                        principalTable: "StaffProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_PrimaryLocationId",
                schema: "public",
                table: "StaffProfiles",
                column: "PrimaryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId",
                schema: "public",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLocationMappings_LocationId",
                schema: "public",
                table: "StaffLocationMappings",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLocationMappings_StaffProfileId",
                schema: "public",
                table: "StaffLocationMappings",
                column: "StaffProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffProfiles_Locations_PrimaryLocationId",
                schema: "public",
                table: "StaffProfiles",
                column: "PrimaryLocationId",
                principalSchema: "public",
                principalTable: "Locations",
                principalColumn: "Id");
        }
    }
}
