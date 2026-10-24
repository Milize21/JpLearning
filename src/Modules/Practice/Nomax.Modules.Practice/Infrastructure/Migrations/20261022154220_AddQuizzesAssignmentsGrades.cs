using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomax.Modules.Practice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizzesAssignmentsGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "jsonb", nullable: false),
                    instructions = table.Column<string>(type: "jsonb", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    allow_resubmit = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'draft'"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignments", x => x.id);
                    table.CheckConstraint("ck_assignments_status", "\"status\" IN ('draft', 'published', 'archived')");
                });

            migrationBuilder.CreateTable(
                name: "class_grades",
                schema: "practice",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    gpa_0_4 = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    breakdown = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_grades", x => new { x.class_id, x.student_id });
                });

            migrationBuilder.CreateTable(
                name: "grade_schemes",
                schema: "practice",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    w_quiz = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValueSql: "30"),
                    w_exam = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValueSql: "40"),
                    w_assignment = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValueSql: "30"),
                    scale_map = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[{\"min\":80,\"gpa\":4},{\"min\":70,\"gpa\":3},{\"min\":60,\"gpa\":2},{\"min\":50,\"gpa\":1},{\"min\":0,\"gpa\":0}]'::jsonb"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_schemes", x => x.class_id);
                    table.CheckConstraint("ck_grade_schemes_weights", "w_quiz >= 0 and w_exam >= 0 and w_assignment >= 0 and w_quiz + w_exam + w_assignment = 100");
                });

            migrationBuilder.CreateTable(
                name: "quizzes",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "jsonb", nullable: false),
                    time_limit_min = table.Column<int>(type: "integer", nullable: true),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "1"),
                    open_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    close_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    shuffle = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    show_review = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'draft'"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quizzes", x => x.id);
                    table.CheckConstraint("ck_quizzes_close_after_open", "close_at > open_at");
                    table.CheckConstraint("ck_quizzes_kind", "\"kind\" IN ('quiz', 'exam')");
                    table.CheckConstraint("ck_quizzes_max_attempts", "max_attempts >= 1");
                    table.CheckConstraint("ck_quizzes_status", "\"status\" IN ('draft', 'published', 'archived')");
                    table.CheckConstraint("ck_quizzes_time_limit_min", "time_limit_min > 0");
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    file_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    grade_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    graded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    graded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_content", "text is not null or cardinality(file_ids) > 0");
                    table.CheckConstraint("ck_submissions_grade_pct", "grade_pct >= 0 and grade_pct <= 100");
                    table.ForeignKey(
                        name: "fk_submissions_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "practice",
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                schema: "practice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    score_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    question_order = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_attempts", x => x.id);
                    table.CheckConstraint("ck_quiz_attempts_score_pct", "score_pct >= 0 and score_pct <= 100");
                    table.CheckConstraint("ck_quiz_attempts_status", "\"status\" IN ('in_progress', 'submitted', 'graded')");
                    table.ForeignKey(
                        name: "fk_quiz_attempts_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalSchema: "practice",
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                schema: "practice",
                columns: table => new
                {
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<decimal>(type: "numeric(6,2)", nullable: false, defaultValueSql: "1"),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_questions", x => new { x.quiz_id, x.question_id });
                    table.CheckConstraint("ck_quiz_questions_points", "points > 0");
                    table.ForeignKey(
                        name: "fk_quiz_questions_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "practice",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quiz_questions_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalSchema: "practice",
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attempt_answers",
                schema: "practice",
                columns: table => new
                {
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer = table.Column<string>(type: "jsonb", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: true),
                    points = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    graded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    graded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempt_answers", x => new { x.attempt_id, x.question_id });
                    table.ForeignKey(
                        name: "fk_attempt_answers_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "practice",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attempt_answers_quiz_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalSchema: "practice",
                        principalTable: "quiz_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_class_id_status_due_at",
                schema: "practice",
                table: "assignments",
                columns: new[] { "class_id", "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_in_progress",
                schema: "practice",
                table: "quiz_attempts",
                columns: new[] { "quiz_id", "student_id" },
                unique: true,
                filter: "status = 'in_progress'");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_quiz_id_student_id_attempt_no",
                schema: "practice",
                table: "quiz_attempts",
                columns: new[] { "quiz_id", "student_id", "attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_student_id_status",
                schema: "practice",
                table: "quiz_attempts",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_quizzes_class_id_status_open_at",
                schema: "practice",
                table: "quizzes",
                columns: new[] { "class_id", "status", "open_at" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_assignment_id_student_id",
                schema: "practice",
                table: "submissions",
                columns: new[] { "assignment_id", "student_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempt_answers",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "class_grades",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "grade_schemes",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "quiz_questions",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "submissions",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "quiz_attempts",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "practice");

            migrationBuilder.DropTable(
                name: "quizzes",
                schema: "practice");
        }
    }
}
