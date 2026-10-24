using System.Text.Json;
using Nomax.IntegrationTests.Classroom;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using Npgsql;

namespace Nomax.IntegrationTests.Practice;

/// <summary>
/// Practice behaviour against PostgreSQL: question bank (BR-SOAL-02/03/04), generator (BR-SOAL-05), drilling with server-side
/// evaluation and idempotent answers (BR-DRILL-02/05/07/08), mastery (BR-MASTERY-01) and progress visibility.
/// Each drilling test uses its own student, class and chapters, so tests run in parallel.
/// </summary>
public sealed class PracticeEndpointsTests(NomaxApiFixture api)
{
    private static readonly int[] One = [1];
    private static readonly int[] Five = [5];
    private static readonly string[] KanaTemplates = ["kana_to_romaji", "romaji_to_kana"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Admin => TestUsers.TokenFor(Roles.Admin);

    private static string Sensei => TestUsers.TokenFor(Roles.Sensei);

    [Fact]
    public async Task BR_SOAL_02_03_private_questions_flow_and_visibility()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var question = await CreateMcqAsync(client, Sensei, world.LevelId, world.Chapters[0], "Pilih あ");
        var id = question.GetProperty("id").GetGuid();
        question.GetProperty("status").GetString().ShouldBe("draft");
        question.GetProperty("scope").GetString().ShouldBe("private");

        // Another sensei neither sees nor finds it; the admin does.
        var other = await OtherSenseiAsync(client);
        (await client.StatusAsync("GET", $"/api/practice/questions/{id}", other)).ShouldBe(404);
        var otherList = await client.JsonAsync("GET", $"/api/practice/questions?levelId={world.LevelId}", other);
        otherList.GetProperty("totalCount").GetInt32().ShouldBe(0);
        (await client.StatusAsync("GET", $"/api/practice/questions/{id}", Admin)).ShouldBe(200);

        // Owner: draft → review → published; search by prompt text.
        (await client.JsonAsync("POST", $"/api/practice/questions/{id}/submit-review", Sensei)).GetProperty("status").GetString().ShouldBe("review");
        (await client.JsonAsync("POST", $"/api/practice/questions/{id}/publish", Sensei)).GetProperty("status").GetString().ShouldBe("published");
        (await client.JsonAsync("GET", "/api/practice/questions?q=pilih%20%E3%81%82", Sensei)).GetProperty("items").EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == id).ShouldBeTrue();

