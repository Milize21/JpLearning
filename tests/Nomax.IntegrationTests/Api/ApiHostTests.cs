using System.Net;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using static Nomax.IntegrationTests.Support.HttpAssertions;

namespace Nomax.IntegrationTests.Api;

/// <summary>
/// Smoke test of the real Nomax.Api Program.cs on an empty database.
/// </summary>
public sealed class ApiHostTests(NomaxApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Starts_and_reports_live_and_ready()
    {
        using var client = api.CreateClient();

        using var live = await client.SendAsync(Get("/health/live"), Cancel);
        using var ready = await client.SendAsync(Get("/health/ready"), Cancel);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        // Keycloak is not running in tests, so readiness is reported as unhealthy; PostgreSQL and Valkey are real.
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Requires_a_token_for_api_routes()
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(Get("/api/identity/me"), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Serves_the_identity_module_with_seeded_profiles()
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(Get("/api/identity/me", TestUsers.TokenFor(Roles.Admin)), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("username").GetString().ShouldBe("admin.dev");
    }

    [Fact]
    public async Task Publishes_the_identity_openapi_document_in_development()
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(Get("/openapi/identity.json"), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync(Cancel);
        document.ShouldContain("/api/identity/users/import");
        document.ShouldContain("\"kanji_furigana_romaji\"");
    }
}
