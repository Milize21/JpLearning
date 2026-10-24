using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Practice.Domain;

namespace Nomax.Modules.Practice.Infrastructure.Configurations;

// Tables of docs/03 §7.1–7.5 and §7.14 exactly (quizzes, assignments and grades arrive in phase 3). level_id, chapter_id,
// item_id, class_id and user ids refer to other modules: plain uuid columns without foreign keys (docs/03 §12).

internal static class PracticeMapping
{
    public static void TextArray(PropertyBuilder<List<string>> property) =>
        property.HasColumnType("text[]").HasDefaultValueSql("'{}'").ValueGeneratedNever();
}

internal sealed class QuestionSourceConfiguration : IEntityTypeConfiguration<QuestionSource>
{
    public void Configure(EntityTypeBuilder<QuestionSource> builder)
    {
        builder.ToTable("question_sources");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(QuestionSource.NameMaxLength);
        builder.Property(item => item.Url).HasMaxLength(QuestionSource.UrlMaxLength);
        builder.Property(item => item.License).HasMaxLength(QuestionSource.LicenseMaxLength);
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions", table => table.HasCheckConstraint("ck_questions_item_ref", "(item_type is null) = (item_id is null)"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        PracticeMapping.TextArray(builder.Property(item => item.Tags));
        builder.Property(item => item.Prompt).HasJsonbConversion();
        builder.Property(item => item.Payload).HasJsonbConversion();
        builder.Property(item => item.Explanation).HasJsonbConversion();
        builder.Property(item => item.Status).HasDefaultValueSql("'draft'").ValueGeneratedNever();
        builder.Property(item => item.Scope).HasDefaultValueSql("'private'").ValueGeneratedNever();
        builder.HasXminVersion();
        builder.HasOne<QuestionSource>().WithMany().HasForeignKey(item => item.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.LevelId, item.ChapterId, item.Category, item.Status });
        builder.HasIndex(item => item.Tags).HasMethod("gin");
        builder.HasIndex(item => new { item.OwnerId, item.Status });
        builder.HasIndex(item => new { item.ItemType, item.ItemId });
    }
}

internal sealed class DrillSessionConfiguration : IEntityTypeConfiguration<DrillSession>
{
    public void Configure(EntityTypeBuilder<DrillSession> builder)
    {
        builder.ToTable("drill_sessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Config).HasJsonbConversion();
        builder.HasIndex(item => new { item.StudentId, item.StartedAt }).IsDescending(false, true);
    }
}

internal sealed class DrillAnswerConfiguration : IEntityTypeConfiguration<DrillAnswer>
{
    public void Configure(EntityTypeBuilder<DrillAnswer> builder)
    {
        builder.ToTable("drill_answers", table => table.HasCheckConstraint("ck_drill_answers_response_ms", "response_ms >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Answer).HasJsonbConversion();
        builder.HasOne<DrillSession>().WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Question>().WithMany().HasForeignKey(item => item.QuestionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.SessionId);
        builder.HasIndex(item => item.QuestionId);
    }
}

internal sealed class ItemMasteryConfiguration : IEntityTypeConfiguration<ItemMastery>
{
    public void Configure(EntityTypeBuilder<ItemMastery> builder)
    {
        builder.ToTable("item_masteries", table => table.HasCheckConstraint("ck_item_masteries_mastery", "mastery >= 0 and mastery <= 1"));
        builder.HasKey(item => new { item.StudentId, item.ItemType, item.ItemId });
        builder.Property(item => item.Attempts).HasDefaultValueSql("0").ValueGeneratedNever();
        builder.Property(item => item.Correct).HasDefaultValueSql("0").ValueGeneratedNever();
        builder.Property(item => item.Streak).HasDefaultValueSql("0").ValueGeneratedNever();
        builder.Property(item => item.Mastery).HasColumnType("numeric(4,3)").HasDefaultValueSql("0").ValueGeneratedNever();
        PracticeMapping.TextArray(builder.Property(item => item.Tags));
        builder.Ignore(item => item.State);
        builder.HasIndex(item => new { item.StudentId, item.Mastery });
        builder.HasIndex(item => new { item.StudentId, item.ChapterId });
        builder.HasIndex(item => item.Tags).HasMethod("gin");
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        // Column type text (docs/03 §7.14); the 2,000-character limit is checked by the endpoint.
        builder.HasIndex(item => new { item.StudentId, item.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(item => new { item.TargetType, item.TargetId });
    }
}
