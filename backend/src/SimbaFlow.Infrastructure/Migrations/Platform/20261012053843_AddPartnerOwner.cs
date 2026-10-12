using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <inheritdoc />
    public partial class AddPartnerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerTenantId",
                schema: "public",
                table: "PartnerAgencies",
                type: "uuid",
                nullable: true);

            // Every partner already registered belongs to whoever first linked it — that is the
            // agency that put it there. Without this backfill the column is null everywhere and
            // every agency's partner list empties the moment the list starts filtering by owner.
            //
            // A partner nobody ever linked keeps a null owner: it is a seeded row, and no
            // agency's list claims it.
            migrationBuilder.Sql("""
                UPDATE public."PartnerAgencies" p
                SET "OwnerTenantId" = first_link."TenantId"
                FROM (
                    SELECT DISTINCT ON ("PartnerAgencyId") "PartnerAgencyId", "TenantId"
                    FROM public."PartnerLinks"
                    WHERE "IsDeleted" = false
                    ORDER BY "PartnerAgencyId", "CreatedAt"
                ) AS first_link
                WHERE p."Id" = first_link."PartnerAgencyId"
                  AND p."OwnerTenantId" IS NULL;
                """);

            // One name per agency, enforced where it cannot be raced past: the check in the
            // handler reads before it writes, so two registrations landing together both pass it.
            //
            // Created only if the data allows it. An agency that already holds the same name
            // twice — possible, because the old rule compared name *and* address — would fail
            // the index and take the whole deployment down with it. Where that happens the
            // handler check stands alone until the duplicates are merged, which is a worse
            // guarantee and a great deal better than a migration that cannot run.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM public."PartnerAgencies"
                        WHERE "IsDeleted" = false AND "OwnerTenantId" IS NOT NULL
                        GROUP BY "OwnerTenantId", upper(btrim("Name"))
                        HAVING count(*) > 1
                    ) THEN
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartnerAgencies_Owner_Name"
                        ON public."PartnerAgencies" ("OwnerTenantId", upper(btrim("Name")))
                        WHERE "IsDeleted" = false AND "OwnerTenantId" IS NOT NULL;
                    ELSE
                        RAISE WARNING 'Partner names are not unique per agency; the unique index was skipped.';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS public."IX_PartnerAgencies_Owner_Name";""");

            migrationBuilder.DropColumn(
                name: "OwnerTenantId",
                schema: "public",
                table: "PartnerAgencies");
        }
    }
}
