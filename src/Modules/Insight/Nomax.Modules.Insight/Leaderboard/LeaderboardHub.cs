using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Auth;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.SharedKernel;

namespace Nomax.Modules.Insight.Leaderboard;

public sealed record LeaderboardUpdatedMessage(Guid SeasonId, IReadOnlyList<LeaderboardEntryDto> Top, DateTimeOffset UpdatedAt);

public sealed record PointsAwardedMessage(decimal Points, string Rule, PointSource SourceType);

public sealed record SeasonClosedMessage(Guid SeasonId, Guid NewSeasonId);

/// <summary>
/// <c>/hubs/leaderboard</c> (docs/05 §9). Group messages carry only entry keys, names and points; the caller's own rank
/// and awarded points go to that user alone.
/// </summary>
[Authorize(Policy = Policies.Authenticated)]
internal sealed class LeaderboardHub(LeaderboardQueries queries) : Hub
{
    public const string Path = "/hubs/leaderboard";

    public static string Group(Guid seasonId) => $"season:{seasonId}";

    /// <summary>Joins the group of a visible active season and returns the current board.</summary>
    public async Task<LeaderboardDto> JoinSeason(Guid seasonId)
    {
        var viewer = new Viewer(
            Context.User?.FindUserId() ?? throw new HubException("unauthorized"),
            Context.User.IsInRole(Roles.Murid));
        var season = await queries.SeasonAsync(viewer, seasonId, Context.ConnectionAborted);
        if (season is not { Status: SeasonStatus.Active })
        {
            throw new HubException("not_found");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(seasonId), Context.ConnectionAborted);
        return await queries.BoardAsync(viewer, season, Context.ConnectionAborted);
    }

    public Task LeaveSeason(Guid seasonId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(seasonId));
}

/// <summary>SignalR user id = Keycloak <c>sub</c>, so <c>Clients.User(studentId)</c> reaches every tab of the student.</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindUserId()?.ToString();
}

/// <summary>BR-SEASON-04: a changed board is broadcast to its group at most once per interval.</summary>
internal sealed partial class LeaderboardBroadcaster(
    IServiceScopeFactory scopeFactory,
    IHubContext<LeaderboardHub> hub,
    IOptions<LeaderboardOptions> options,
    TimeProvider clock,
    ILogger<LeaderboardBroadcaster> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, byte> _dirty = new();

    public void MarkDirty(Guid seasonId) => _dirty.TryAdd(seasonId, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.Value.BroadcastIntervalMs), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var seasonId in _dirty.Keys)
            {
                _dirty.TryRemove(seasonId, out _);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var queries = scope.ServiceProvider.GetRequiredService<LeaderboardQueries>();
                    var top = await queries.TopAsync(seasonId, stoppingToken);
                    await hub.Clients.Group(LeaderboardHub.Group(seasonId))
                        .SendAsync("LeaderboardUpdated", new LeaderboardUpdatedMessage(seasonId, top, clock.GetUtcNow()), stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    LogBroadcastFailed(logger, seasonId, exception);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Leaderboard broadcast for season {SeasonId} failed.")]
    private static partial void LogBroadcastFailed(ILogger logger, Guid seasonId, Exception exception);
}

/// <summary>After a committed point event: Redis score, the student's own messages, and a board broadcast.</summary>
internal sealed partial class LeaderboardNotifier(
    IHubContext<LeaderboardHub> hub,
    LeaderboardStore store,
    LeaderboardQueries queries,
    LeaderboardBroadcaster broadcaster,
    ILogger<LeaderboardNotifier> logger)
{
    public async Task PointsAwardedAsync(PointEvent awarded, CancellationToken cancellationToken)
    {
        try
        {
            if (awarded.Points > 0)
            {
                await store.AddAsync(awarded.SeasonId, awarded.StudentId, awarded.Points, cancellationToken);
                broadcaster.MarkDirty(awarded.SeasonId);
            }

            var student = hub.Clients.User(awarded.StudentId.ToString());
            await student.SendAsync("PointsAwarded", new PointsAwardedMessage(awarded.Points, awarded.Rule, awarded.SourceType), cancellationToken);
            if (await queries.MyRankAsync(awarded.SeasonId, awarded.StudentId, cancellationToken) is { } rank)
            {
                await student.SendAsync("MyRankUpdated", rank, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is StackExchange.Redis.RedisException or InvalidOperationException)
        {
            // PostgreSQL already has the points; a missed push only delays the live view.
            LogNotifyFailed(logger, awarded.SeasonId, exception);
        }
    }

    public async Task SeasonClosedAsync(Guid seasonId, Guid newSeasonId, CancellationToken cancellationToken)
    {
        try
        {
            await store.StartEmptyAsync(newSeasonId);
        }
        catch (StackExchange.Redis.RedisException exception)
        {
            LogNotifyFailed(logger, newSeasonId, exception);
        }

        await hub.Clients.Group(LeaderboardHub.Group(seasonId)).SendAsync("SeasonClosed", new SeasonClosedMessage(seasonId, newSeasonId), cancellationToken);
    }

    /// <summary>Boards that may show the user get rebuilt or re-broadcast (deactivation, reactivation, renaming).</summary>
    public async Task UserChangedAsync(Guid userId, IReadOnlyList<Guid> activeSeasonIds, UserChange change, string? displayName = null)
    {
        try
        {
            switch (change)
            {
                case UserChange.Deactivated:
                    await store.RemoveAsync(activeSeasonIds, userId);
                    break;
                case UserChange.Reactivated:
                    await store.InvalidateAsync(activeSeasonIds);
                    break;
                case UserChange.Renamed when displayName is not null:
                    await store.SetNameAsync(userId, displayName);
                    break;
            }
        }
        catch (StackExchange.Redis.RedisException exception)
        {
            LogNotifyFailed(logger, userId, exception);
        }

        foreach (var seasonId in activeSeasonIds)
        {
            broadcaster.MarkDirty(seasonId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Live leaderboard update for {Id} failed; PostgreSQL stays correct.")]
    private static partial void LogNotifyFailed(ILogger logger, Guid id, Exception exception);
}

internal enum UserChange
{
    Deactivated,
    Reactivated,
    Renamed,
}
