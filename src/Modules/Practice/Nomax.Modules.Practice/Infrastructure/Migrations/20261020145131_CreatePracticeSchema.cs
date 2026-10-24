using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomax.Modules.Practice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreatePracticeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "practice");

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.CheckConstraint("ck_comments_target_type", "\"target_type\" IN ('drill_session', 'quiz_attempt', 'submission', 'progress')");
                });

            migrationBuilder.CreateTable(
                name: "drill_sessions",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drill_sessions", x => x.id);
                    table.CheckConstraint("ck_drill_sessions_category", "\"category\" IN ('hiragana', 'katakana', 'kanji', 'vocab', 'grammar', 'sentence')");
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "practice",
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
                name: "item_masteries",
                schema: "practice",
                columns: table => new
                {
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    correct = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    streak = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mastery = table.Column<decimal>(type: "numeric(4,3)", nullable: false, defaultValueSql: "0"),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_masteries", x => new { x.student_id, x.item_type, x.item_id });
                    table.CheckConstraint("ck_item_masteries_category", "\"category\" IN ('hiragana', 'katakana', 'kanji', 'vocab', 'grammar', 'sentence')");
                    table.CheckConstraint("ck_item_masteries_item_type", "\"item_type\" IN ('kana', 'kanji', 'vocab', 'grammar', 'sentence', 'question')");
                    table.CheckConstraint("ck_item_masteries_mastery", "mastery >= 0 and mastery <= 1");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "practice",
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
                name: "question_sources",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    imported_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    prompt = table.Column<string>(type: "jsonb", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    explanation = table.Column<string>(type: "jsonb", nullable: true),
                    item_type = table.Column<string>(type: "text", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'draft'"),
                    scope = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'private'"),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.CheckConstraint("ck_questions_category", "\"category\" IN ('hiragana', 'katakana', 'kanji', 'vocab', 'grammar', 'sentence')");
                    table.CheckConstraint("ck_questions_item_ref", "(item_type is null) = (item_id is null)");
                    table.CheckConstraint("ck_questions_item_type", "\"item_type\" IN ('kana', 'kanji', 'vocab', 'grammar', 'sentence')");
                    table.CheckConstraint("ck_questions_kind", "\"kind\" IN ('mcq', 'tap_sentence', 'type_answer', 'match', 'flashcard', 'handwrite_kana', 'handwrite_kanji', 'long_answer')");
                    table.CheckConstraint("ck_questions_scope", "\"scope\" IN ('builtin', 'private')");
                    table.CheckConstraint("ck_questions_status", "\"status\" IN ('draft', 'review', 'published', 'archived')");
                    table.ForeignKey(
                        name: "fk_questions_question_sources_source_id",
                        column: x => x.source_id,
                        principalSchema: "practice",
                        principalTable: "question_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "drill_answers",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_mode = table.Column<string>(type: "text", nullable: false),
                    answer = table.Column<string>(type: "jsonb", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    response_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drill_answers", x => x.id);
                    table.CheckConstraint("ck_drill_answers_input_mode", "\"input_mode\" IN ('choice', 'tap', 'type', 'handwrite', 'self_check')");
                    table.CheckConstraint("ck_drill_answers_response_ms", "response_ms >= 0");
                    table.ForeignKey(
                        name: "fk_drill_answers_drill_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "practice",
                        principalTable: "drill_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_drill_answers_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "practice",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comments_student_id_created_at",
                schema: "practice",
                table: "comments",
                columns: new[] { "student_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_comments_target_type_target_id",
                schema: "practice",
                table: "comments",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_drill_answers_question_id",
                schema: "practice",
                table: "drill_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_drill_answers_session_id",
                schema: "practice",
                table: "drill_answers",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_drill_sessions_student_id_started_at",
                schema: "practice",
                table: "drill_sessions",
                columns: new[] { "student_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_item_masteries_student_id_chapter_id",
                schema: "practice",
                table: "item_masteries",
                columns: new[] { "student_id", "chapter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_item_masteries_student_id_mastery",
                schema: "practice",
                table: "item_masteries",
                columns: new[] { "student_id", "mastery" });

            migrationBuilder.CreateIndex(
                name: "ix_item_masteries_tags",
                schema: "practice",
                table: "item_masteries",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "practice",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_questions_item_type_item_id",
                schema: "practice",
                table: "questions",
                columns: new[] { "item_type", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_level_id_chapter_id_category_status",
                schema: "practice",
                table: "questions",
                columns: new[] { "level_id", "chapter_id", "category", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_owner_id_status",
                schema: "practice",
                table: "questions",
                columns: new[] { "owner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_tags",
                schema: "practice",
                table: "questions",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comments",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "drill_answers",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "item_masteries",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "drill_sessions",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "questions",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "question_sources",
                schema: "practice");
        }
    }
}
