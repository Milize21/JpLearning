using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Practice.Domain;

namespace Nomax.Modules.Practice.Infrastructure.Configurations;

// Tables of docs/03 §7.6–7.13 (phase 3). class_id, chapter_id, file ids and user ids refer to other modules: plain uuid
// columns without foreign keys (docs/03 §12).

internal sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes", table =>
        {
            table.HasCheckConstraint("ck_quizzes_time_limit_min", "time_limit_min > 0");
            table.HasCheckConstraint("ck_quizzes_max_attempts", "max_attempts >= 1");
            table.HasCheckConstraint("ck_quizzes_close_after_open", "close_at > open_at");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasJsonbConversion();
        builder.Property(item => item.MaxAttempts).HasDefaultValueSql("1").ValueGeneratedNever();
        builder.Property(item => item.Shuffle).HasDefaultValueSql("true").ValueGeneratedNever();
        builder.Property(item => item.ShowReview).HasDefaultValueSql("false").ValueGeneratedNever();
        builder.Property(item => item.Status).HasDefaultValueSql("'draft'").ValueGeneratedNever();
        builder.HasXminVersion();
        builder.HasIndex(item => new { item.ClassId, item.Status, item.OpenAt });
    }
}

internal sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("quiz_questions", table => table.HasCheckConstraint("ck_quiz_questions_points", "points > 0"));
        builder.HasKey(item => new { item.QuizId, item.QuestionId });
        builder.Property(item => item.Points).HasColumnType("numeric(6,2)").HasDefaultValueSql("1").ValueGeneratedNever();
        builder.HasOne<Quiz>().WithMany().HasForeignKey(item => item.QuizId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Question>().WithMany().HasForeignKey(item => item.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("quiz_attempts", table => table.HasCheckConstraint("ck_quiz_attempts_score_pct", "score_pct >= 0 and score_pct <= 100"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ScorePct).HasColumnType("numeric(5,2)");
        builder.Property(item => item.QuestionOrder).HasColumnType("uuid[]");
        builder.HasOne<Quiz>().WithMany().HasForeignKey(item => item.QuizId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.QuizId, item.StudentId, item.AttemptNo }).IsUnique();
        builder.HasIndex(item => new { item.QuizId, item.StudentId }).IsUnique()
            .HasDatabaseName("ix_quiz_attempts_in_progress").HasFilter("status = 'in_progress'");
        builder.HasIndex(item => new { item.StudentId, item.Status });
    }
}

internal sealed class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.ToTable("attempt_answers");
        builder.HasKey(item => new { item.AttemptId, item.QuestionId });
        builder.Property(item => item.Answer).HasJsonbConversion();
        builder.Property(item => item.Points).HasColumnType("numeric(6,2)");
        builder.HasOne<QuizAttempt>().WithMany().HasForeignKey(item => item.AttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Question>().WithMany().HasForeignKey(item => item.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasJsonbConversion();
        builder.Property(item => item.Instructions).HasJsonbConversion();
        builder.Property(item => item.AllowResubmit).HasDefaultValueSql("false").ValueGeneratedNever();
        builder.Property(item => item.Status).HasDefaultValueSql("'draft'").ValueGeneratedNever();
        builder.HasXminVersion();
        builder.HasIndex(item => new { item.ClassId, item.Status, item.DueAt });
    }
}

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions", table =>
        {
            table.HasCheckConstraint("ck_submissions_grade_pct", "grade_pct >= 0 and grade_pct <= 100");
            table.HasCheckConstraint("ck_submissions_content", "text is not null or cardinality(file_ids) > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.FileIds).HasColumnType("uuid[]").HasDefaultValueSql("'{}'").ValueGeneratedNever();
        builder.Property(item => item.GradePct).HasColumnType("numeric(5,2)");
        builder.HasOne<Assignment>().WithMany().HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.AssignmentId, item.StudentId }).IsUnique();
    }
}

internal sealed class GradeSchemeConfiguration : IEntityTypeConfiguration<GradeScheme>
{
    public void Configure(EntityTypeBuilder<GradeScheme> builder)
    {
        builder.ToTable("grade_schemes", table =>
        {
            table.HasCheckConstraint("ck_grade_schemes_weights", "w_quiz >= 0 and w_exam >= 0 and w_assignment >= 0 and w_quiz + w_exam + w_assignment = 100");
        });
        builder.HasKey(item => item.ClassId);
        builder.Property(item => item.ClassId).ValueGeneratedNever();
        builder.Property(item => item.WQuiz).HasColumnName("w_quiz").HasColumnType("numeric(5,2)").HasDefaultValueSql("30").ValueGeneratedNever();
        builder.Property(item => item.WExam).HasColumnName("w_exam").HasColumnType("numeric(5,2)").HasDefaultValueSql("40").ValueGeneratedNever();
        builder.Property(item => item.WAssignment).HasColumnName("w_assignment").HasColumnType("numeric(5,2)").HasDefaultValueSql("30").ValueGeneratedNever();
        builder.Property(item => item.ScaleMap).HasJsonbConversion()
            .HasDefaultValueSql("""'[{"min":80,"gpa":4},{"min":70,"gpa":3},{"min":60,"gpa":2},{"min":50,"gpa":1},{"min":0,"gpa":0}]'::jsonb""")
            .ValueGeneratedNever();
        builder.HasXminVersion();
    }
}

internal sealed class ClassGradeConfiguration : IEntityTypeConfiguration<ClassGrade>
{
    public void Configure(EntityTypeBuilder<ClassGrade> builder)
    {
        builder.ToTable("class_grades");
        builder.HasKey(item => new { item.ClassId, item.StudentId });
        builder.Property(item => item.FinalPct).HasColumnType("numeric(5,2)");
        builder.Property(item => item.Gpa04).HasColumnName("gpa_0_4").HasColumnType("numeric(3,2)");
        builder.Property(item => item.Breakdown).HasJsonbConversion();
    }
}