        // A sensei cannot create builtin questions.
        (await client.StatusAsync("POST", "/api/practice/questions", Sensei, McqBody(world.LevelId, world.Chapters[0], "x", scope: "builtin"))).ShouldBe(403);
    }

    [Fact]
    public async Task Invalid_payloads_and_chapters_are_rejected_with_field_errors()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var other = await NewWorldAsync(client);

        using var badPayload = await client.SendAsync("POST", "/api/practice/questions", Sensei, new
        {
            kind = "mcq",
            levelId = world.LevelId,
            chapterId = world.Chapters[0],
            category = "hiragana",
            prompt = new { id = "x" },
            payload = new { choices = new[] { new { id = "a" } }, correct = Five, multi = false },
        });
        var problem = await badPayload.ShouldBeProblemAsync(System.Net.HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("errors").TryGetProperty("payload.choices", out _).ShouldBeTrue();

        (await client.StatusAsync("POST", "/api/practice/questions", Sensei, McqBody(world.LevelId, other.Chapters[0], "x"))).ShouldBe(400);
    }

    [Fact]
    public async Task BR_SOAL_04_answered_questions_keep_their_content_and_duplicate_archives_the_original()
    {
        using var client = api.CreateClient();
        var setup = await NewDrillSetupAsync(client);
        var start = await client.JsonAsync("POST", "/api/practice/drill-sessions", setup.Student, new { category = "hiragana" });
        var sessionId = start.GetProperty("sessionId").GetGuid();
        await AnswerAsync(client, setup.Student, sessionId, setup.QuestionId, Guid.CreateVersion7(), 0);

        var question = await client.JsonAsync("GET", $"/api/practice/questions/{setup.QuestionId}", Sensei);
        question.GetProperty("hasAnswers").GetBoolean().ShouldBeTrue();
        var version = question.GetProperty("version").GetUInt32();

        (await client.StatusAsync("PATCH", $"/api/practice/questions/{setup.QuestionId}", Sensei, new
        {
            version,
            payload = new { choices = new[] { new { id = "x" }, new { id = "y" } }, correct = One, multi = false },
        })).ShouldBe(409);
        var fixedPrompt = await client.JsonAsync("PATCH", $"/api/practice/questions/{setup.QuestionId}", Sensei, new { version, prompt = new { id = "Pilih romaji yang benar" } });
        fixedPrompt.GetProperty("prompt").GetProperty("id").GetString().ShouldBe("Pilih romaji yang benar");

        var copy = await client.JsonAsync("POST", $"/api/practice/questions/{setup.QuestionId}/duplicate", Sensei);
        copy.GetProperty("status").GetString().ShouldBe("draft");
        (await client.JsonAsync("GET", $"/api/practice/questions/{setup.QuestionId}", Sensei)).GetProperty("status").GetString().ShouldBe("archived");
    }

    [Fact]
    public async Task BR_DRILL_full_session_hides_the_key_until_answered_and_updates_mastery()
    {
        using var client = api.CreateClient();
        var setup = await NewDrillSetupAsync(client);

        var start = await client.SendAsync("POST", "/api/practice/drill-sessions", setup.Student, new { category = "hiragana", chapterIds = new[] { setup.ChapterId } });
        var startText = await start.Content.ReadAsStringAsync(Cancel);
        ((int)start.StatusCode).ShouldBe(201, startText);
        // docs/03 §8.3: no key in what students receive before answering.
        startText.ShouldNotContain("\"correct\"");
        startText.ShouldNotContain("\"accepted\"");
        var started = JsonDocument.Parse(startText).RootElement;
        var sessionId = started.GetProperty("sessionId").GetGuid();
        started.GetProperty("poolSize").GetInt32().ShouldBe(1);
        var safe = started.GetProperty("batch")[0];
        safe.GetProperty("choices").GetArrayLength().ShouldBe(3);

        var answerId = Guid.CreateVersion7();
        var result = await AnswerAsync(client, setup.Student, sessionId, setup.QuestionId, answerId, 1);
        result.GetProperty("isCorrect").GetBoolean().ShouldBeTrue();
        result.GetProperty("correctAnswer").GetProperty("correct")[0].GetInt32().ShouldBe(1);
        result.GetProperty("mastery").GetProperty("after").GetDecimal().ShouldBe(0.3m);

        // BR-DRILL-07: resending the same answer id changes nothing.
        var replay = await AnswerAsync(client, setup.Student, sessionId, setup.QuestionId, answerId, 0);
        replay.GetProperty("isCorrect").GetBoolean().ShouldBeTrue();
        (await CountAsync($"select count(*) from practice.drill_answers where session_id = '{sessionId}'")).ShouldBe(1);
        (await CountAsync($"select attempts from practice.item_masteries where student_id = '{setup.StudentId}'")).ShouldBe(1);
        (await CountAsync($"select count(*) from practice.outbox_messages where type = 'AnswerSubmitted.v1' and payload->>'answerId' = '{answerId}'")).ShouldBe(1);

        // A wrong answer resets the streak: 0.3 + 0.3 × (0 − 0.3) = 0.21.
        var wrong = await AnswerAsync(client, setup.Student, sessionId, setup.QuestionId, Guid.CreateVersion7(), 0);
        wrong.GetProperty("isCorrect").GetBoolean().ShouldBeFalse();
        wrong.GetProperty("mastery").GetProperty("after").GetDecimal().ShouldBe(0.21m);

        var mastery = await client.JsonAsync("GET", "/api/practice/me/mastery", setup.Student);
        mastery.ShouldHaveSingleItemWhere(item => item.GetProperty("attempts").GetInt32() == 2 && item.GetProperty("streak").GetInt32() == 0);

        var summary = await client.JsonAsync("POST", $"/api/practice/drill-sessions/{sessionId}/end", setup.Student);
        summary.GetProperty("answerCount").GetInt32().ShouldBe(2);
        summary.GetProperty("correctCount").GetInt32().ShouldBe(1);
        (await CountAsync($"select count(*) from practice.outbox_messages where type = 'DrillSessionEnded.v1' and payload->>'sessionId' = '{sessionId}'")).ShouldBe(1);

        // BR-DRILL-08: an ended session takes no more answers.
        using var late = await client.SendAsync("POST", $"/api/practice/drill-sessions/{sessionId}/answers", setup.Student,
            new { answerId = Guid.CreateVersion7(), questionId = setup.QuestionId, answer = new { selected = One } });
        await late.ShouldBeProblemAsync(System.Net.HttpStatusCode.Conflict, "session_ended");

        var sessions = await client.JsonAsync("GET", "/api/practice/me/drill-sessions", setup.Student);
        sessions.GetProperty("items")[0].GetProperty("answerCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task BR_DRILL_02_pool_rules_locked_chapters_empty_pool_and_private_questions_of_other_teachers()
    {
        using var client = api.CreateClient();
        var setup = await NewDrillSetupAsync(client);

        // A chapter that is not unlocked is refused; a category without questions is an empty pool.
        using var locked = await client.SendAsync("POST", "/api/practice/drill-sessions", setup.Student, new { category = "hiragana", chapterIds = new[] { setup.LockedChapterId } });
        await locked.ShouldBeProblemAsync(System.Net.HttpStatusCode.Forbidden, "chapter_locked");
        using var empty = await client.SendAsync("POST", "/api/practice/drill-sessions", setup.Student, new { category = "kanji", chapterIds = new[] { setup.ChapterId } });
        await empty.ShouldBeProblemAsync(System.Net.HttpStatusCode.Conflict, "empty_pool");

        // A private question of a sensei who does not teach the student is not in the pool.
        var outsider = await OtherSenseiAsync(client);
        var foreign = await CreateMcqAsync(client, outsider, setup.LevelId, setup.ChapterId, "Soal sensei lain");
        await client.JsonAsync("POST", $"/api/practice/questions/{foreign.GetProperty("id").GetGuid()}/publish", outsider);
        var start = await client.JsonAsync("POST", "/api/practice/drill-sessions", setup.Student, new { category = "hiragana", chapterIds = new[] { setup.ChapterId } });
        start.GetProperty("poolSize").GetInt32().ShouldBe(1);

        // Closing the chapter takes its questions out at once (BR-AKSES-02).
        var sessionId = start.GetProperty("sessionId").GetGuid();
        await client.JsonAsync("DELETE", $"/api/classroom/classes/{setup.ClassId}/unlocks/{setup.ChapterId}", Admin);
        using var afterClose = await client.SendAsync("POST", $"/api/practice/drill-sessions/{sessionId}/answers", setup.Student,
            new { answerId = Guid.CreateVersion7(), questionId = setup.QuestionId, answer = new { selected = One } });
        await afterClose.ShouldBeProblemAsync(System.Net.HttpStatusCode.Forbidden, "chapter_locked");
    }

    [Fact]
    public async Task BR_DRILL_04_weak_focus_falls_back_to_normal_with_few_practised_items()
    {
        using var client = api.CreateClient();
        var setup = await NewDrillSetupAsync(client);

        var start = await client.JsonAsync("POST", "/api/practice/drill-sessions", setup.Student, new { category = "hiragana", mode = "weak_focus" });

        start.GetProperty("fallback").GetString().ShouldBe("normal");
    }

    [Fact]
    public async Task Progress_is_visible_to_teachers_of_the_student_and_comments_reach_the_student()
    {
        using var client = api.CreateClient();
        var setup = await NewDrillSetupAsync(client);
        var start = await client.JsonAsync("POST", "/api/practice/drill-sessions", setup.Student, new { category = "hiragana" });
        var sessionId = start.GetProperty("sessionId").GetGuid();
        await AnswerAsync(client, setup.Student, sessionId, setup.QuestionId, Guid.CreateVersion7(), 0);

        var progress = await client.JsonAsync("GET", $"/api/practice/classes/{setup.ClassId}/progress", Sensei);
        var row = progress.GetProperty("students").EnumerateArray().Single(item => item.GetProperty("studentId").GetGuid() == setup.StudentId);
        row.GetProperty("answersLast7Days").GetInt32().ShouldBe(1);
        row.GetProperty("accuracy").GetDecimal().ShouldBe(0m);

        (await client.JsonAsync("GET", $"/api/practice/students/{setup.StudentId}/weaknesses?classId={setup.ClassId}", Sensei))
            .GetProperty("weakestItems").GetArrayLength().ShouldBe(1);
        (await client.StatusAsync("GET", $"/api/practice/students/{setup.StudentId}/weaknesses", await OtherSenseiAsync(client))).ShouldBe(404);

        await client.JsonAsync("POST", "/api/practice/comments", Sensei, new { studentId = setup.StudentId, targetType = "drill_session", targetId = sessionId, text = "Bagus, lanjutkan!" });
        var forStudent = await client.JsonAsync("GET", $"/api/practice/comments?targetType=drill_session&targetId={sessionId}", setup.Student);
        forStudent.ShouldHaveSingleItemWhere(comment => comment.GetProperty("text").GetString() == "Bagus, lanjutkan!");
        var forOtherStudent = await client.JsonAsync("GET", $"/api/practice/comments?targetId={sessionId}", TestUsers.TokenFor(Roles.Murid));
        forOtherStudent.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task BR_SOAL_05_generator_creates_review_questions_once_and_admin_publishes_them()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        await ImportKanaAsync(client, world, "ア:a\nイ:i\nウ:u\nエ:e");

        var first = await client.JsonAsync("POST", "/api/practice/generator/runs", Admin, new { levelId = world.LevelId, templates = KanaTemplates });
        first.GetProperty("created").GetInt32().ShouldBe(8);
        var second = await client.JsonAsync("POST", "/api/practice/generator/runs", Admin, new { levelId = world.LevelId, templates = KanaTemplates });
        second.GetProperty("created").GetInt32().ShouldBe(0);
        second.GetProperty("skipped").GetInt32().ShouldBe(8);

        var ids = first.GetProperty("questionIds").EnumerateArray().Select(item => item.GetGuid()).ToList();
        var question = await client.JsonAsync("GET", $"/api/practice/questions/{ids[0]}", Admin);
        question.GetProperty("status").GetString().ShouldBe("review");
        question.GetProperty("scope").GetString().ShouldBe("builtin");
        question.GetProperty("category").GetString().ShouldBe("katakana");

        // Sensei may read builtin questions but not publish them (BR-SOAL-02).
        (await client.StatusAsync("POST", $"/api/practice/questions/{ids[0]}/publish", Sensei)).ShouldBe(403);
        var published = await client.JsonAsync("POST", "/api/practice/questions/publish", Admin, new { questionIds = ids });
        published.GetProperty("published").GetInt32().ShouldBe(8);
    }

    [Fact]
    public async Task BR_AKSES_05_staff_preview_evaluates_without_storing_anything()
    {
        using var client = api.CreateClient();
        var world = await NewWorldAsync(client);
        var question = await CreateMcqAsync(client, Sensei, world.LevelId, world.Chapters[0], "Uji coba");
        var id = question.GetProperty("id").GetGuid();
        var before = await CountAsync("select count(*) from practice.drill_answers");

        var preview = await client.JsonAsync("POST", "/api/practice/preview/drill", Sensei, new { category = "hiragana", chapterIds = new[] { world.Chapters[0] }, includeDrafts = true });
        preview.GetArrayLength().ShouldBe(1);
        preview[0].TryGetProperty("correct", out _).ShouldBeFalse();
        var result = await client.JsonAsync("POST", $"/api/practice/questions/{id}/preview-answer", Sensei, new { answer = new { selected = One } });
        result.GetProperty("isCorrect").GetBoolean().ShouldBeTrue();

        (await CountAsync("select count(*) from practice.drill_answers")).ShouldBeLessThanOrEqualTo(before + 100); // other tests run in parallel
        (await CountAsync($"select count(*) from practice.drill_answers where question_id = '{id}'")).ShouldBe(0);
        (await client.JsonAsync("GET", $"/api/practice/questions/{id}", Sensei)).GetProperty("hasAnswers").GetBoolean().ShouldBeFalse();
    }

    private sealed record World(Guid LevelId, Guid BookId, IReadOnlyList<Guid> Chapters);

    private sealed record DrillSetup(Guid LevelId, Guid ClassId, Guid ChapterId, Guid LockedChapterId, Guid QuestionId, Guid StudentId, string Student);

    /// <summary>A level with two chapters, a class (sensei teaches) with chapter 1 open, a new student and one published question.</summary>
    private static async Task<DrillSetup> NewDrillSetupAsync(HttpClient client)
    {
        var world = await NewWorldAsync(client);
        var created = await client.JsonAsync("POST", "/api/classroom/classes", Sensei, new { name = $"Latihan {Guid.NewGuid():N}"[..17], levelId = world.LevelId, bookId = world.BookId });
        var classId = created.GetProperty("id").GetGuid();
        var user = await client.JsonAsync("POST", "/api/identity/users", Admin, new { username = $"drill.{Guid.NewGuid():N}"[..14], displayName = "Murid Latihan", role = "murid" });
        var studentId = user.GetProperty("user").GetProperty("id").GetGuid();
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Sensei, new { studentIds = new[] { studentId } });
        await client.JsonAsync("PUT", $"/api/classroom/classes/{classId}/unlocks/{world.Chapters[0]}", Sensei);
        var question = await CreateMcqAsync(client, Sensei, world.LevelId, world.Chapters[0], "Pilih romaji");
        var questionId = question.GetProperty("id").GetGuid();
        await client.JsonAsync("POST", $"/api/practice/questions/{questionId}/publish", Sensei);
        return new DrillSetup(world.LevelId, classId, world.Chapters[0], world.Chapters[1], questionId, studentId, TestJwt.For(studentId, Roles.Murid));
    }

    private static object McqBody(Guid levelId, Guid chapterId, string prompt, string scope = "private") => new
    {
        kind = "mcq",
        levelId,
        chapterId,
        category = "hiragana",
        prompt = new { id = prompt },
        payload = new
        {
            stimulus = new { ja = new[] { new { t = "あ" } } },
            choices = new[] { new { id = "i" }, new { id = "a" }, new { id = "u" } },
            correct = One,
            multi = false,
            jaDisplay = "no_romaji",
        },
        scope,
    };

    private static Task<JsonElement> CreateMcqAsync(HttpClient client, string token, Guid levelId, Guid chapterId, string prompt) =>
        client.JsonAsync("POST", "/api/practice/questions", token, McqBody(levelId, chapterId, prompt));

    private static Task<JsonElement> AnswerAsync(HttpClient client, string token, Guid sessionId, Guid questionId, Guid answerId, int choice) =>
        client.JsonAsync("POST", $"/api/practice/drill-sessions/{sessionId}/answers", token, new
        {
            answerId,
            questionId,
            inputMode = "choice",
            answer = new { selected = new[] { choice } },
            responseMs = 1200,
        });

    private static async Task<World> NewWorldAsync(HttpClient client)
    {
        var code = $"p{Guid.NewGuid():N}"[..12];
        var level = await client.JsonAsync("POST", "/api/content/levels", Admin, new { code, name = new { id = $"Level {code}" }, sortOrder = 60 });
        var levelId = level.GetProperty("id").GetGuid();
        var book = await client.JsonAsync("POST", "/api/content/books", Admin, new { levelId, title = new { id = "Buku latihan" }, sortOrder = 0 });
        var bookId = book.GetProperty("id").GetGuid();
        var chapters = new List<Guid>();
        foreach (var number in new[] { 1, 2 })
        {
            var chapter = await client.JsonAsync("POST", "/api/content/chapters", Admin, new { bookId, number, title = new { id = $"Bab {number}" }, sortOrder = number });
            chapters.Add(chapter.GetProperty("id").GetGuid());
        }

        return new World(levelId, bookId, chapters);
    }

    /// <summary>Imports katakana "char:romaji" lines into chapter 1 of the world.</summary>
    private static async Task ImportKanaAsync(HttpClient client, World world, string lines)
    {
        var csv = "char,script,romaji,row_key,variant,chapter\n" + string.Join('\n', lines.Split('\n').Select((line, index) =>
        {
            var parts = line.Split(':');
            return $"{parts[0]},katakana,{parts[1]},a,seion,1";
        }));
        using var content = new MultipartFormDataContent
        {
            { new StringContent(csv, System.Text.Encoding.UTF8, "text/csv"), "file", "kana.csv" },
            { new StringContent("Uji integrasi"), "source" },
            { new StringContent("public-domain"), "license" },
            { new StringContent(world.LevelId.ToString()), "levelId" },
            { new StringContent(world.BookId.ToString()), "bookId" },
        };
        await client.JsonAsync("POST", "/api/content/reference/kana/import?dryRun=false", Admin, content);
    }

    private static async Task<string> OtherSenseiAsync(HttpClient client)
    {
        var created = await client.JsonAsync("POST", "/api/identity/users", Admin, new { username = $"sensei.{Guid.NewGuid():N}"[..15], displayName = "Sensei Lain", role = "sensei" });
        return TestJwt.For(created.GetProperty("user").GetProperty("id").GetGuid(), Roles.Sensei);
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(api.PostgresConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Cancel), System.Globalization.CultureInfo.InvariantCulture);
    }
}
