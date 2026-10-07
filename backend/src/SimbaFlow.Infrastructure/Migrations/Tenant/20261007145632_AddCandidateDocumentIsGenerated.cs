using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Marks the documents we drew ourselves, so that printing one again replaces the last copy
    /// without touching anything a person uploaded.
    ///
    /// Everything already on file defaults to "uploaded", which is the safe direction: the worst
    /// it costs is one stale generated copy left behind. Getting it the other way round would let
    /// the next press of Generate delete a signed contract. The backfill below recovers the
    /// generated ones anyway, by the name their generator stored them under — a kind, a passport
    /// number and a fourteen-digit timestamp, which is specific enough that a file a person named
    /// does not collide with it.
    /// </summary>
    public partial class AddCandidateDocumentIsGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_generated",
                table: "candidate_documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE candidate_documents
                SET is_generated = true
                WHERE file_name ~ '_(cv|contract|visa|tasheer)_.*_[0-9]{14}\.pdf$'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_generated",
                table: "candidate_documents");
        }
    }
}
