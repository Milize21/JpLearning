using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomax.Modules.Insight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateInsightSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "insight");

            migrationBuilder.CreateTable(
                name: "activity_logs",
                schema: "insight",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    @ref = table.Column<string>(name: "ref", type: "character varying(100)", maxLength: 100, nullable: true),
                    meta = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "insight",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => new { x.message_id, x.handler });
                });

            migrationBuilder.CreateTable(
                name: "leaderboard_seasons",
                schema: "insight",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leaderboard_seasons", x => x.id);
                    table.CheckConstraint("ck_leaderboard_seasons_status", "\"status\" IN ('active', 'closed')");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "insight",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "point_events",
                schema: "insight",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rule = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_point_events", x => x.id);
                    table.CheckConstraint("ck_point_events_points", "points >= 0");
                    table.CheckConstraint("ck_point_events_source_type", "\"source_type\" IN ('drill', 'quiz', 'exam')");
                    table.ForeignKey(
                        name: "fk_point_events_leaderboard_seasons_season_id",
                        column: x => x.season_id,
                        principalSchema: "insight",
                        principalTable: "leaderboard_seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "season_standings",
                schema: "insight",
                columns: table => new
                {
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_season_standings", x => new { x.season_id, x.student_id });
                    table.ForeignKey(
                        name: "fk_season_standings_leaderboard_seasons_season_id",
                        column: x => x.season_id,
                        principalSchema: "insight",
                        principalTable: "leaderboard_seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_type_ts",
                schema: "insight",
                table: "activity_logs",
                columns: new[] { "type", "ts" });

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_user_id_ts",
                schema: "insight",
                table: "activity_logs",
                columns: new[] { "user_id", "ts" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_leaderboard_seasons_active_level",
                schema: "insight",
                table: "leaderboard_seasons",
                column: "level_id",
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "insight",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_point_events_season_id_student_id",
                schema: "insight",
                table: "point_events",
                columns: new[] { "season_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_point_events_source_type_source_id",
                schema: "insight",
                table: "point_events",
                columns: new[] { "source_type", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_point_events_student_id_item_key",
                schema: "insight",
                table: "point_events",
                columns: new[] { "student_id", "item_key" });

            migrationBuilder.CreateIndex(
                name: "ix_point_events_student_id_ts",
                schema: "insight",
                table: "point_events",
                columns: new[] { "student_id", "ts" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_logs",
                schema: "insight");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "insight");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "insight");

            migrationBuilder.DropTable(
                name: "point_events",
                schema: "insight");

            migrationBuilder.DropTable(
                name: "season_standings",
                schema: "insight");

            migrationBuilder.DropTable(
                name: "leaderboard_seasons",
                schema: "insight");
        }
    }
}
