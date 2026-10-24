using System.Text.Json;
using Nomax.BuildingBlocks.Persistence;

namespace Nomax.Modules.Insight.Domain;

public enum SeasonStatus
{
    Active,
    Closed,
}

/// <summary><c>point_source</c> (docs/03 §3.3).</summary>
public enum PointSource
{
    Drill,
    Quiz,
    Exam,
}

/// <summary>insight.activity_logs (docs/03 §9.1); types in docs/05 §11.</summary>
public sealed class ActivityLog : IHasTimestamps
{
    public const int TypeMaxLength = 50;
    public const int RefMaxLength = 100;

    private ActivityLog()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string? Ref { get; private set; }

    public JsonElement Meta { get; private set; }

    public DateTimeOffset Ts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ActivityLog Create(Guid id, Guid userId, string type, string? reference, JsonElement meta, DateTimeOffset ts) =>
        new() { Id = id, UserId = userId, Type = type, Ref = reference, Meta = meta.Clone(), Ts = ts };
}

/// <summary>insight.leaderboard_seasons (docs/03 §9.2): one active season per level (BR-SEASON-01).</summary>
public sealed class LeaderboardSeason : IHasTimestamps
{
    public const int NameMaxLength = 100;

    private LeaderboardSeason()
    {
    }

    public Guid Id { get; private set; }

    public Guid LevelId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public SeasonStatus Status { get; private set; }

    public Guid? ClosedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static LeaderboardSeason Open(Guid id, Guid levelId, string name, DateTimeOffset startsAt) =>
        new() { Id = id, LevelId = levelId, Name = name, StartsAt = startsAt, Status = SeasonStatus.Active };

    public void Close(Guid closedBy, DateTimeOffset at)
    {
        Status = SeasonStatus.Closed;
        EndsAt = at;
        ClosedBy = closedBy;
    }
}

/// <summary>insight.point_events (docs/03 §9.3): one per source (BR-POIN-10); 0 points are kept for audit.</summary>
public sealed class PointEvent : IHasTimestamps
{
    public const int RuleMaxLength = 50;
    public const int ItemKeyMaxLength = 80;

    private PointEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid LevelId { get; private set; }

    public Guid SeasonId { get; private set; }

    public PointSource SourceType { get; private set; }

    public Guid SourceId { get; private set; }

    public decimal Points { get; private set; }

    public DateTimeOffset Ts { get; private set; }

    public string Rule { get; private set; } = string.Empty;

    public string ItemKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static PointEvent Create(
        Guid id, Guid studentId, Guid levelId, Guid seasonId, PointSource sourceType, Guid sourceId, PointAward award, string itemKey, DateTimeOffset ts) =>
        new()
        {
            Id = id,
            StudentId = studentId,
            LevelId = levelId,
            SeasonId = seasonId,
            SourceType = sourceType,
            SourceId = sourceId,
            Points = award.Points,
            Rule = award.Rule,
            ItemKey = itemKey,
            Ts = ts,
        };
}

/// <summary>insight.season_standings (docs/03 §9.4): the archived ranking of a closed season.</summary>
public sealed class SeasonStanding : IHasTimestamps
{
    public const int DisplayNameMaxLength = 80;

    private SeasonStanding()
    {
    }

    public Guid SeasonId { get; private set; }

    public Guid StudentId { get; private set; }

    public int Rank { get; private set; }

    public decimal Points { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static SeasonStanding Create(Guid seasonId, Guid studentId, int rank, decimal points, string displayName) =>
        new()
        {
            SeasonId = seasonId,
            StudentId = studentId,
            Rank = rank,
            Points = points,
            DisplayName = displayName.Length > DisplayNameMaxLength ? displayName[..DisplayNameMaxLength] : displayName,
        };
}
