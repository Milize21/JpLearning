using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Insight.Domain;
using Nomax.SharedKernel.Identifiers;

namespace Nomax.Modules.Insight.Infrastructure;

/// <summary>BR-SEASON-01: every level always has one active season; a missing one is opened on first use.</summary>
internal sealed class SeasonService(InsightDbContext db, IContentCatalog catalog, TimeProvider clock)
{
    /// <summary>The active season of the level, opened when it has none; null for an unknown level.</summary>
    public async Task<LeaderboardSeason?> ActiveAsync(Guid levelId, CancellationToken cancellationToken)
    {
        var season = await FindActiveAsync(levelId, cancellationToken);
        if (season is not null)
        {
            return season;
        }

        if (await catalog.GetLevelAsync(levelId, cancellationToken) is not { } level)
        {
            return null;
        }

        var number = await db.Seasons.CountAsync(item => item.LevelId == levelId, cancellationToken) + 1;
        var now = clock.GetUtcNow();
        var name = Ranking.SeasonName(LevelLabel(level), number);

        // ON CONFLICT keeps a surrounding transaction usable when another request opened the season first.
        await db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO insight.leaderboard_seasons (id, level_id, name, starts_at, status, created_at, updated_at)
             VALUES ({Uuid7.New(clock)}, {levelId}, {name}, {now}, 'active', {now}, {now})
             ON CONFLICT (level_id) WHERE status = 'active' DO NOTHING
             """,
            cancellationToken);
        return await FindActiveAsync(levelId, cancellationToken);
    }

    /// <summary>"{level} · Musim {n+1}" for the successor of a closed season (BR-SEASON-02).</summary>
    public async Task<LeaderboardSeason> SuccessorAsync(LeaderboardSeason closed, CancellationToken cancellationToken)
    {
        var level = await catalog.GetLevelAsync(closed.LevelId, cancellationToken);
        var number = await db.Seasons.CountAsync(item => item.LevelId == closed.LevelId, cancellationToken) + 1;
        var name = Ranking.SeasonName(level is null ? closed.Name.Split(" · ")[0] : LevelLabel(level), number);
        return LeaderboardSeason.Open(Uuid7.New(clock), closed.LevelId, name, clock.GetUtcNow());
    }

    // Seasons are named in Indonesian (BR-SEASON-02), e.g. "N5 · Musim 1".
    private static string LevelLabel(LevelInfo level) => level.Name.Indonesian ?? level.Code.ToUpperInvariant();

    private Task<LeaderboardSeason?> FindActiveAsync(Guid levelId, CancellationToken cancellationToken) =>
        db.Seasons.FirstOrDefaultAsync(item => item.LevelId == levelId && item.Status == SeasonStatus.Active, cancellationToken);
}
