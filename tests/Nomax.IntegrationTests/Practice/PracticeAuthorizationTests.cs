using System.Net;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Practice;

/// <summary>
/// Authorization matrix of the Practice endpoints (docs/09 §1, docs/05 §7): no token → 401, wrong role → 403, data of
/// someone else → 404 (docs/01 §6).
/// </summary>
public sealed class PracticeAuthorizationTests(NomaxApiFixture api)
{
    private const string Unknown = "00000000-0000-4000-8000-0000000000ff";
    private const string Demo = "c0000000-0000-4000-8000-000000000001";

    public static TheoryData<string, string> StaffEndpoints => new()
    {
        { "GET", "/api/practice/questions" },
        { "POST", "/api/practice/questions" },
        { "GET", $"/api/practice/questions/{Unknown}" },
        { "GET", $"/api/practice/questions/{Unknown}/student-view" },
        { "PATCH", $"/api/practice/questions/{Unknown}" },
        { "POST", $"/api/practice/questions/{Unknown}/submit-review" },
        { "POST", $"/api/practice/questions/{Unknown}/publish" },
        { "POST", $"/api/practice/questions/{Unknown}/archive" },
        { "POST", $"/api/practice/questions/{Unknown}/duplicate" },
        { "POST", $"/api/practice/questions/{Unknown}/preview-answer" },
        { "POST", "/api/practice/preview/drill" },
        { "GET", $"/api/practice/classes/{Demo}/progress" },
        { "GET", $"/api/practice/students/{Unknown}/weaknesses" },
        { "GET", $"/api/practice/students/{Unknown}/drill-sessions" },
        { "POST", "/api/practice/comments" },
    };

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/practice/sources" },
        { "POST", "/api/practice/sources" },
        { "PATCH", $"/api/practice/sources/{Unknown}" },
        { "POST", "/api/practice/generator/runs" },
        { "POST", "/api/practice/questions/publish" },
    };

    public static TheoryData<string, string> MuridEndpoints => new()
    {
        { "POST", "/api/practice/drill-sessions" },
        { "GET", $"/api/practice/drill-sessions/{Unknown}/next" },
        { "POST", $"/api/practice/drill-sessions/{Unknown}/answers" },
        { "POST", $"/api/practice/drill-sessions/{Unknown}/end" },
        { "GET", "/api/practice/me/drill-sessions" },
        { "GET", "/api/practice/me/mastery" },
        { "GET", "/api/practice/me/weaknesses" },
    };

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    [MemberData(nameof(AdminEndpoints))]
    [MemberData(nameof(MuridEndpoints))]
    [InlineData("GET", "/api/practice/comments")]
    public async Task Without_a_token_every_endpoint_is_401(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, token: null, Body(method));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Murid_gets_403_on_staff_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Murid), Body(method));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Sensei_gets_403_on_admin_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Sensei), Body(method));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Theory]
    [MemberData(nameof(MuridEndpoints))]
    public async Task Staff_get_403_on_student_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        foreach (var role in new[] { Roles.Admin, Roles.Sensei })
        {
            using var response = await client.SendAsync(method, path, TestUsers.TokenFor(role), Body(method));
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }
    }

    [Theory]
    [InlineData("GET", "/next")]
    [InlineData("POST", "/answers")]
    [InlineData("POST", "/end")]
    public async Task Another_students_session_is_404(string method, string suffix)
    {
        using var client = api.CreateClient();
        var started = await client.JsonAsync("POST", "/api/practice/drill-sessions", TestUsers.TokenFor(Roles.Murid), new { category = "hiragana" });
        var other = TestJwt.For(Guid.Parse("a0000000-0000-4000-8000-000000000004"), Roles.Murid);
        var body = method == "POST" ? new { answerId = Guid.CreateVersion7(), questionId = Unknown, answer = new { selected = new[] { 0 } } } : null;

        using var response = await client.SendAsync(method, $"/api/practice/drill-sessions/{started.GetProperty("sessionId").GetGuid()}{suffix}", other, body);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Sensei_gets_404_for_progress_of_a_class_or_student_they_do_not_teach()
    {
        using var client = api.CreateClient();
        var other = await client.JsonAsync("POST", "/api/classroom/classes", TestUsers.TokenFor(Roles.Admin), new
        {
            name = $"Kelas Lain {Guid.NewGuid():N}"[..20],
            levelId = "b0000000-0000-4000-8000-000000000001",
            bookId = "b1000000-0000-4000-8000-000000000001",
        });
        var sensei = TestUsers.TokenFor(Roles.Sensei);

        (await client.StatusAsync("GET", $"/api/practice/classes/{other.GetProperty("id").GetGuid()}/progress", sensei)).ShouldBe(404);
        (await client.StatusAsync("GET", $"/api/practice/students/{Unknown}/weaknesses", sensei)).ShouldBe(404);
        (await client.StatusAsync("GET", $"/api/practice/students/{Unknown}/drill-sessions", sensei)).ShouldBe(404);
        (await client.StatusAsync("GET", $"/api/practice/classes/{Demo}/progress", sensei)).ShouldBe(200);
        (await client.StatusAsync("GET", $"/api/practice/students/{TestUsers.Murid}/weaknesses", sensei)).ShouldBe(200);
    }

    private static object? Body(string method) =>
        method is "POST" or "PATCH"
            ? new
            {
                version = 0,
                name = "Matrix",
                license = "internal",
                levelId = Unknown,
                templates = new[] { "kana_to_romaji" },
                questionIds = new[] { Unknown },
                category = "hiragana",
                studentId = Unknown,
                targetType = "progress",
                targetId = Unknown,
                text = "x",
                answer = new { selected = new[] { 0 } },
            }
            : null;
}
