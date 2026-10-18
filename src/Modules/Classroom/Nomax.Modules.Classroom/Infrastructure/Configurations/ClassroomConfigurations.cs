using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Infrastructure.Configurations;

// Tables of docs/03 §6.1–6.8 exactly. level_id, book_id, chapter_id and user ids refer to other modules: plain uuid
// columns without foreign keys (docs/03 §12). Only the indexes listed in the document exist.

internal sealed class ClassConfiguration : IEntityTypeConfiguration<StudyClass>
{
    public void Configure(EntityTypeBuilder<StudyClass> builder)
    {
        builder.ToTable("classes");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(StudyClass.NameMaxLength);
        builder.Property(item => item.Status).HasDefaultValueSql("'active'").ValueGeneratedNever();
        builder.HasIndex(item => item.Status);
        builder.HasIndex(item => item.LevelId);
    }
}

internal sealed class ClassTeacherConfiguration : IEntityTypeConfiguration<ClassTeacher>
{
    public void Configure(EntityTypeBuilder<ClassTeacher> builder)
    {
        builder.ToTable("class_teachers");
        builder.HasKey(item => new { item.ClassId, item.TeacherId });
        builder.Property(item => item.IsPrimary).HasDefaultValueSql("false").ValueGeneratedNever();
        builder.HasOne<StudyClass>().WithMany().HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.ClassId).IsUnique().HasDatabaseName("ix_class_teachers_primary").HasFilter("is_primary");
        builder.HasIndex(item => item.TeacherId);
    }
}

internal sealed class ClassStudentConfiguration : IEntityTypeConfiguration<ClassStudent>
{
    public void Configure(EntityTypeBuilder<ClassStudent> builder)
    {
        builder.ToTable("class_students");
        builder.HasKey(item => new { item.ClassId, item.StudentId });
        builder.Property(item => item.JoinedAt).HasDefaultValueSql("now()").ValueGeneratedNever();
        builder.Property(item => item.Status).HasDefaultValueSql("'active'").ValueGeneratedNever();
        builder.HasOne<StudyClass>().WithMany().HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.StudentId, item.Status });
    }
}

internal sealed class ClassUnlockConfiguration : IEntityTypeConfiguration<ClassUnlock>
{
    public void Configure(EntityTypeBuilder<ClassUnlock> builder)
    {
        builder.ToTable("class_unlocks");
        builder.HasKey(item => new { item.ClassId, item.ChapterId });
        builder.HasOne<StudyClass>().WithMany().HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> builder)
    {
        builder.ToTable("class_sessions", table => table.HasCheckConstraint("ck_class_sessions_ends_after_start", "ends_at > starts_at"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Topic).HasMaxLength(ClassSession.TopicMaxLength);
        builder.Property(item => item.Timezone).HasMaxLength(ClassSession.TimezoneMaxLength);
        builder.Property(item => item.MeetingUrl).HasMaxLength(ClassSession.UrlMaxLength);
        builder.Property(item => item.Status).HasDefaultValueSql("'scheduled'").ValueGeneratedNever();
        builder.Property(item => item.Sequence).HasDefaultValueSql("0").ValueGeneratedNever();
        builder.HasOne<StudyClass>().WithMany().HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ClassId, item.StartsAt });
        builder.HasIndex(item => item.StartsAt).HasDatabaseName("ix_class_sessions_scheduled").HasFilter("status = 'scheduled'");
    }
}

internal sealed class CalendarFeedTokenConfiguration : IEntityTypeConfiguration<CalendarFeedToken>
{
    public void Configure(EntityTypeBuilder<CalendarFeedToken> builder)
    {
        builder.ToTable("calendar_feed_tokens");
        builder.HasKey(item => item.UserId);
        builder.Property(item => item.UserId).ValueGeneratedNever();
        builder.Property(item => item.TokenHash).HasColumnType("char(64)");
        builder.HasIndex(item => item.TokenHash).IsUnique();
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Type).HasMaxLength(Notification.TypeMaxLength);
        builder.Property(item => item.Payload).HasJsonbConversion();
        builder.Property(item => item.Status).HasDefaultValueSql("'pending'").ValueGeneratedNever();
        builder.Property(item => item.Attempts).HasDefaultValueSql("0").ValueGeneratedNever();
        builder.Property(item => item.DedupeKey).HasMaxLength(Notification.DedupeKeyMaxLength);
        builder.HasIndex(item => item.DedupeKey).IsUnique();
        builder.HasIndex(item => item.SendAt).HasDatabaseName("ix_notifications_pending").HasFilter("status = 'pending'");
        builder.HasIndex(item => new { item.UserId, item.CreatedAt }).IsDescending(false, true);
    }
}

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.P256dh).HasMaxLength(PushSubscription.P256dhMaxLength);
        builder.Property(item => item.Auth).HasMaxLength(PushSubscription.AuthMaxLength);
        builder.Property(item => item.UserAgent).HasMaxLength(PushSubscription.UserAgentMaxLength);
        builder.HasIndex(item => item.Endpoint).IsUnique();
    }
}
