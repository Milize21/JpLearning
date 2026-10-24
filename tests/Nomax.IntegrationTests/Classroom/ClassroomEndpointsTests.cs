using System.Text.Json;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using Npgsql;

namespace Nomax.IntegrationTests.Classroom;

/// <summary>
/// Classroom behaviour against PostgreSQL: BR-AKSES-01 through the real endpoints, members and teachers (BR-AKSES-03),
/// sessions with notifications (BR-JADWAL-05/06), the ICS feed (BR-JADWAL-03/04) and the UserDeactivated consumer.
/// Every test builds its own level, book, chapters, classes and students, so tests run in parallel.
/// </summary>
public sealed class ClassroomEndpointsTests(NomaxApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Admin => TestUsers.TokenFor(Roles.Admin);

    private static string Sensei => TestUsers.TokenFor(Roles.Sensei);

    [Fact]
    public async Task BR_AKSES_01_all_six_cases_through_the_api()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var (studentId, student) = await NewStudentAsync(client, "akses");
        var chapter = world.Chapters[0];
        var path = $"/api/content/chapters/{chapter}";

        var k1 = await NewClassAsync(client, world, "K1");
        var k2 = await NewClassAsync(client, world, "K2");
        await client.JsonAsync("POST", $"/api/classroom/classes/{k1}/students", Admin, new { studentIds = new[] { studentId } });
        await client.JsonAsync("PUT", $"/api/classroom/classes/{k2}/unlocks/{chapter}", Admin);

        // Case 2: K2 unlocked C but the student is only in K1.
        (await client.StatusAsync("GET", path, student)).ShouldBe(403);

        // Case 3 (and 1): joining K2 gives access, the union over classes.
        await client.JsonAsync("POST", $"/api/classroom/classes/{k2}/students", Admin, new { studentIds = new[] { studentId } });
        (await client.StatusAsync("GET", path, student)).ShouldBe(200);

        // Case 4: removed from K2.
        await client.JsonAsync("DELETE", $"/api/classroom/classes/{k2}/students/{studentId}", Admin);
        (await client.StatusAsync("GET", path, student)).ShouldBe(403);

        // Case 5: archived K2 still gives read access.
        await client.JsonAsync("POST", $"/api/classroom/classes/{k2}/students", Admin, new { studentIds = new[] { studentId } });
        await client.JsonAsync("POST", $"/api/classroom/classes/{k2}/archive", Admin);
        (await client.StatusAsync("GET", path, student)).ShouldBe(200);

        // Case 6: a draft class gives nothing.
        var k3 = await NewClassAsync(client, world, "K3", status: "draft");
        await client.JsonAsync("PUT", $"/api/classroom/classes/{k3}/unlocks/{world.Chapters[1]}", Admin);
        await client.JsonAsync("POST", $"/api/classroom/classes/{k3}/students", Admin, new { studentIds = new[] { studentId } });
        (await client.StatusAsync("GET", $"/api/content/chapters/{world.Chapters[1]}", student)).ShouldBe(403);

        var book = await client.JsonAsync("GET", $"/api/content/books/{world.BookId}", student);
        book.GetProperty("chapters").EnumerateArray().Select(item => item.GetProperty("isUnlocked").GetBoolean()).ShouldBe([true, false]);
    }

    [Fact]
    public async Task BR_AKSES_01_unlock_is_idempotent_and_limited_to_the_class_book()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var other = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Unlock");

        (await client.StatusAsync("PUT", $"/api/classroom/classes/{classId}/unlocks/{world.Chapters[0]}", Admin)).ShouldBe(204);
        (await client.StatusAsync("PUT", $"/api/classroom/classes/{classId}/unlocks/{world.Chapters[0]}", Admin)).ShouldBe(204);
        (await client.StatusAsync("PUT", $"/api/classroom/classes/{classId}/unlocks/{other.Chapters[0]}", Admin)).ShouldBe(400);

        var unlocks = await client.JsonAsync("GET", $"/api/classroom/classes/{classId}/unlocks", Admin);
        unlocks.GetArrayLength().ShouldBe(1);
        (await CountAsync($"select count(*) from classroom.outbox_messages where type = 'ChapterUnlocked.v1' and payload->>'classId' = '{classId}'")).ShouldBe(1);
    }

    [Fact]
    public async Task BR_AKSES_02_closing_a_chapter_hides_its_materials_and_publishes_ChapterLocked()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var chapter = world.Chapters[0];
        var classId = await NewClassAsync(client, world, "Tutup");
        var (studentId, student) = await NewStudentAsync(client, "tutup");
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Admin, new { studentIds = new[] { studentId } });
        await client.JsonAsync("PUT", $"/api/classroom/classes/{classId}/unlocks/{chapter}", Admin);
        await client.JsonAsync("POST", $"/api/content/chapters/{chapter}/materials", Admin, new { kind = "link", title = new { id = "Tautan" }, url = "https://example.com/bab1" });
        (await client.JsonAsync("GET", $"/api/content/chapters/{chapter}/materials", student)).GetArrayLength().ShouldBe(1);

        (await client.StatusAsync("DELETE", $"/api/classroom/classes/{classId}/unlocks/{chapter}", Admin)).ShouldBe(204);

        (await client.StatusAsync("GET", $"/api/content/chapters/{chapter}", student)).ShouldBe(403);
        (await client.StatusAsync("GET", $"/api/content/chapters/{chapter}/materials", student)).ShouldBe(403);
        (await client.JsonAsync("GET", $"/api/content/chapters/{chapter}/materials", Admin)).GetArrayLength().ShouldBe(1);
        (await CountAsync($"select count(*) from classroom.outbox_messages where type = 'ChapterLocked.v1' and payload->>'classId' = '{classId}'")).ShouldBe(1);
        (await client.JsonAsync("GET", $"/api/classroom/classes/{classId}/unlocks", Admin)).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task BR_JADWAL_02_meeting_links_reach_only_active_members_and_teachers()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Link");
        var (memberId, member) = await NewStudentAsync(client, "link");
        var (_, outsider) = await NewStudentAsync(client, "luar");
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Admin, new { studentIds = new[] { memberId } });
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var session = await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/sessions", Admin, new { topic = "Link", startsAt = start, endsAt = start.AddHours(1), platform = "zoom", meetingUrl = "https://zoom.us/j/42" });
        var sessionId = session.GetProperty("id").GetGuid();
        var window = $"from={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}&to={Uri.EscapeDataString(start.AddDays(1).ToString("O"))}";

        var forMember = await client.JsonAsync("GET", $"/api/classroom/classes/{classId}/sessions?{window}", member);
        forMember.ShouldHaveSingleItemWhere(item => item.GetProperty("meetingUrl").GetString() == "https://zoom.us/j/42");
        (await client.StatusAsync("GET", $"/api/classroom/classes/{classId}/sessions?{window}", outsider)).ShouldBe(404);
        var outsiderSchedule = await client.JsonAsync("GET", $"/api/classroom/me/schedule?{window}", outsider);
        outsiderSchedule.EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == sessionId).ShouldBeFalse();

        // Leaving the class ends access to the link as well.
        await client.JsonAsync("DELETE", $"/api/classroom/classes/{classId}/students/{memberId}", Admin);
        (await client.StatusAsync("GET", $"/api/classroom/classes/{classId}/sessions?{window}", member)).ShouldBe(404);
        var memberSchedule = await client.JsonAsync("GET", $"/api/classroom/me/schedule?{window}", member);
        memberSchedule.EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == sessionId).ShouldBeFalse();
    }

    [Fact]
    public async Task Students_are_added_by_username_and_unknown_names_are_reported()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Usernames", token: Sensei);
        var (_, _, username) = await NewStudentWithNameAsync(client, "byname");

        var result = await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Sensei,
            new { usernames = new[] { username.ToUpperInvariant(), "tidak.ada", "sensei.dev" } });

        result.GetProperty("added").EnumerateArray().Select(item => item.GetProperty("username").GetString()).ShouldBe([username]);
        result.GetProperty("notFound").EnumerateArray().Select(item => item.GetString()).ShouldBe(["tidak.ada", "sensei.dev"]);
        var students = await client.JsonAsync("GET", $"/api/classroom/classes/{classId}/students", Sensei);
        students.GetArrayLength().ShouldBe(1);
        students[0].TryGetProperty("email", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task BR_AKSES_03_creator_is_primary_and_the_last_teacher_stays()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Teachers", token: Sensei);

        var teachers = await client.JsonAsync("GET", $"/api/classroom/classes/{classId}/teachers", Sensei);
        teachers.ShouldHaveSingleItemWhere(teacher => teacher.GetProperty("isPrimary").GetBoolean());

        (await client.StatusAsync("DELETE", $"/api/classroom/classes/{classId}/teachers/{TestUsers.Sensei}", Sensei)).ShouldBe(409);

        // Admin becomes the new primary teacher; the sensei can then be removed and loses access.
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/teachers", Sensei, new { teacherId = TestUsers.Admin, isPrimary = true });
        (await client.StatusAsync("DELETE", $"/api/classroom/classes/{classId}/teachers/{TestUsers.Sensei}", Admin)).ShouldBe(204);
        (await client.StatusAsync("GET", $"/api/classroom/classes/{classId}/students", Sensei)).ShouldBe(404);
    }

    [Fact]
    public async Task BR_JADWAL_05_06_session_lifecycle_plans_and_cancels_notifications()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Jadwal");
        var (studentId, _) = await NewStudentAsync(client, "jadwal", email: true);
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Admin, new { studentIds = new[] { studentId } });
        var start = DateTimeOffset.UtcNow.AddDays(3);

        var session = await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/sessions", Admin, new
        {
            topic = "Baris a",
            startsAt = start,
            endsAt = start.AddHours(1),
            platform = "zoom",
            meetingUrl = "https://zoom.us/j/1",
        });
        var sessionId = session.GetProperty("id").GetGuid();

        // One student with email: created + 2 reminders, on email and web push.
        (await CountAsync($"select count(*) from classroom.notifications where payload->>'sessionId' = '{sessionId}'")).ShouldBe(6);

        var updated = await client.JsonAsync("PATCH", $"/api/classroom/sessions/{sessionId}", Admin, new { startsAt = start.AddDays(1), endsAt = start.AddDays(1).AddHours(1) });
        updated.GetProperty("sequence").GetInt32().ShouldBe(1);
        (await CountAsync($"select count(*) from classroom.notifications where payload->>'sessionId' = '{sessionId}' and type like 'session_reminder%' and status = 'cancelled'")).ShouldBe(4);
        (await CountAsync($"select count(*) from classroom.notifications where payload->>'sessionId' = '{sessionId}' and type = 'session_updated'")).ShouldBe(2);

        (await client.StatusAsync("POST", $"/api/classroom/sessions/{sessionId}/cancel", Admin)).ShouldBe(204);
        (await CountAsync($"select count(*) from classroom.notifications where payload->>'sessionId' = '{sessionId}' and type like 'session_reminder%' and status = 'pending'")).ShouldBe(0);
        (await CountAsync($"select count(*) from classroom.notifications where payload->>'sessionId' = '{sessionId}' and type = 'session_cancelled'")).ShouldBe(2);
        (await client.StatusAsync("PATCH", $"/api/classroom/sessions/{sessionId}", Admin, new { topic = "x" })).ShouldBe(409);
    }

    [Fact]
    public async Task BR_JADWAL_01_invalid_sessions_are_refused()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Invalid");
        var start = DateTimeOffset.UtcNow.AddDays(1);

        (await client.StatusAsync("POST", $"/api/classroom/classes/{classId}/sessions", Admin, new { topic = "x", startsAt = start, endsAt = start.AddHours(9), platform = "zoom" })).ShouldBe(400);
        (await client.StatusAsync("POST", $"/api/classroom/classes/{classId}/sessions", Admin, new { topic = "x", startsAt = start, endsAt = start.AddHours(1), platform = "zoom", meetingUrl = "http://zoom.us" })).ShouldBe(400);

        await client.JsonAsync("PATCH", $"/api/classroom/classes/{classId}", Admin, new { status = "draft" });
        (await client.StatusAsync("POST", $"/api/classroom/classes/{classId}/sessions", Admin, new { topic = "x", startsAt = start, endsAt = start.AddHours(1), platform = "zoom" })).ShouldBe(409);
    }

    [Fact]
    public async Task BR_JADWAL_03_04_ics_feed_shows_member_sessions_and_rotation_revokes_the_old_url()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Ics");
        var (studentId, student) = await NewStudentAsync(client, "ics");
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Admin, new { studentIds = new[] { studentId } });
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var session = await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/sessions", Admin, new { topic = "Baris ka", startsAt = start, endsAt = start.AddHours(1), platform = "discord" });

        var token = await client.JsonAsync("POST", "/api/classroom/me/calendar-token", student);
        var path = new Uri(token.GetProperty("url").GetString()!).AbsolutePath;
        using var feed = await client.SendAsync("GET", path, token: null);
        var ics = await feed.Content.ReadAsStringAsync(Cancel);

        feed.Content.Headers.ContentType!.MediaType.ShouldBe("text/calendar");
        feed.Headers.CacheControl!.Private.ShouldBeTrue();
        feed.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromMinutes(5));
        ics.ShouldContain($"UID:{session.GetProperty("id").GetGuid()}@nomax");
        ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal).ShouldContain("SUMMARY:[Ics");

        await client.JsonAsync("POST", "/api/classroom/me/calendar-token", student);
        (await client.StatusAsync("GET", path, token: null)).ShouldBe(404);
        (await client.JsonAsync("GET", "/api/classroom/me/calendar-token", student)).GetProperty("active").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task BR_AKUN_05_deactivation_cancels_pending_notifications_through_the_outbox()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var classId = await NewClassAsync(client, world, "Outbox");
        var (studentId, _) = await NewStudentAsync(client, "outbox");
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Admin, new { studentIds = new[] { studentId } });
        var start = DateTimeOffset.UtcNow.AddDays(5);
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/sessions", Admin, new { topic = "Outbox", startsAt = start, endsAt = start.AddHours(1), platform = "zoom" });
        var pending = $"select count(*) from classroom.notifications where user_id = '{studentId}' and status = 'pending' and type like 'session_reminder%'";
        (await CountAsync(pending)).ShouldBe(2);

        (await client.StatusAsync("POST", $"/api/identity/users/{studentId}/deactivate", Admin)).ShouldBe(204);

        // Identity outbox → in-process dispatch → Classroom inbox consumer (polled every second).
        for (var i = 0; i < 40 && await CountAsync(pending) > 0; i++)
        {
            await Task.Delay(250, Cancel);
        }

        (await CountAsync(pending)).ShouldBe(0);
        (await CountAsync("select count(*) from classroom.inbox_messages where handler like '%UserDeactivatedConsumer'")).ShouldBeGreaterThanOrEqualTo(1);
    }

    private sealed record World(Guid LevelId, Guid BookId, IReadOnlyList<Guid> Chapters);

    private static async Task<World> NewWorldAsync(HttpClient client)
    {
        var code = $"t{Guid.NewGuid():N}"[..12];
        var level = await client.JsonAsync("POST", "/api/content/levels", Admin, new { code, name = new { id = $"Level {code}" }, sortOrder = 50 });
        var levelId = level.GetProperty("id").GetGuid();
        var book = await client.JsonAsync("POST", "/api/content/books", Admin, new { levelId, title = new { id = "Buku uji" }, sortOrder = 0 });
        var bookId = book.GetProperty("id").GetGuid();
        var chapters = new List<Guid>();
        foreach (var number in new[] { 1, 2 })
        {
            var chapter = await client.JsonAsync("POST", "/api/content/chapters", Admin, new { bookId, number, title = new { id = $"Bab {number}" }, sortOrder = number });
            chapters.Add(chapter.GetProperty("id").GetGuid());
        }

        return new World(levelId, bookId, chapters);
    }

    private static async Task<Guid> NewClassAsync(HttpClient client, World world, string name, string? status = null, string? token = null)
    {
        var created = await client.JsonAsync("POST", "/api/classroom/classes", token ?? Admin, new
        {
            name = $"{name} {Guid.NewGuid():N}"[..Math.Min(name.Length + 9, 100)],
            levelId = world.LevelId,
            bookId = world.BookId,
            status = status ?? "active",
        });
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string Token)> NewStudentAsync(HttpClient client, string name, bool email = false)
    {
        var (id, token, _) = await NewStudentWithNameAsync(client, name, email);
        return (id, token);
    }

    private static async Task<(Guid Id, string Token, string Username)> NewStudentWithNameAsync(HttpClient client, string name, bool email = false)
    {
        var username = $"{name}.{Guid.NewGuid():N}"[..(name.Length + 9)];
        var created = await client.JsonAsync("POST", "/api/identity/users", Admin, new
        {
            username,
            displayName = $"Murid {name}",
            role = "murid",
            email = email ? $"{username}@nomax.test" : null,
        });
        var id = created.GetProperty("user").GetProperty("id").GetGuid();
        return (id, TestJwt.For(id, Roles.Murid), username);
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(api.PostgresConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Cancel))!;
    }
}

internal static class JsonAssertions
{
    public static void ShouldHaveSingleItemWhere(this JsonElement array, Func<JsonElement, bool> predicate) =>
        array.EnumerateArray().Count(predicate).ShouldBe(1);
}
