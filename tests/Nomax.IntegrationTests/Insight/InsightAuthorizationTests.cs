using System.Net;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Insight;

/// <summary>Authorization matrix of the Insight endpoints (docs/09 §1, docs/05 §7a): no token → 401, wrong role → 403.</summary>
public sealed class InsightAuthorizationTests(NomaxApiFixture api)
{
    private const string Unknown = "00000000-0000-4000-8000-0000000000ff";

    public static TheoryData<string, string> StaffEndpoints => new()
    {
        { "GET", $"/api/insight/students/{Unknown}/activity" },
        { "GET", $"/api/insight/classes/{Unknown}/activity-summary" },
    };

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "POST", $"/api/insight/seasons/{Unknown}/close" },
    };

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    [MemberData(nameof(AdminEndpoints))]
    [InlineData("GET", "/api/insight/leaderboards")]
    [InlineData("GET", "/api/insight/seasons")]
    [InlineData("GET", $"/api/insight/seasons/{Unknown}/standings")]
    [InlineData("GET", "/api/insight/me/activity")]
    public async Task Without_a_token_every_endpoint_is_401(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Murid_gets_403_on_staff_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Murid));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Sensei_gets_403_on_admin_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Sensei));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Unknown_seasons_are_404()
    {
        using var client = api.CreateClient();
        var admin = TestUsers.TokenFor(Roles.Admin);

        (await client.StatusAsync("GET", $"/api/insight/seasons/{Unknown}/standings", admin)).ShouldBe(404);
        (await client.StatusAsync("POST", $"/api/insight/seasons/{Unknown}/close", admin)).ShouldBe(404);
        (await client.StatusAsync("GET", $"/api/insight/leaderboards?levelId={Unknown}", admin)).ShouldBe(404);
    }
}
