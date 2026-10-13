using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <summary>
    /// Several Telegram chats per account instead of one.
    ///
    /// The order below is not what the scaffolder produced: the table is created, the existing
    /// links are copied into it, and only then does the old column go. Dropping it first — which
    /// is what a generated migration does — would take every linked phone in production with it.
    /// </summary>
    public partial class AddBotChatLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotChatLinks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TelegramUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotChatLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotChatLinks_ChatId",
                schema: "public",
                table: "BotChatLinks",
                column: "ChatId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_BotChatLinks_UserId",
                schema: "public",
                table: "BotChatLinks",
                column: "UserId");

            // Everybody linked today keeps their link. Without this, every member of staff with
            // the bot on their phone goes silent at deploy, with nothing to tell them why and no
            // way back except a fresh code each.
            migrationBuilder.Sql("""
                INSERT INTO public."BotChatLinks"
                    ("Id", "UserId", "ChatId", "LinkedAt", "CreatedAt", "IsDeleted")
                SELECT gen_random_uuid(), u."Id", u."TelegramChatId", now(), now(), false
                FROM public."AspNetUsers" u
                WHERE u."TelegramChatId" IS NOT NULL
                  AND u."TelegramChatId" <> ''
                  AND u."BotLinked" = true
                  AND u."IsDeleted" = false
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TelegramChatId",
                schema: "public",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BotLinked",
                schema: "public",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                schema: "public",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BotLinked",
                schema: "public",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                schema: "public",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TelegramChatId",
                schema: "public",
                table: "AspNetUsers",
                column: "TelegramChatId");

            // Rolling back, one chat per account is all the old column can hold, so the oldest
            // link is the one kept — the phone that was linked first. Any later ones are lost,
            // which is the shape of the column and not something this can work around.
            migrationBuilder.Sql("""
                UPDATE public."AspNetUsers" u
                SET "TelegramChatId" = l."ChatId", "BotLinked" = true
                FROM (
                    SELECT DISTINCT ON ("UserId") "UserId", "ChatId"
                    FROM public."BotChatLinks"
                    WHERE "IsDeleted" = false
                    ORDER BY "UserId", "LinkedAt"
                ) l
                WHERE l."UserId" = u."Id";
                """);

            migrationBuilder.DropTable(
                name: "BotChatLinks",
                schema: "public");
        }
    }
}
