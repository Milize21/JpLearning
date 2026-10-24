using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Messaging;
using Nomax.IntegrationTests.Support;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.Modules.Insight.IntegrationEvents;
using Nomax.Modules.Insight.Leaderboard;
using Nomax.Modules.Practice.Contracts;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Events;
using Nomax.SharedKernel.Serialization;
using StackExchange.Redis;

namespace Nomax.IntegrationTests.Insight;

/// <summary>
/// Points, seasons and the live board (docs/08 §7 acceptance criteria). Each test works on a level of its own, so drilling
/// done by other tests never mixes into its season. Events are handed to the consumers directly, as the outbox would.
/// </summary>
public sealed class InsightEndpointsTests(NomaxApiFixture api)
{
    private static readonly Guid OtherMurid = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private static string Admin => TestUsers.TokenFor(Roles.Admin);

    [Fact]
    public async Task BR_POIN_10_the_same_event_twice_gives_points_once_and_the_board_shows_them()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        var answer = Answer(TestUsers.Murid, levelId, Guid.NewGuid());
        var eventId = Guid.CreateVersion7();

        await DeliverAsync(answer, eventId);
        await DeliverAsync(answer, eventId);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InsightDbContext>();
            var events = await db.PointEvents.Where(item => item.SourceId == answer.AnswerId).ToListAsync(TestContext.Current.CancellationToken);
            events.Count.ShouldBe(1);
            events[0].Points.ShouldBe(1m);
            events[0].Rule.ShouldBe(PointAward.DrillBase);
            events[0].ItemKey.ShouldBe($"kana:{answer.ItemId}");
        }

        var board = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin)).GetProperty("board");
        var top = board.GetProperty("top");
        top.GetArrayLength().ShouldBe(1);
        top[0].GetProperty("rank").GetInt32().ShouldBe(1);
        top[0].GetProperty("points").GetDecimal().ShouldBe(1m);
        top[0].GetProperty("entryKey").GetString()!.Length.ShouldBe(16);
        board.GetProperty("me").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task BR_POIN_cases_1_2_5_repeats_halve_wrong_answers_and_self_checks_count_nothing()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        var itemId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 20, 3, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 5; i++)
        {
            await DeliverAsync(Answer(OtherMurid, levelId, itemId, at.AddMinutes(i)));
            await DeliverAsync(Answer(OtherMurid, levelId, itemId, at.AddMinutes(i).AddSeconds(30), isCorrect: false));
        }

        await DeliverAsync(Answer(OtherMurid, levelId, Guid.NewGuid(), at.AddMinutes(10), inputMode: "self_check"));

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InsightDbContext>();
        var points = await db.PointEvents
            .Where(item => item.StudentId == OtherMurid && item.LevelId == levelId)
            .OrderBy(item => item.Ts)
            .Select(item => new { item.Points, item.Rule })
            .ToListAsync(TestContext.Current.CancellationToken);
        points.Select(item => item.Points).ShouldBe([1m, 1m, 1m, 0.5m, 0.5m, 0m]);
        points[^1].Rule.ShouldBe(PointAward.SelfCheckZero);
    }

    [Fact]
    public async Task BR_POIN_case_4_a_fourth_answer_at_2359_WIB_counts_half_and_the_next_at_0001_WIB_counts_full()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        var studentId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var morning = new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero); // 10:00 WIB

        for (var i = 0; i < 3; i++)
        {
            await DeliverAsync(Answer(studentId, levelId, itemId, morning.AddMinutes(i)));
        }

        await DeliverAsync(Answer(studentId, levelId, itemId, new DateTimeOffset(2026, 10, 22, 16, 59, 0, TimeSpan.Zero))); // 23:59 WIB
        await DeliverAsync(Answer(studentId, levelId, itemId, new DateTimeOffset(2026, 10, 22, 17, 1, 0, TimeSpan.Zero))); // 00:01 WIB

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InsightDbContext>();
        var points = await db.PointEvents.Where(item => item.StudentId == studentId).OrderBy(item => item.Ts).Select(item => item.Points)
            .ToListAsync(TestContext.Current.CancellationToken);
        points.ShouldBe([1m, 1m, 1m, 0.5m, 1m]);
    }

    [Fact]
    public async Task BR_SEASON_01_concurrent_first_uses_open_exactly_one_active_season()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);

        var opened = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var scope = api.Services.CreateAsyncScope();
            var season = await scope.ServiceProvider.GetRequiredService<SeasonService>().ActiveAsync(levelId, TestContext.Current.CancellationToken);
            return season!.Id;
        }));

        opened.Distinct().Count().ShouldBe(1);
        await using var check = api.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<InsightDbContext>();
        (await db.Seasons.CountAsync(season => season.LevelId == levelId, TestContext.Current.CancellationToken)).ShouldBe(1);
        (await check.ServiceProvider.GetRequiredService<SeasonService>().ActiveAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task BR_SEASON_05_deactivated_students_leave_the_board_and_return_with_their_points()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        var name = $"Papan {Guid.NewGuid():N}"[..14];
        var created = await client.JsonAsync("POST", "/api/identity/users", Admin, new { username = $"papan.{Guid.NewGuid():N}"[..14], displayName = name, role = "murid" });
        var studentId = created.GetProperty("user").GetProperty("id").GetGuid();
        await DeliverAsync(Answer(studentId, levelId, Guid.NewGuid()));
        await DeliverAsync(Answer(studentId, levelId, Guid.NewGuid()));
        (await TopAsync(client, levelId)).ShouldContainKey(name);

        await client.JsonAsync("POST", $"/api/identity/users/{studentId}/deactivate", Admin);
        await DeliverAsync<UserDeactivated, UserDeactivatedConsumer>(new UserDeactivated(studentId));
        (await TopAsync(client, levelId)).ShouldNotContainKey(name);

        // A rebuild from PostgreSQL keeps them hidden.
        var seasonId = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin)).GetProperty("board").GetProperty("season").GetProperty("id").GetGuid();
        await api.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase()
            .KeyDeleteAsync([LeaderboardStore.BoardKey(seasonId), LeaderboardStore.ReadyKey(seasonId)]);
        (await TopAsync(client, levelId)).ShouldNotContainKey(name);

        await client.JsonAsync("POST", $"/api/identity/users/{studentId}/activate", Admin);
        await DeliverAsync<UserReactivated, UserReactivatedConsumer>(new UserReactivated(studentId));
        (await TopAsync(client, levelId))[name].ShouldBe(2m);
    }

    [Fact]
    public async Task BR_POIN_08_quiz_points_count_only_improvements()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        var quizId = Guid.NewGuid();

        foreach (var (score, attempt) in new[] { (85m, 1), (92m, 2), (80m, 3) })
        {
            var graded = new QuizGraded(Guid.CreateVersion7(), quizId, "quiz", Guid.NewGuid(), levelId, TestUsers.Murid, score, attempt, DateTimeOffset.UtcNow);
            await using var scope = api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<QuizGradedConsumer>()
                .HandleAsync(new IntegrationEventEnvelope<QuizGraded>(Guid.CreateVersion7(), DateTimeOffset.UtcNow, graded), TestContext.Current.CancellationToken);
        }

        await using var check = api.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<InsightDbContext>();
        var points = await db.PointEvents.Where(item => item.ItemKey == $"quiz:{quizId}").OrderBy(item => item.Ts).Select(item => item.Points)
            .ToListAsync(TestContext.Current.CancellationToken);
        points.ShouldBe([85m, 7m, 0m]);
        (await db.ActivityLogs.CountAsync(log => log.UserId == TestUsers.Murid && log.Type == "quiz_graded", TestContext.Current.CancellationToken))
            .ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task BR_SEASON_02_closing_archives_the_ranking_and_starts_an_empty_board()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        await DeliverAsync(Answer(TestUsers.Murid, levelId, Guid.NewGuid()));
        await DeliverAsync(Answer(OtherMurid, levelId, Guid.NewGuid()));
        await DeliverAsync(Answer(OtherMurid, levelId, Guid.NewGuid()));
        var before = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin)).GetProperty("board");
        var oldSeason = before.GetProperty("season").GetProperty("id").GetGuid();

        var next = await client.JsonAsync("POST", $"/api/insight/seasons/{oldSeason}/close", Admin);
        (await client.StatusAsync("POST", $"/api/insight/seasons/{oldSeason}/close", Admin)).ShouldBe(409);

        next.GetProperty("name").GetString()!.ShouldEndWith("· Musim 2");
        var after = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin)).GetProperty("board");
        after.GetProperty("season").GetProperty("id").GetGuid().ShouldBe(next.GetProperty("id").GetGuid());
        after.GetProperty("top").GetArrayLength().ShouldBe(0);

        var archive = await client.JsonAsync("GET", $"/api/insight/seasons/{oldSeason}/standings", Admin);
        archive.GetProperty("season").GetProperty("status").GetString().ShouldBe("closed");
        archive.GetProperty("totalCount").GetInt32().ShouldBe(2);
        archive.GetProperty("items")[0].GetProperty("points").GetDecimal().ShouldBe(2m);
        archive.GetProperty("items")[1].GetProperty("rank").GetInt32().ShouldBe(2);

        var seasons = await client.JsonAsync("GET", $"/api/insight/seasons?levelId={levelId}", Admin);
        seasons.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Redis_can_be_flushed_and_is_rebuilt_from_PostgreSQL()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        await DeliverAsync(Answer(TestUsers.Murid, levelId, Guid.NewGuid()));
        await DeliverAsync(Answer(TestUsers.Murid, levelId, Guid.NewGuid()));
        var board = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin)).GetProperty("board");
        var seasonId = board.GetProperty("season").GetProperty("id").GetGuid();

        var redis = api.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        await redis.KeyDeleteAsync([LeaderboardStore.BoardKey(seasonId), LeaderboardStore.ReadyKey(seasonId)]);

        var rebuilt = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin)).GetProperty("board").GetProperty("top");
        rebuilt.GetArrayLength().ShouldBe(1);
        rebuilt[0].GetProperty("points").GetDecimal().ShouldBe(2m);
        (await redis.SortedSetScoreAsync(LeaderboardStore.BoardKey(seasonId), TestUsers.Murid.ToString())).ShouldBe(200);
    }

    [Fact]
    public async Task Hub_board_messages_carry_entry_keys_but_no_user_ids()
    {
        using var client = api.CreateClient();
        var levelId = await NewLevelAsync(client);
        await DeliverAsync(Answer(TestUsers.Murid, levelId, Guid.NewGuid()));
        await DeliverAsync(Answer(OtherMurid, levelId, Guid.NewGuid()));
        var seasonId = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin))
            .GetProperty("board").GetProperty("season").GetProperty("id").GetGuid();

        await using var scope = api.Services.CreateAsyncScope();
        var top = await scope.ServiceProvider.GetRequiredService<LeaderboardQueries>().TopAsync(seasonId, TestContext.Current.CancellationToken);
        var json = JsonSerializer.Serialize(new LeaderboardUpdatedMessage(seasonId, top, DateTimeOffset.UtcNow), NomaxJson.Options);

        top.Count.ShouldBe(2);
        json.ShouldNotContain(TestUsers.Murid.ToString());
        json.ShouldNotContain(OtherMurid.ToString());
        json.ShouldContain("\"entryKey\"");
    }

    [Fact]
    public async Task Students_see_only_the_levels_of_their_classes_and_their_own_rank()
    {
        using var client = api.CreateClient();
        var murid = TestUsers.TokenFor(Roles.Murid);
        var otherLevel = await NewLevelAsync(client);

        var mine = await client.JsonAsync("GET", "/api/insight/leaderboards", murid);
        var levels = mine.GetProperty("levels").EnumerateArray().Select(level => level.GetProperty("levelId").GetGuid()).ToList();
        levels.ShouldContain(Guid.Parse("b0000000-0000-4000-8000-000000000001"));
        levels.ShouldNotContain(otherLevel);
        (await client.StatusAsync("GET", $"/api/insight/leaderboards?levelId={otherLevel}", murid)).ShouldBe(404);

        var otherSeason = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={otherLevel}", Admin))
            .GetProperty("board").GetProperty("season").GetProperty("id").GetGuid();
        (await client.StatusAsync("GET", $"/api/insight/seasons/{otherSeason}/standings", murid)).ShouldBe(404);
        (await client.StatusAsync("GET", $"/api/insight/seasons?levelId={otherLevel}", murid)).ShouldBe(404);
    }

    [Fact]
    public async Task Activity_log_pages_and_the_class_summary_respect_ownership()
    {
        using var client = api.CreateClient();
        var ended = new DrillSessionEnded(Guid.CreateVersion7(), TestUsers.Murid, 10, 8, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DrillSessionEndedConsumer>()
                .HandleAsync(new IntegrationEventEnvelope<DrillSessionEnded>(Guid.CreateVersion7(), DateTimeOffset.UtcNow, ended), TestContext.Current.CancellationToken);
        }

        var page = await client.JsonAsync("GET", "/api/insight/me/activity", TestUsers.TokenFor(Roles.Murid));
        page.GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("ref").GetString() == $"drill_session:{ended.SessionId}");
        (await client.StatusAsync("GET", "/api/insight/me/activity?cursor=bad!", TestUsers.TokenFor(Roles.Murid))).ShouldBe(400);

        // The student's own log hides which staff account graded the work; the teacher's view keeps it.
        var graded = new SubmissionGraded(Guid.CreateVersion7(), Guid.NewGuid(), Guid.NewGuid(), TestUsers.Murid, 90m, TestUsers.Sensei, DateTimeOffset.UtcNow);
        await DeliverAsync<SubmissionGraded, SubmissionGradedConsumer>(graded);
        var mine = (await client.JsonAsync("GET", "/api/insight/me/activity", TestUsers.TokenFor(Roles.Murid))).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("ref").GetString() == $"submission:{graded.SubmissionId}");
        mine.GetProperty("meta").TryGetProperty("gradedBy", out _).ShouldBeFalse();
        mine.GetProperty("meta").GetProperty("gradePct").GetDecimal().ShouldBe(90m);

        var sensei = TestUsers.TokenFor(Roles.Sensei);
        var teacherView = (await client.JsonAsync("GET", $"/api/insight/students/{TestUsers.Murid}/activity", sensei)).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("ref").GetString() == $"submission:{graded.SubmissionId}");
        teacherView.GetProperty("meta").GetProperty("gradedBy").GetGuid().ShouldBe(TestUsers.Sensei);
        (await client.StatusAsync("GET", $"/api/insight/students/{Guid.NewGuid()}/activity", sensei)).ShouldBe(404);

        var summary = await client.JsonAsync("GET", "/api/insight/classes/c0000000-0000-4000-8000-000000000001/activity-summary", sensei);
        var row = summary.EnumerateArray().Single(item => item.GetProperty("studentId").GetGuid() == TestUsers.Murid);
        row.GetProperty("lastActiveAt").ValueKind.ShouldBe(JsonValueKind.String);
        (await client.StatusAsync("GET", $"/api/insight/classes/{Guid.NewGuid()}/activity-summary", sensei)).ShouldBe(404);
    }

    private static async Task<Guid> NewLevelAsync(HttpClient client)
    {
        var code = $"lb{Guid.NewGuid():N}"[..12];
        var level = await client.JsonAsync("POST", "/api/content/levels", Admin, new { code, name = new { id = "Papan" }, sortOrder = 50 });
        return level.GetProperty("id").GetGuid();
    }

    private static AnswerSubmitted Answer(
        Guid studentId, Guid levelId, Guid itemId, DateTimeOffset? at = null, bool isCorrect = true, string inputMode = "choice") =>
        new(Guid.CreateVersion7(), Guid.NewGuid(), studentId, levelId, Guid.NewGuid(), Guid.NewGuid(), "kana", itemId, "hiragana", isCorrect, inputMode,
            at ?? DateTimeOffset.UtcNow);

    private static async Task<Dictionary<string, decimal>> TopAsync(HttpClient client, Guid levelId)
    {
        var board = (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", Admin)).GetProperty("board");
        return board.GetProperty("top").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("displayName").GetString()!, entry => entry.GetProperty("points").GetDecimal());
    }

    private async Task DeliverAsync<TEvent, TConsumer>(TEvent payload)
        where TEvent : IIntegrationEvent
        where TConsumer : IIntegrationEventConsumer<TEvent>
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TConsumer>().HandleAsync(
            new IntegrationEventEnvelope<TEvent>(Guid.CreateVersion7(), DateTimeOffset.UtcNow, payload), TestContext.Current.CancellationToken);
    }

    private async Task DeliverAsync(AnswerSubmitted answer, Guid? eventId = null)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AnswerSubmittedConsumer>().HandleAsync(
            new IntegrationEventEnvelope<AnswerSubmitted>(eventId ?? Guid.CreateVersion7(), answer.AnsweredAt, answer), TestContext.Current.CancellationToken);
    }
}
