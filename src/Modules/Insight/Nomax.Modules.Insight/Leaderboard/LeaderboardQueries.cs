using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Insight.Leaderboard;

public sealed record SeasonDto(Guid Id, Guid LevelId, string Name, SeasonStatus Status, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

/// <summary>A board row (BR-SEASON-05): display name, points and rank only.</summary>
public sealed record LeaderboardEntryDto(int Rank, string EntryKey, string DisplayName, decimal Points);

/// <summary>The caller's own row; also the <c>MyRankUpdated</c> hub message.</summary>
public sealed record MyRankDto(Guid SeasonId, int Rank, decimal Points, string EntryKey);

/// <param name="Me">Only for students with points in the season.</param>
public sealed record LeaderboardDto(SeasonDto Season, IReadOnlyList<LeaderboardEntryDto> Top, MyRankDto? Me, DateTimeOffset UpdatedAt);

public sealed record LeaderboardLevelDto(Guid LevelId, string Code, TextBlock Name);

/// <param name="Levels">Levels the caller may see (students: levels of their classes, Q-06).</param>
/// <param name="Board">Active season of the requested (or first) level; null without visible levels.</param>
public sealed record LeaderboardsResponse(IReadOnlyList<LeaderboardLevelDto> Levels, LeaderboardDto? Board);

/// <summary>Who is looking: hub calls have no HTTP request scope, so the caller is passed explicitly.</summary>
internal sealed record Viewer(Guid Id, bool IsStudent);

/// <summary>
/// Read side of the boards and the visibility rule of Q-06: students see the seasons of the levels of their (non-draft)
/// classes, staff see every level. A season outside that is answered as not found.
/// </summary>
internal sealed class LeaderboardQueries(
    InsightDbContext db,
    SeasonService seasons,
    LeaderboardStore store,
    EntryKeys keys,
    IClassAccessReader classes,
    IContentCatalog catalog,
    IOptions<LeaderboardOptions> options,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<LevelInfo>> VisibleLevelsAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        var levels = await catalog.GetLevelsAsync(cancellationToken);
        if (!viewer.IsStudent)
        {
            return levels;
        }

        var mine = new HashSet<Guid>();
        foreach (var classId in await classes.GetStudentClassIdsAsync(viewer.Id, cancellationToken))
        {
            if (await classes.GetClassInfoAsync(classId, cancellationToken) is { Status: not ClassStatus.Draft } info)
            {
                mine.Add(info.LevelId);
            }
        }

        return [.. levels.Where(level => mine.Contains(level.Id))];
    }

    public async Task<bool> CanSeeLevelAsync(Viewer viewer, Guid levelId, CancellationToken cancellationToken) =>
        (await VisibleLevelsAsync(viewer, cancellationToken)).Any(level => level.Id == levelId);

    /// <summary>The season if the viewer may see it, otherwise null (→ 404).</summary>
    public async Task<LeaderboardSeason?> SeasonAsync(Viewer viewer, Guid seasonId, CancellationToken cancellationToken)
    {
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(item => item.Id == seasonId, cancellationToken);
        return season is not null && await CanSeeLevelAsync(viewer, season.LevelId, cancellationToken) ? season : null;
    }

    public async Task<LeaderboardsResponse> LeaderboardsAsync(Viewer viewer, Guid? levelId, CancellationToken cancellationToken)
    {
        var levels = await VisibleLevelsAsync(viewer, cancellationToken);
        var dtos = levels.Select(level => new LeaderboardLevelDto(level.Id, level.Code, level.Name)).ToList();
        var chosen = levelId is null ? (levels.Count > 0 ? levels[0] : null) : levels.FirstOrDefault(level => level.Id == levelId);
        if (chosen is null || await seasons.ActiveAsync(chosen.Id, cancellationToken) is not { } season)
        {
            return new LeaderboardsResponse(dtos, null);
        }

        return new LeaderboardsResponse(dtos, await BoardAsync(viewer, season, cancellationToken));
    }

    public async Task<LeaderboardDto> BoardAsync(Viewer viewer, LeaderboardSeason season, CancellationToken cancellationToken)
    {
        var top = await TopAsync(season.Id, cancellationToken);
        var me = viewer.IsStudent ? await MyRankAsync(season.Id, viewer.Id, cancellationToken) : null;
        return new LeaderboardDto(ToDto(season), top, me, clock.GetUtcNow());
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> TopAsync(Guid seasonId, CancellationToken cancellationToken) =>
        [.. (await store.TopAsync(seasonId, options.Value.TopN, cancellationToken)).Select(row => ToEntry(seasonId, row))];

    public async Task<MyRankDto?> MyRankAsync(Guid seasonId, Guid studentId, CancellationToken cancellationToken) =>
        await store.RankOfAsync(seasonId, studentId, cancellationToken) is { } rank
            ? new MyRankDto(seasonId, rank.Rank, rank.Points, keys.For(seasonId, studentId))
            : null;

    public LeaderboardEntryDto ToEntry(Guid seasonId, BoardEntry row) =>
        new(row.Rank, keys.For(seasonId, row.StudentId), row.DisplayName, row.Points);

    public static SeasonDto ToDto(LeaderboardSeason season) =>
        new(season.Id, season.LevelId, season.Name, season.Status, season.StartsAt, season.EndsAt);
}
