using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomax.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateUserProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "user_profiles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    ui_locale = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'id'"),
                    script_pref = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'kanji_furigana'"),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValueSql: "'Asia/Jakarta'"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profiles", x => x.id);
                    table.CheckConstraint("ck_user_profiles_role", "\"role\" IN ('admin', 'sensei', 'murid')");
                    table.CheckConstraint("ck_user_profiles_script_pref", "\"script_pref\" IN ('off', 'kanji_furigana', 'kana', 'romaji', 'kanji_furigana_romaji')");
                    table.CheckConstraint("ck_user_profiles_ui_locale", "\"ui_locale\" IN ('id', 'en')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_role_is_active",
                schema: "identity",
                table: "user_profiles",
                columns: new[] { "role", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_username",
                schema: "identity",
                table: "user_profiles",
                column: "username",
                unique: true);

            // docs/03 §4.1: unique (lower(email)) where email is not null. EF cannot model expression indexes.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_user_profiles_lower_email ON identity.user_profiles (lower(email)) WHERE email IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "identity");
        }
    }
}
