using System.Net;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Classroom;

/// <summary>
/// Authorization matrix of the Classroom endpoints (docs/09 §1, docs/05 §6): no token → 401, murid on staff endpoints → 403,
/// a class the caller does not teach or belong to → 404 (not an owner, docs/01 §6).
/// </summary>
public sealed class ClassroomAuthorizationTests(NomaxApiFixture api)
{
    private const string Unknown = "00000000-0000-4000-8000-0000000000ff";
    private const string Demo = "c0000000-0000-4000-8000-000000000001";
    private const string Chapter1 = "b2000000-0000-4000-8000-000000000001";

    public static TheoryData<string, string> StaffEndpoints => new()
    {
        { "POST", "/api/classroom/classes" },
        { "PATCH", $"/api/classroom/classes/{Demo}" },
        { "POST", $"/api/classroom/classes/{Demo}/archive" },
        { "GET", $"/api/classroom/classes/{Demo}/students" },
        { "POST", $"/api/classroom/classes/{Demo}/students" },
        { "DELETE", $"/api/classroom/classes/{Demo}/students/{Unknown}" },
        { "GET", $"/api/classroom/classes/{Demo}/teachers" },
        { "POST", $"/api/classroom/classes/{Demo}/teachers" },
        { "DELETE", $"/api/classroom/classes/{Demo}/teachers/{Unknown}" },
        { "PUT", $"/api/classroom/classes/{Demo}/unlocks/{Chapter1}" },
        { "DELETE", $"/api/classroom/classes/{Demo}/unlocks/{Chapter1}" },
        { "POST", $"/api/classroom/classes/{Demo}/sessions" },
        { "PATCH", $"/api/classroom/sessions/{Unknown}" },
        { "POST", $"/api/classroom/sessions/{Unknown}/cancel" },
    };

    public static TheoryData<string, string> SignedInEndpoints => new()
    {
        { "GET", "/api/classroom/classes" },
        { "GET", $"/api/classroom/classes/{Demo}" },
        { "GET", $"/api/classroom/classes/{Demo}/unlocks" },
        { "GET", $"/api/classroom/classes/{Demo}/sessions" },
        { "GET", "/api/classroom/me/schedule" },
        { "GET", "/api/classroom/me/calendar-token" },
        { "POST", "/api/classroom/me/calendar-token" },
        { "DELETE", "/api/classroom/me/calendar-token" },
        { "GET", "/api/classroom/me/push-subscriptions" },
        { "POST", "/api/classroom/me/push-subscriptions" },
        { "DELETE", $"/api/classroom/me/push-subscriptions/{Unknown}" },
        { "GET", "/api/classroom/push/public-key" },
    };

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    [MemberData(nameof(SignedInEndpoints))]
    public async Task Without_a_token_every_endpoint_is_401(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, token: null, Body(method));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    public async Task Murid_gets_403_on_staff_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Murid), Body(method));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Theory]
    [MemberData(nameof(SignedInEndpoints))]
    public async Task Every_role_reaches_its_own_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        foreach (var role in new[] { Roles.Admin, Roles.Sensei, Roles.Murid })
        {
            using var response = await client.SendAsync(method, path, TestUsers.TokenFor(role), Body(method, path));
            ((int)response.StatusCode).ShouldBeOneOf([200, 204, 404], $"{method} {path} as {role}");
        }
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PATCH", "")]
    [InlineData("GET", "/students")]
    [InlineData("GET", "/unlocks")]
    [InlineData("GET", "/sessions")]
    [InlineData("PUT", $"/unlocks/{Chapter1}")]
    public async Task Sensei_gets_404_for_a_class_they_do_not_teach(string method, string suffix)
    {
        using var client = api.CreateClient();
        var other = await client.JsonAsync("POST", "/api/classroom/classes", TestUsers.TokenFor(Roles.Admin), new
        {
            name = $"Kelas Lain {Guid.NewGuid():N}"[..20],
            levelId = "b0000000-0000-4000-8000-000000000001",
            bookId = "b1000000-0000-4000-8000-000000000001",
        });
        var path = $"/api/classroom/classes/{other.GetProperty("id").GetGuid()}{suffix}";

        using var asSensei = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Sensei), Body(method));
        using var asMurid = await client.SendAsync("GET", $"/api/classroom/classes/{other.GetProperty("id").GetGuid()}", TestUsers.TokenFor(Roles.Murid));

        await asSensei.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        await asMurid.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task BR_JADWAL_04_unknown_ics_token_is_404()
    {
        using var client = api.CreateClient();

        (await client.StatusAsync("GET", "/ical/not-a-real-token.ics", token: null)).ShouldBe(404);
    }

    private static object? Body(string method, string? path = null) =>
        path?.EndsWith("push-subscriptions", StringComparison.Ordinal) == true && method == "POST"
            ? new { endpoint = "https://fcm.googleapis.com/fcm/send/matrix", keys = new { p256dh = "BCVxsr7N", auth = "BTBZ" } }
            : method is "POST" or "PATCH"
                ? new { name = "Matrix", topic = "Matrix", levelId = Unknown, bookId = Unknown, studentIds = new[] { Unknown }, teacherId = Unknown }
                : null;
}
