using System.Net;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using static Nomax.IntegrationTests.Support.HttpAssertions;

namespace Nomax.IntegrationTests.Gateway;

/// <summary>
/// Each test gets its own Gateway so rate-limit counters never leak between tests.
/// </summary>
public sealed class GatewayTests : IAsyncLifetime
{
    private readonly GatewayFactory _factory = new();
    private HttpClient _client = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Health_endpoints_answer_without_a_token()
    {
        using var live = await _client.SendAsync(Get("/health/live"), Cancel);
        using var ready = await _client.SendAsync(Get("/health/ready"), Cancel);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Api.Requests.ShouldContain(request => request.Path == "/health/live");
    }

    [Fact]
    public async Task Api_route_without_a_token_is_rejected_before_forwarding()
    {
        using var response = await _client.SendAsync(Get("/api/identity/me"), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
        _factory.Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Api_route_with_a_valid_token_is_forwarded_with_the_token()
    {
        var token = TestUsers.TokenFor(Roles.Murid);

        using var response = await _client.SendAsync(Get("/api/identity/me", token), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forwarded = _factory.Api.Requests.ShouldHaveSingleItem();
        forwarded.Path.ShouldBe("/api/identity/me");
        forwarded.Authorization.ShouldBe($"Bearer {token}");
    }

    [Fact]
    public async Task Token_in_the_query_is_ignored_outside_hubs()
    {
        var token = TestUsers.TokenFor(Roles.Murid);

        using var api = await _client.SendAsync(Get($"/api/identity/me?access_token={token}"), Cancel);
        using var hub = await _client.SendAsync(Get($"/hubs/leaderboard/negotiate?access_token={token}"), Cancel);

        await api.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
        hub.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Api.Requests.ShouldHaveSingleItem().Path.ShouldBe("/hubs/leaderboard/negotiate");
    }

    [Fact]
    public async Task Ical_feed_is_forwarded_without_a_token()
    {
        using var response = await _client.SendAsync(Get("/ical/feed-token.ics"), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Api.Requests.ShouldHaveSingleItem().Path.ShouldBe("/ical/feed-token.ics");
    }

    [Fact]
    public async Task Openapi_is_forwarded_without_a_token_in_development()
    {
        using var response = await _client.SendAsync(Get("/openapi/identity.json"), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unrouted_path_needs_a_token_then_is_404()
    {
        using var anonymous = await _client.SendAsync(Get("/elsewhere"), Cancel);
        using var signedIn = await _client.SendAsync(Get("/elsewhere", TestUsers.TokenFor(Roles.Admin)), Cancel);

        await anonymous.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
        await signedIn.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Security_headers_are_on_forwarded_and_rejected_responses()
    {
        using var forwarded = await _client.SendAsync(Get("/api/x", TestUsers.TokenFor(Roles.Murid)), Cancel);
        using var rejected = await _client.SendAsync(Get("/api/x"), Cancel);

        foreach (var response in new[] { forwarded, rejected })
        {
            response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
            response.Headers.GetValues("Referrer-Policy").ShouldBe(["strict-origin-when-cross-origin"]);
            response.Headers.GetValues("Permissions-Policy").ShouldBe(["camera=(), microphone=(), geolocation=()"]);
            response.Headers.GetValues("Content-Security-Policy").ShouldBe(["default-src 'none'; frame-ancestors 'none'; base-uri 'none'"]);
        }
    }

    [Fact]
    public async Task Rate_limit_is_per_user_with_retry_after()
    {
        var murid = TestUsers.TokenFor(Roles.Murid);
        for (var i = 0; i < GatewayFactory.PerUserPerMinute; i++)
        {
            using var allowed = await _client.SendAsync(Get("/api/x", murid), Cancel);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var limited = await _client.SendAsync(Get("/api/x", murid), Cancel);
        using var otherUser = await _client.SendAsync(Get("/api/x", TestUsers.TokenFor(Roles.Sensei)), Cancel);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limited");
        int.Parse(limited.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBeInRange(1, 60);
        otherUser.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ical_feed_is_rate_limited_per_ip()
    {
        for (var i = 0; i < GatewayFactory.IcalPerIpPerMinute; i++)
        {
            using var allowed = await _client.SendAsync(Get("/ical/feed-token.ics"), Cancel);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var limited = await _client.SendAsync(Get("/ical/feed-token.ics"), Cancel);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limited");
    }
}
