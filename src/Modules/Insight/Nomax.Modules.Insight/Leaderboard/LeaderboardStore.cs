using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using StackExchange.Redis;

namespace Nomax.Modules.Insight.Leaderboard;

/// <summary>A ranked row with its pseudonymous key; the student id never leaves the server.</summary>
internal sealed record BoardEntry(Guid StudentId, string DisplayName, decimal Points, int Rank);

/// <summary>
/// Live scores per season in a Redis sorted set <c>lb:{seasonId}</c> (BR-SEASON-03/04). Redis is only a cache
/// (docs/02 §7): a season without its <c>lb:{seasonId}:ready</c> marker is rebuilt from <c>point_events</c>, so the cache
/// can be flushed at any time. Scores are stored in hundredths so ties compare exactly.
/// </summary>
internal sealed partial class LeaderboardStore(
    IConnectionMultiplexer redis, InsightDbContext db, IUserDirectory users, ILogger<LeaderboardStore> logger)
{
    private const string NamesKey = "lb:names";

    private IDatabase Redis => redis.GetDatabase();

    public static RedisKey BoardKey(Guid seasonId) => $"lb:{seasonId}";

    public static RedisKey ReadyKey(Guid seasonId) => $"lb:{seasonId}:ready";

    /// <summary>BR-SEASON-04: <c>ZINCRBY</c> after a committed point event.</summary>
    public async Task AddAsync(Guid seasonId, Guid studentId, decimal points, CancellationToken cancellationToken)
    {
        if (await Redis.KeyExistsAsync(ReadyKey(seasonId)))
        {
            await Redis.SortedSetIncrementAsync(BoardKey(seasonId), studentId.ToString(), ToScore(points));
            return;
        }

        // The rebuild reads the committed event, so no increment on top of it.
        await RebuildAsync(seasonId, cancellationToken);
    }

    /// <summary>The top <paramref name="count"/> rows, ties included up to the cut and ordered by name (BR-SEASON-03).</summary>
    public async Task<IReadOnlyList<BoardEntry>> TopAsync(Guid seasonId, int count, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureAsync(seasonId, cancellationToken);
            var head = await Redis.SortedSetRangeByRankWithScoresAsync(BoardKey(seasonId), 0, count - 1, Order.Descending);
            if (head.Length == 0)
            {
                return [];
            }

            // Rows tied with the last one may sort after it in Redis (by id); fetch them so names decide (A–Z).
            var entries = head.Length < count
                ? head
                : await Redis.SortedSetRangeByScoreWithScoresAsync(BoardKey(seasonId), head[^1].Score, double.PositiveInfinity, order: Order.Descending, take: count * 4);
            var ids = entries.Select(entry => Guid.Parse(entry.Element.ToString())).ToList();
            var names = await NamesAsync(ids, cancellationToken);
            var ranked = Ranking.Rank(entries.Select((entry, i) => (ids[i], names.GetValueOrDefault(ids[i], string.Empty), FromScore(entry.Score))));
            return [.. ranked.Take(count).Select(row => new BoardEntry(row.StudentId, row.DisplayName, row.Points, row.Rank))];
        }
        catch (RedisException exception)
        {
            LogRedisFallback(logger, seasonId, exception);
            return [.. (await StandingsFromDatabaseAsync(seasonId, cancellationToken)).Take(count)];
        }
    }

    /// <summary>The student's live rank, <c>1 + ZCOUNT(lb, "(" + score, +inf)</c>; null without points.</summary>
    public async Task<(int Rank, decimal Points)?> RankOfAsync(Guid seasonId, Guid studentId, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureAsync(seasonId, cancellationToken);
            var score = await Redis.SortedSetScoreAsync(BoardKey(seasonId), studentId.ToString());
            if (score is null)
            {
                return null;
            }

            var above = await Redis.SortedSetLengthAsync(BoardKey(seasonId), score.Value, double.PositiveInfinity, Exclude.Start);
            return ((int)above + 1, FromScore(score.Value));
        }
        catch (RedisException exception)
        {
            LogRedisFallback(logger, seasonId, exception);
            var row = (await StandingsFromDatabaseAsync(seasonId, cancellationToken)).FirstOrDefault(entry => entry.StudentId == studentId);
            return row is null ? null : (row.Rank, row.Points);
        }
    }

    /// <summary>BR-SEASON-05: a deactivated student disappears from the live boards.</summary>
    public async Task RemoveAsync(IEnumerable<Guid> seasonIds, Guid studentId)
    {
        foreach (var seasonId in seasonIds)
        {
            await Redis.SortedSetRemoveAsync(BoardKey(seasonId), studentId.ToString());
        }
    }

    /// <summary>Drops the ready markers so the next read rebuilds from PostgreSQL (e.g. after a reactivation).</summary>
    public async Task InvalidateAsync(IEnumerable<Guid> seasonIds)
    {
        foreach (var seasonId in seasonIds)
        {
            await Redis.KeyDeleteAsync(ReadyKey(seasonId));
        }
    }

    /// <summary>BR-SEASON-02: the new season starts from an empty board.</summary>
    public async Task StartEmptyAsync(Guid seasonId)
    {
        await Redis.KeyDeleteAsync(BoardKey(seasonId));
        await Redis.StringSetAsync(ReadyKey(seasonId), "1");
    }

    public Task SetNameAsync(Guid studentId, string displayName) =>
        Redis.HashSetAsync(NamesKey, studentId.ToString(), displayName);

    /// <summary>
    /// The ranking computed from <c>point_events</c> (sum per student, active users only); used to rebuild Redis, for the
    /// standings of the active season and when Redis is down.
    /// </summary>
    public async Task<IReadOnlyList<BoardEntry>> StandingsFromDatabaseAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var sums = await db.PointEvents.AsNoTracking()
            .Where(item => item.SeasonId == seasonId)
            .GroupBy(item => item.StudentId)
            .Select(group => new { StudentId = group.Key, Points = group.Sum(item => item.Points) })
            .ToListAsync(cancellationToken);
        if (sums.Count == 0)
        {
            return [];
        }

        var profiles = await users.GetUsersAsync([.. sums.Select(sum => sum.StudentId)], cancellationToken);
        var ranked = Ranking.Rank(sums
            .Where(sum => profiles.TryGetValue(sum.StudentId, out var profile) && profile.IsActive)
            .Select(sum => (sum.StudentId, profiles[sum.StudentId].DisplayName, sum.Points)));
        return [.. ranked.Select(row => new BoardEntry(row.StudentId, row.DisplayName, row.Points, row.Rank))];
    }

    private async Task EnsureAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        if (!await Redis.KeyExistsAsync(ReadyKey(seasonId)))
        {
            await RebuildAsync(seasonId, cancellationToken);
        }
    }

    private async Task RebuildAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var standings = await StandingsFromDatabaseAsync(seasonId, cancellationToken);

        // Build aside and swap in with RENAME so readers never see a half-filled board.
        RedisKey staging = $"lb:{seasonId}:staging:{Guid.NewGuid():N}";
        if (standings.Count > 0)
        {
            await Redis.SortedSetAddAsync(staging, [.. standings.Select(row => new SortedSetEntry(row.StudentId.ToString(), ToScore(row.Points)))]);
            await Redis.HashSetAsync(NamesKey, [.. standings.Select(row => new HashEntry(row.StudentId.ToString(), row.DisplayName))]);
            await Redis.KeyRenameAsync(staging, BoardKey(seasonId));
        }
        else
        {
            await Redis.KeyDeleteAsync(BoardKey(seasonId));
        }

        await Redis.StringSetAsync(ReadyKey(seasonId), "1");
        LogRebuilt(logger, seasonId, standings.Count);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken cancellationToken)
    {
        var cached = await Redis.HashGetAsync(NamesKey, [.. ids.Select(id => (RedisValue)id.ToString())]);
        var names = new Dictionary<Guid, string>(ids.Count);
        var missing = new List<Guid>();
        for (var i = 0; i < ids.Count; i++)
        {
            if (cached[i].HasValue)
            {
                names[ids[i]] = cached[i].ToString();
            }
            else
            {
                missing.Add(ids[i]);
            }
        }

        if (missing.Count > 0)
        {
            var profiles = await users.GetUsersAsync(missing, cancellationToken);
            foreach (var profile in profiles.Values)
            {
                names[profile.Id] = profile.DisplayName;
                await SetNameAsync(profile.Id, profile.DisplayName);
            }
        }

        return names;
    }

    private static double ToScore(decimal points) => (double)decimal.Round(points * 100m);

    private static decimal FromScore(double score) => (decimal)Math.Round(score) / 100m;

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebuilt leaderboard of season {SeasonId} from PostgreSQL ({Count} students).")]
    private static partial void LogRebuilt(ILogger logger, Guid seasonId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis unavailable for the leaderboard of season {SeasonId}; reading PostgreSQL.")]
    private static partial void LogRedisFallback(ILogger logger, Guid seasonId, Exception exception);
}
