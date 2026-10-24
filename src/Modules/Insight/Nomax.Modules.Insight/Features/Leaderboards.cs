using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Messaging;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Insight.Contracts;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.Modules.Insight.IntegrationEvents;
using Nomax.Modules.Insight.Leaderboard;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Paging;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Insight.Features;

/// <param name="Me">The caller's row when they are a student with points in the season.</param>
public sealed record StandingsPage(SeasonDto Season, IReadOnlyList<LeaderboardEntryDto> Items, int Page, int PageSize, int TotalCount, MyRankDto? Me);

/// <summary>
/// Boards and seasons (docs/05 §7a): GET /leaderboards, GET /seasons, GET /seasons/{id}/standings for every role (students
/// only for the levels of their classes) and POST /seasons/{id}/close for admins (BR-SEASON-02).
/// </summary>
internal sealed class LeaderboardsHandler(
    InsightDbContext db,
    ICurrentUser currentUser,
    LeaderboardQueries queries,
    LeaderboardStore store,
    SeasonService seasons,
    IUserDirectory users,
    LeaderboardNotifier notifier,
    TimeProvider clock)
{
    private Viewer Viewer => new(currentUser.Id, currentUser.IsInRole(Roles.Murid));

    public async Task<Result<LeaderboardsResponse>> LeaderboardsAsync(Guid? levelId, CancellationToken cancellationToken)
    {
        var response = await queries.LeaderboardsAsync(Viewer, levelId, cancellationToken);
        return levelId is not null && response.Board is null ? AppError.NotFound() : response;
    }

    public async Task<Result<IReadOnlyList<SeasonDto>>> SeasonsAsync(Guid? levelId, CancellationToken cancellationToken)
    {
        var visible = (await queries.VisibleLevelsAsync(Viewer, cancellationToken)).Select(level => level.Id).ToList();
        if (levelId is not null && !visible.Contains(levelId.Value))
        {
            return AppError.NotFound();
        }

        var levels = levelId is null ? visible : [levelId.Value];
        var rows = await db.Seasons.AsNoTracking()
            .Where(season => levels.Contains(season.LevelId))
            .OrderByDescending(season => season.StartsAt)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SeasonDto>>([.. rows.Select(LeaderboardQueries.ToDto)]);
    }

    /// <summary>The archived ranking of a closed season, or the ranking so far of the active one.</summary>
    public async Task<Result<StandingsPage>> StandingsAsync(Guid seasonId, PageRequest page, CancellationToken cancellationToken)
    {
        var viewer = Viewer;
        if (await queries.SeasonAsync(viewer, seasonId, cancellationToken) is not { } season)
        {
            return AppError.NotFound();
        }

        IReadOnlyList<BoardEntry> rows = season.Status == SeasonStatus.Closed
            ? [.. (await db.SeasonStandings.AsNoTracking()
                    .Where(row => row.SeasonId == seasonId)
                    .OrderBy(row => row.Rank)
                    .ToListAsync(cancellationToken))
                .OrderBy(row => row.Rank).ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(row => new BoardEntry(row.StudentId, row.DisplayName, row.Points, row.Rank))]
            : await store.StandingsFromDatabaseAsync(seasonId, cancellationToken);

        var mine = viewer.IsStudent ? rows.FirstOrDefault(row => row.StudentId == viewer.Id) : null;
        var me = mine is null ? null : new MyRankDto(seasonId, mine.Rank, mine.Points, queries.ToEntry(seasonId, mine).EntryKey);
        return new StandingsPage(
            LeaderboardQueries.ToDto(season),
            [.. rows.Skip(page.Skip).Take(page.PageSize).Select(row => queries.ToEntry(seasonId, row))],
            page.Page,
            page.PageSize,
            rows.Count,
            me);
    }

    /// <summary>
    /// BR-SEASON-02 in one transaction: sum the points per student → <c>season_standings</c> → close → open "{level} · Musim
    /// {n+1}". After the commit the new board starts empty in Redis and the hub announces <c>SeasonClosed</c>.
    /// </summary>
    public async Task<Result<SeasonDto>> CloseAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var adminId = currentUser.Id;
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The conditional update makes two admins closing at once safe: only one of them changes the row.
        var closedRows = await db.Seasons
            .Where(season => season.Id == seasonId && season.Status == SeasonStatus.Active)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(season => season.Status, SeasonStatus.Closed)
                    .SetProperty(season => season.EndsAt, now)
                    .SetProperty(season => season.ClosedBy, adminId)
                    .SetProperty(season => season.UpdatedAt, now),
                cancellationToken);
        if (closedRows == 0)
        {
            return await db.Seasons.AnyAsync(season => season.Id == seasonId, cancellationToken)
                ? AppError.InvalidState("Only an active season can be closed.")
                : AppError.NotFound();
        }

        var closed = await db.Seasons.AsNoTracking().FirstAsync(season => season.Id == seasonId, cancellationToken);
        var sums = await db.PointEvents.AsNoTracking()
            .Where(item => item.SeasonId == seasonId)
            .GroupBy(item => item.StudentId)
            .Select(group => new { StudentId = group.Key, Points = group.Sum(item => item.Points) })
            .ToListAsync(cancellationToken);
        var profiles = sums.Count == 0
            ? new Dictionary<Guid, UserSummary>()
            : await users.GetUsersAsync([.. sums.Select(sum => sum.StudentId)], cancellationToken);
        var ranked = Ranking.Rank(sums
            .Where(sum => profiles.TryGetValue(sum.StudentId, out var profile) && profile.IsActive)
            .Select(sum => (sum.StudentId, profiles[sum.StudentId].DisplayName, sum.Points)));
        db.SeasonStandings.AddRange(ranked.Select(row => SeasonStanding.Create(seasonId, row.StudentId, row.Rank, row.Points, row.DisplayName)));

        var successor = await seasons.SuccessorAsync(closed, cancellationToken);
        db.Seasons.Add(successor);
        db.ActivityLogs.Add(ActivityLog.Create(
            Uuid7.New(clock), adminId, ActivityTypes.SeasonClosed, $"season:{seasonId}",
            JsonSerializer.SerializeToElement(new { closed.LevelId, NewSeasonId = successor.Id, Students = ranked.Count }, NomaxJson.Options),
            now));
        db.AddIntegrationEvent(new SeasonClosed(seasonId, closed.LevelId, successor.Id, now), clock);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await notifier.SeasonClosedAsync(seasonId, successor.Id, cancellationToken);
        return LeaderboardQueries.ToDto(successor);
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/leaderboards", async (Guid? levelId, LeaderboardsHandler handler, CancellationToken cancellationToken) =>
                (await handler.LeaderboardsAsync(levelId, cancellationToken)).ToOkResult())
            .WithName("GetLeaderboards")
            .WithSummary("Visible levels and the live board of the active season of one level (students: levels of their classes)")
            .Produces<LeaderboardsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/seasons", async (Guid? levelId, LeaderboardsHandler handler, CancellationToken cancellationToken) =>
                (await handler.SeasonsAsync(levelId, cancellationToken)).ToOkResult())
            .WithName("ListSeasons")
            .WithSummary("Active and archived seasons, newest first")
            .Produces<IReadOnlyList<SeasonDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/seasons/{id:guid}/standings", async (Guid id, int? page, int? pageSize, LeaderboardsHandler handler, CancellationToken cancellationToken) =>
                (await handler.StandingsAsync(id, PageRequest.Create(page, pageSize), cancellationToken)).ToOkResult())
            .WithName("GetSeasonStandings")
            .WithSummary("Ranking of a season: the archive of a closed one, the standings so far of the active one")
            .Produces<StandingsPage>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/seasons/{id:guid}/close", async (Guid id, LeaderboardsHandler handler, CancellationToken cancellationToken) =>
                (await handler.CloseAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("CloseSeason")
            .WithSummary("Archive the ranking and open the next season of the level (admin, BR-SEASON-02); returns the new season")
            .Produces<SeasonDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
