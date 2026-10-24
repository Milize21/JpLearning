using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Messaging;
using Nomax.IntegrationTests.Support;
using Nomax.Modules.Insight.IntegrationEvents;
using Nomax.Modules.Practice.Contracts;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Insight;

/// <summary>
/// /hubs/leaderboard through a real SignalR client over the in-memory server (long polling, bearer header): who may
/// connect, which seasons they may join, and what reaches them live (BR-SEASON-04).
/// </summary>
public sealed class LeaderboardHubTests(NomaxApiFixture api)
{
    private const string Negotiate = "/hubs/leaderboard/negotiate?negotiateVersion=1";

    private static readonly Guid MuridLevel = Guid.Parse("b0000000-0000-4000-8000-000000000001");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Connecting_needs_a_token_and_an_active_account()
    {
        using var client = api.CreateClient();

        (await client.StatusAsync("POST", Negotiate, null)).ShouldBe(401);
        using var inactive = await client.SendAsync("POST", Negotiate, TestJwt.For(TestUsers.InactiveMurid, Roles.Murid));
        await inactive.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "account_inactive");
        (await client.StatusAsync("POST", Negotiate, TestUsers.TokenFor(Roles.Murid))).ShouldBe(200);
    }

    [Fact]
    public async Task JoinSeason_returns_the_board_of_a_visible_season_and_not_found_otherwise()
    {
        using var client = api.CreateClient();
        var own = await ActiveSeasonAsync(client, MuridLevel);
        var hidden = await ActiveSeasonAsync(client, await NewLevelAsync(client));

        await using var hub = await ConnectAsync(TestUsers.TokenFor(Roles.Murid));
        var board = await hub.InvokeAsync<JsonElement>("JoinSeason", own, Cancel);

        board.GetProperty("season").GetProperty("id").GetGuid().ShouldBe(own);
        (await Should.ThrowAsync<HubException>(() => hub.InvokeAsync<JsonElement>("JoinSeason", hidden, Cancel))).Message.ShouldContain("not_found");
        (await Should.ThrowAsync<HubException>(() => hub.InvokeAsync<JsonElement>("JoinSeason", Guid.NewGuid(), Cancel))).Message.ShouldContain("not_found");
    }

    [Fact]
    public async Task BR_SEASON_04_points_reach_the_student_and_board_broadcasts_are_throttled()
    {
        using var client = api.CreateClient();
        var seasonId = await ActiveSeasonAsync(client, MuridLevel);
        var boards = new ConcurrentQueue<DateTimeOffset>();
        var myRank = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var awarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var hub = await ConnectAsync(TestUsers.TokenFor(Roles.Murid));
        hub.On<JsonElement>("PointsAwarded", _ => awarded.TrySetResult());
        hub.On<JsonElement>("MyRankUpdated", rank =>
        {
            if (rank.GetProperty("seasonId").GetGuid() == seasonId)
            {
                myRank.TrySetResult();
            }
        });
        hub.On<JsonElement>("LeaderboardUpdated", update =>
        {
            if (update.GetProperty("seasonId").GetGuid() == seasonId)
            {
                boards.Enqueue(update.GetProperty("updatedAt").GetDateTimeOffset());
            }
        });
        await hub.InvokeAsync<JsonElement>("JoinSeason", seasonId, Cancel);

        const int answers = 10;
        for (var i = 0; i < answers; i++)
        {
            await DeliverAsync(new AnswerSubmitted(Guid.CreateVersion7(), Guid.NewGuid(), TestUsers.Murid, MuridLevel, Guid.NewGuid(), Guid.NewGuid(),
                "kana", Guid.NewGuid(), "hiragana", true, "choice", DateTimeOffset.UtcNow));
        }

        await awarded.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await myRank.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await Task.Delay(TimeSpan.FromSeconds(3), Cancel); // let the broadcaster run a few ticks

        // One board per tick (1 s) at most, however many points arrive: the server stamps each broadcast.
        var sent = boards.ToArray();
        sent.Length.ShouldBeGreaterThan(0);
        sent.Length.ShouldBeLessThan(answers);
        for (var i = 1; i < sent.Length; i++)
        {
            (sent[i] - sent[i - 1]).ShouldBeGreaterThan(TimeSpan.FromMilliseconds(500));
        }
    }

    private async Task<HubConnection> ConnectAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "hubs/leaderboard"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        await connection.StartAsync(Cancel);
        return connection;
    }

    private static async Task<Guid> ActiveSeasonAsync(HttpClient client, Guid levelId) =>
        (await client.JsonAsync("GET", $"/api/insight/leaderboards?levelId={levelId}", TestUsers.TokenFor(Roles.Admin)))
        .GetProperty("board").GetProperty("season").GetProperty("id").GetGuid();

    private static async Task<Guid> NewLevelAsync(HttpClient client)
    {
        var code = $"hb{Guid.NewGuid():N}"[..12];
        var level = await client.JsonAsync("POST", "/api/content/levels", TestUsers.TokenFor(Roles.Admin), new { code, name = new { id = "Hub" }, sortOrder = 50 });
        return level.GetProperty("id").GetGuid();
    }

    private async Task DeliverAsync(AnswerSubmitted answer)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AnswerSubmittedConsumer>().HandleAsync(
            new IntegrationEventEnvelope<AnswerSubmitted>(Guid.CreateVersion7(), answer.AnsweredAt, answer), Cancel);
    }
}
