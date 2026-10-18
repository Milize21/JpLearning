using System.Net;
using System.Net.Http.Json;
using System.Text;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using static Nomax.IntegrationTests.Support.HttpAssertions;

namespace Nomax.IntegrationTests.Api;

public sealed class ApiPipelineTests(ProbeApiFixture fixture) : IClassFixture<ProbeApiFixture>
{
    private HttpClient Client => fixture.Client;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_live_is_anonymous()
    {
        using var response = await Client.SendAsync(Get("/health/live"), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Missing_token_is_401_unauthorized()
    {
        using var response = await Client.SendAsync(Get("/api/probe/authenticated"), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Token_signed_by_another_key_is_401()
    {
        var token = TestJwt.Create(TestUsers.Murid, [Roles.Murid], signedByForeignKey: true);

        using var response = await Client.SendAsync(Get("/api/probe/authenticated", token), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Expired_token_is_401()
    {
        var token = TestJwt.Create(TestUsers.Murid, [Roles.Murid], expires: DateTime.UtcNow.AddMinutes(-2));

        using var response = await Client.SendAsync(Get("/api/probe/authenticated", token), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Token_for_another_audience_is_401()
    {
        var token = TestJwt.Create(TestUsers.Murid, [Roles.Murid], audience: "account");

        using var response = await Client.SendAsync(Get("/api/probe/authenticated", token), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Token_without_an_application_role_is_403_forbidden()
    {
        var token = TestJwt.Create(TestUsers.Murid, []);

        using var response = await Client.SendAsync(Get("/api/probe/authenticated", token), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    // Layer 1 of docs/01 §5: every policy against every role.
    [Theory]
    [InlineData("/api/probe/authenticated", Roles.Murid, HttpStatusCode.OK)]
    [InlineData("/api/probe/authenticated", Roles.Sensei, HttpStatusCode.OK)]
    [InlineData("/api/probe/authenticated", Roles.Admin, HttpStatusCode.OK)]
    [InlineData("/api/probe/murid", Roles.Murid, HttpStatusCode.OK)]
    [InlineData("/api/probe/murid", Roles.Sensei, HttpStatusCode.Forbidden)]
    [InlineData("/api/probe/murid", Roles.Admin, HttpStatusCode.Forbidden)]
    [InlineData("/api/probe/staff", Roles.Murid, HttpStatusCode.Forbidden)]
    [InlineData("/api/probe/staff", Roles.Sensei, HttpStatusCode.OK)]
    [InlineData("/api/probe/staff", Roles.Admin, HttpStatusCode.OK)]
    [InlineData("/api/probe/admin", Roles.Murid, HttpStatusCode.Forbidden)]
    [InlineData("/api/probe/admin", Roles.Sensei, HttpStatusCode.Forbidden)]
    [InlineData("/api/probe/admin", Roles.Admin, HttpStatusCode.OK)]
    public async Task Role_policies_follow_the_access_matrix(string path, string role, HttpStatusCode expected)
    {
        using var response = await Client.SendAsync(Get(path, TestUsers.TokenFor(role)), Cancel);

        if (expected == HttpStatusCode.Forbidden)
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }
        else
        {
            response.StatusCode.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task BR_AKUN_05_deactivated_account_is_403_account_inactive()
    {
        var token = TestJwt.For(TestUsers.InactiveMurid, Roles.Murid);

        using var response = await Client.SendAsync(Get("/api/probe/murid", token), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "account_inactive");
    }

    [Fact]
    public async Task Anonymous_endpoints_do_not_check_the_account()
    {
        var token = TestJwt.For(TestUsers.InactiveMurid, Roles.Murid);

        using var response = await Client.SendAsync(Get("/anonymous", token), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Query_token_is_accepted_only_on_hubs()
    {
        var token = TestUsers.TokenFor(Roles.Murid);

        using var hub = await Client.SendAsync(Get($"/hubs/probe?access_token={token}"), Cancel);
        using var api = await Client.SendAsync(Get($"/api/probe/authenticated?access_token={token}"), Cancel);

        hub.StatusCode.ShouldBe(HttpStatusCode.OK);
        await api.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Current_user_is_the_token_subject()
    {
        using var response = await Client.SendAsync(Get("/api/probe/me", TestUsers.TokenFor(Roles.Admin)), Cancel);

        var body = await response.ReadJsonAsync();
        body.GetProperty("id").GetGuid().ShouldBe(TestUsers.Admin);
        body.GetProperty("isAdmin").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_route_needs_a_token_then_is_404_not_found()
    {
        using var anonymous = await Client.SendAsync(Get("/api/probe/nothing-here"), Cancel);
        using var signedIn = await Client.SendAsync(Get("/api/probe/nothing-here", TestUsers.TokenFor(Roles.Murid)), Cancel);

        await anonymous.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
        await signedIn.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Unhandled_exception_is_500_without_details()
    {
        using var response = await Client.SendAsync(Get("/api/probe/boom", TestUsers.TokenFor(Roles.Admin)), Cancel);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "internal_error");
        var raw = problem.GetRawText();
        raw.ShouldNotContain("secret detail");
        raw.ShouldNotContain("InvalidOperationException");
    }

    [Fact]
    public async Task Oversized_request_is_413_payload_too_large()
    {
        using var response = await Client.SendAsync(Get("/api/probe/too-large", TestUsers.TokenFor(Roles.Admin)), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "payload_too_large");
    }

    [Fact]
    public async Task Wrong_method_is_405_method_not_allowed()
    {
        using var response = await Client.SendAsync(Get("/api/probe/put-only", TestUsers.TokenFor(Roles.Admin)), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.MethodNotAllowed, "method_not_allowed");
    }

    [Fact]
    public async Task Validation_errors_are_keyed_by_json_field()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/probe/validated")
        {
            Content = JsonContent.Create(new { displayName = "", count = 0 }),
        };
        request.Headers.Authorization = new("Bearer", TestUsers.TokenFor(Roles.Sensei));

        using var response = await Client.SendAsync(request, Cancel);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("displayName", out _).ShouldBeTrue();
        errors.TryGetProperty("count", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Malformed_json_is_400_validation_failed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/probe/validated")
        {
            Content = new StringContent("{ not json", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", TestUsers.TokenFor(Roles.Sensei));

        using var response = await Client.SendAsync(request, Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/probe/validated")
        {
            Content = JsonContent.Create(new { displayName = "Kelas Demo", count = 3 }),
        };
        request.Headers.Authorization = new("Bearer", TestUsers.TokenFor(Roles.Sensei));

        using var response = await Client.SendAsync(request, Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("displayName").GetString().ShouldBe("Kelas Demo");
    }

    [Fact]
    public async Task Use_case_errors_become_problems_with_their_code()
    {
        var token = TestUsers.TokenFor(Roles.Admin);

        using var notFound = await Client.SendAsync(Get("/api/probe/not-found", token), Cancel);
        using var conflict = await Client.SendAsync(Get("/api/probe/conflict", token), Cancel);

        await notFound.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        var problem = await conflict.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
        problem.GetProperty("detail").GetString().ShouldBe("Username is already taken.");
    }

    [Fact]
    public async Task Json_uses_snake_case_enums_utc_times_and_readable_japanese()
    {
        using var response = await Client.SendAsync(Get("/api/probe/formats", TestUsers.TokenFor(Roles.Murid)), Cancel);

        var body = await response.Content.ReadAsStringAsync(Cancel);
        body.ShouldBe("""{"scriptPref":"kanji_furigana_romaji","at":"2026-10-18T12:30:00Z","japanese":"練習"}""");
    }

    [Fact]
    public async Task Module_openapi_document_lists_only_its_own_endpoints()
    {
        using var response = await Client.SendAsync(Get("/openapi/probe.json"), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync(Cancel);
        document.ShouldContain("/api/probe/admin");
        document.ShouldNotContain("/health/live");
        document.ShouldNotContain("/hubs/probe");
    }

    [Fact]
    public async Task Scalar_ui_is_served_in_development()
    {
        using var response = await Client.SendAsync(Get("/scalar/"), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task Account_status_is_checked_for_signed_in_requests()
    {
        var before = fixture.AccountStatus.Calls;

        using var response = await Client.SendAsync(Get("/api/probe/authenticated", TestUsers.TokenFor(Roles.Sensei)), Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        fixture.AccountStatus.Calls.ShouldBeGreaterThan(before);
    }
}
