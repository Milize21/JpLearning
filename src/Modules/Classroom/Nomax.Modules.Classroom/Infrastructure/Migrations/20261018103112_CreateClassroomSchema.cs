using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomax.Modules.Classroom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateClassroomSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "classroom");

            migrationBuilder.CreateTable(
                name: "calendar_feed_tokens",
                schema: "classroom",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "char(64)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_feed_tokens", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "classes",
                schema: "classroom",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'active'"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_classes", x => x.id);
                    table.CheckConstraint("ck_classes_status", "\"status\" IN ('draft', 'active', 'archived')");
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "classroom",
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
                name: "notifications",
                schema: "classroom",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    send_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'pending'"),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    dedupe_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_channel", "\"channel\" IN ('email', 'web_push')");
                    table.CheckConstraint("ck_notifications_status", "\"status\" IN ('pending', 'sent', 'failed', 'cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "classroom",
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
                name: "push_subscriptions",
                schema: "classroom",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    p256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "class_sessions",
                schema: "classroom",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    platform = table.Column<string>(type: "text", nullable: false),
                    meeting_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'scheduled'"),
                    sequence = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_sessions", x => x.id);
                    table.CheckConstraint("ck_class_sessions_ends_after_start", "ends_at > starts_at");
                    table.CheckConstraint("ck_class_sessions_platform", "\"platform\" IN ('zoom', 'discord', 'other')");
                    table.CheckConstraint("ck_class_sessions_status", "\"status\" IN ('scheduled', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_class_sessions_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "classroom",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_students",
                schema: "classroom",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'active'"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_students", x => new { x.class_id, x.student_id });
                    table.CheckConstraint("ck_class_students_status", "\"status\" IN ('active', 'removed')");
                    table.ForeignKey(
                        name: "fk_class_students_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "classroom",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_teachers",
                schema: "classroom",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_teachers", x => new { x.class_id, x.teacher_id });
                    table.ForeignKey(
                        name: "fk_class_teachers_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "classroom",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_unlocks",
                schema: "classroom",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unlocked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_unlocks", x => new { x.class_id, x.chapter_id });
                    table.ForeignKey(
                        name: "fk_class_unlocks_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "classroom",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_feed_tokens_token_hash",
                schema: "classroom",
                table: "calendar_feed_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_class_id_starts_at",
                schema: "classroom",
                table: "class_sessions",
                columns: new[] { "class_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_scheduled",
                schema: "classroom",
                table: "class_sessions",
                column: "starts_at",
                filter: "status = 'scheduled'");

            migrationBuilder.CreateIndex(
                name: "ix_class_students_student_id_status",
                schema: "classroom",
                table: "class_students",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_class_teachers_primary",
                schema: "classroom",
                table: "class_teachers",
                column: "class_id",
                unique: true,
                filter: "is_primary");

            migrationBuilder.CreateIndex(
                name: "ix_class_teachers_teacher_id",
                schema: "classroom",
                table: "class_teachers",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_level_id",
                schema: "classroom",
                table: "classes",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_status",
                schema: "classroom",
                table: "classes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_dedupe_key",
                schema: "classroom",
                table: "notifications",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_pending",
                schema: "classroom",
                table: "notifications",
                column: "send_at",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_created_at",
                schema: "classroom",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "classroom",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_endpoint",
                schema: "classroom",
                table: "push_subscriptions",
                column: "endpoint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_feed_tokens",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "class_sessions",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "class_students",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "class_teachers",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "class_unlocks",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "push_subscriptions",
                schema: "classroom");

            migrationBuilder.DropTable(
                name: "classes",
                schema: "classroom");
        }
    }
}
