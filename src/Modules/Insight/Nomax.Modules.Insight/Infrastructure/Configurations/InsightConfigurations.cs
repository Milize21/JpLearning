using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Insight.Domain;

namespace Nomax.Modules.Insight.Infrastructure.Configurations;

// Tables of docs/03 §9 exactly. user_id, student_id and level_id refer to other modules: plain uuid columns without
// foreign keys (docs/03 §12).

internal sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Type).HasMaxLength(ActivityLog.TypeMaxLength);
        builder.Property(item => item.Ref).HasMaxLength(ActivityLog.RefMaxLength);
        builder.Property(item => item.Meta).HasJsonbConversion().HasDefaultValueSql("'{}'::jsonb").ValueGeneratedNever();
        builder.HasIndex(item => new { item.UserId, item.Ts }).IsDescending(false, true);
        builder.HasIndex(item => new { item.Type, item.Ts });
    }
}

internal sealed class LeaderboardSeasonConfiguration : IEntityTypeConfiguration<LeaderboardSeason>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeason> builder)
    {
        builder.ToTable("leaderboard_seasons");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(LeaderboardSeason.NameMaxLength);

        // BR-SEASON-01: one active season per level.
        builder.HasIndex(item => item.LevelId).IsUnique().HasDatabaseName("ux_leaderboard_seasons_active_level").HasFilter("status = 'active'");
    }
}

internal sealed class PointEventConfiguration : IEntityTypeConfiguration<PointEvent>
{
    public void Configure(EntityTypeBuilder<PointEvent> builder)
    {
        builder.ToTable("point_events", table => table.HasCheckConstraint("ck_point_events_points", "points >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Points).HasColumnType("numeric(8,2)");
        builder.Property(item => item.Rule).HasMaxLength(PointEvent.RuleMaxLength);
        builder.Property(item => item.ItemKey).HasMaxLength(PointEvent.ItemKeyMaxLength);
        builder.HasOne<LeaderboardSeason>().WithMany().HasForeignKey(item => item.SeasonId).OnDelete(DeleteBehavior.Restrict);

        // BR-POIN-10: one point event per source.
        builder.HasIndex(item => new { item.SourceType, item.SourceId }).IsUnique();
        builder.HasIndex(item => new { item.SeasonId, item.StudentId });
        builder.HasIndex(item => new { item.StudentId, item.Ts });
        builder.HasIndex(item => new { item.StudentId, item.ItemKey });
    }
}

internal sealed class SeasonStandingConfiguration : IEntityTypeConfiguration<SeasonStanding>
{
    public void Configure(EntityTypeBuilder<SeasonStanding> builder)
    {
        builder.ToTable("season_standings");
        builder.HasKey(item => new { item.SeasonId, item.StudentId });
        builder.Property(item => item.Points).HasColumnType("numeric(10,2)");
        builder.Property(item => item.DisplayName).HasMaxLength(SeasonStanding.DisplayNameMaxLength);
        builder.HasOne<LeaderboardSeason>().WithMany().HasForeignKey(item => item.SeasonId).OnDelete(DeleteBehavior.Restrict);
    }
}
