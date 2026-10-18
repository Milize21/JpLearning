using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomax.Modules.Content.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateContentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "content",
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
                name: "levels",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "content",
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
                name: "stored_files",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "char(64)", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'uploading'"),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                    table.CheckConstraint("ck_stored_files_purpose", "\"purpose\" IN ('material', 'book_cover', 'submission', 'question_import')");
                    table.CheckConstraint("ck_stored_files_size_bytes", "size_bytes BETWEEN 0 AND 1073741824");
                    table.CheckConstraint("ck_stored_files_status", "\"status\" IN ('uploading', 'ready', 'failed', 'deleted')");
                });

            migrationBuilder.CreateTable(
                name: "books",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "jsonb", nullable: false),
                    cover_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_books", x => x.id);
                    table.ForeignKey(
                        name: "fk_books_levels_level_id",
                        column: x => x.level_id,
                        principalSchema: "content",
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_books_stored_files_cover_file_id",
                        column: x => x.cover_file_id,
                        principalSchema: "content",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chapters",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chapters", x => x.id);
                    table.ForeignKey(
                        name: "fk_chapters_books_book_id",
                        column: x => x.book_id,
                        principalSchema: "content",
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grammar_points",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "jsonb", nullable: false),
                    explanation = table.Column<string>(type: "jsonb", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grammar_points", x => x.id);
                    table.ForeignKey(
                        name: "fk_grammar_points_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "content",
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grammar_points_levels_level_id",
                        column: x => x.level_id,
                        principalSchema: "content",
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kana_chars",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    @char = table.Column<string>(name: "char", type: "character varying(4)", maxLength: 4, nullable: false),
                    script = table.Column<string>(type: "text", nullable: false),
                    romaji = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    row_key = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    variant = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kana_chars", x => x.id);
                    table.CheckConstraint("ck_kana_chars_script", "\"script\" IN ('hiragana', 'katakana')");
                    table.CheckConstraint("ck_kana_chars_variant", "\"variant\" IN ('seion', 'dakuon', 'handakuon', 'youon')");
                    table.ForeignKey(
                        name: "fk_kana_chars_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "content",
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kana_chars_levels_level_id",
                        column: x => x.level_id,
                        principalSchema: "content",
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kanji",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    onyomi = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    kunyomi = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    meanings = table.Column<string>(type: "jsonb", nullable: false),
                    stroke_count = table.Column<short>(type: "smallint", nullable: true),
                    kanjivg_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kanji", x => x.id);
                    table.ForeignKey(
                        name: "fk_kanji_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "content",
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kanji_levels_level_id",
                        column: x => x.level_id,
                        principalSchema: "content",
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "materials",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "jsonb", nullable: false),
                    body = table.Column<string>(type: "jsonb", nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    publish_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materials", x => x.id);
                    table.CheckConstraint("ck_materials_content", "(kind = 'file' AND file_id IS NOT NULL) OR (kind = 'link' AND url IS NOT NULL) OR (kind = 'richtext' AND body IS NOT NULL)");
                    table.CheckConstraint("ck_materials_kind", "\"kind\" IN ('file', 'link', 'richtext')");
                    table.ForeignKey(
                        name: "fk_materials_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "content",
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_materials_stored_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "content",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vocab",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reading = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    meanings = table.Column<string>(type: "jsonb", nullable: false),
                    pos = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    jmdict_seq = table.Column<long>(type: "bigint", nullable: true),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vocab", x => x.id);
                    table.ForeignKey(
                        name: "fk_vocab_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "content",
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vocab_levels_level_id",
                        column: x => x.level_id,
                        principalSchema: "content",
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sentences",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "jsonb", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    grammar_point_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tatoeba_id = table.Column<long>(type: "bigint", nullable: true),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sentences", x => x.id);
                    table.ForeignKey(
                        name: "fk_sentences_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "content",
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sentences_grammar_points_grammar_point_id",
                        column: x => x.grammar_point_id,
                        principalSchema: "content",
                        principalTable: "grammar_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sentences_levels_level_id",
                        column: x => x.level_id,
                        principalSchema: "content",
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_books_level_id_sort_order",
                schema: "content",
                table: "books",
                columns: new[] { "level_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_chapters_book_id_number",
                schema: "content",
                table: "chapters",
                columns: new[] { "book_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chapters_book_id_sort_order",
                schema: "content",
                table: "chapters",
                columns: new[] { "book_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_grammar_points_level_id_chapter_id",
                schema: "content",
                table: "grammar_points",
                columns: new[] { "level_id", "chapter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_kana_chars_level_id_chapter_id",
                schema: "content",
                table: "kana_chars",
                columns: new[] { "level_id", "chapter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_kana_chars_script_char",
                schema: "content",
                table: "kana_chars",
                columns: new[] { "script", "char" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kanji_character",
                schema: "content",
                table: "kanji",
                column: "character",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kanji_level_id_chapter_id",
                schema: "content",
                table: "kanji",
                columns: new[] { "level_id", "chapter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_levels_code",
                schema: "content",
                table: "levels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_materials_chapter_id_sort_order",
                schema: "content",
                table: "materials",
                columns: new[] { "chapter_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "content",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sentences_level_id_chapter_id",
                schema: "content",
                table: "sentences",
                columns: new[] { "level_id", "chapter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_status_created_at",
                schema: "content",
                table: "stored_files",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_storage_key",
                schema: "content",
                table: "stored_files",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_uploaded_by",
                schema: "content",
                table: "stored_files",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_vocab_level_id_chapter_id",
                schema: "content",
                table: "vocab",
                columns: new[] { "level_id", "chapter_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vocab_level_id_word_reading",
                schema: "content",
                table: "vocab",
                columns: new[] { "level_id", "word", "reading" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "content");

            migrationBuilder.DropTable(
                name: "kana_chars",
                schema: "content");

            migrationBuilder.DropTable(
                name: "kanji",
                schema: "content");

            migrationBuilder.DropTable(
                name: "materials",
                schema: "content");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "content");

            migrationBuilder.DropTable(
                name: "sentences",
                schema: "content");

            migrationBuilder.DropTable(
                name: "vocab",
                schema: "content");

            migrationBuilder.DropTable(
                name: "grammar_points",
                schema: "content");

            migrationBuilder.DropTable(
                name: "chapters",
                schema: "content");

            migrationBuilder.DropTable(
                name: "books",
                schema: "content");

            migrationBuilder.DropTable(
                name: "levels",
                schema: "content");

            migrationBuilder.DropTable(
                name: "stored_files",
                schema: "content");
        }
    }
}
