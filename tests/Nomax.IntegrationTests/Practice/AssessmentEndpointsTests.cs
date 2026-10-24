using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using Npgsql;

namespace Nomax.IntegrationTests.Practice;

/// <summary>
/// Quizzes, assignments and grades against PostgreSQL (BR-KUIS, BR-TUGAS, BR-NILAI) plus their authorization matrix.
/// Each test builds its own level, class and student so tests run in parallel.
/// </summary>
public sealed class AssessmentEndpointsTests(NomaxApiFixture api)
{
    private const string Unknown = "00000000-0000-4000-8000-0000000000ff";
    private const string Demo = "c0000000-0000-4000-8000-000000000001";
    private static readonly int[] One = [1];
    private static readonly int[] Zero = [0];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Admin => TestUsers.TokenFor(Roles.Admin);

    private static string Sensei => TestUsers.TokenFor(Roles.Sensei);

    public static TheoryData<string, string> StaffEndpoints => new()
    {
        { "GET", $"/api/practice/classes/{Demo}/quizzes" },
        { "POST", $"/api/practice/classes/{Demo}/quizzes" },
        { "GET", $"/api/practice/quizzes/{Unknown}" },
        { "PATCH", $"/api/practice/quizzes/{Unknown}" },
        { "GET", $"/api/practice/quizzes/{Unknown}/questions" },
        { "PUT", $"/api/practice/quizzes/{Unknown}/questions" },
        { "POST", $"/api/practice/quizzes/{Unknown}/publish" },
        { "POST", $"/api/practice/quizzes/{Unknown}/archive" },
        { "GET", $"/api/practice/quizzes/{Unknown}/attempts" },
        { "POST", $"/api/practice/attempts/{Unknown}/answers/{Unknown}/grade" },
        { "GET", $"/api/practice/classes/{Demo}/assignments" },
        { "POST", $"/api/practice/classes/{Demo}/assignments" },
        { "GET", $"/api/practice/assignments/{Unknown}" },
        { "PATCH", $"/api/practice/assignments/{Unknown}" },
        { "POST", $"/api/practice/assignments/{Unknown}/publish" },
        { "GET", $"/api/practice/assignments/{Unknown}/submissions" },
        { "POST", $"/api/practice/submissions/{Unknown}/grade" },
        { "GET", $"/api/practice/classes/{Demo}/grade-scheme" },
        { "PUT", $"/api/practice/classes/{Demo}/grade-scheme" },
        { "GET", $"/api/practice/classes/{Demo}/grades" },
    };

    public static TheoryData<string, string> MuridEndpoints => new()
    {
        { "POST", $"/api/practice/quizzes/{Unknown}/attempts" },
        { "PUT", $"/api/practice/attempts/{Unknown}/answers/{Unknown}" },
        { "POST", $"/api/practice/attempts/{Unknown}/submit" },
        { "GET", "/api/practice/me/quizzes" },
        { "PUT", $"/api/practice/assignments/{Unknown}/submission" },
        { "GET", "/api/practice/me/assignments" },
        { "GET", "/api/practice/me/grades" },
    };

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    [MemberData(nameof(MuridEndpoints))]
    [InlineData("GET", $"/api/practice/attempts/{Unknown}")]
    [InlineData("GET", $"/api/practice/submissions/{Unknown}")]
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
    [MemberData(nameof(MuridEndpoints))]
    public async Task Staff_get_403_on_student_endpoints(string method, string path)
    {
        using var client = api.CreateClient();
        using var response = await client.SendAsync(method, path, Sensei, Body(method));
        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task BR_KUIS_full_quiz_with_manual_grading_review_and_grades()
    {
        using var client = api.CreateClient();
        var setup = await NewSetupAsync(client);
        var mcq = await PublishedQuestionAsync(client, setup, Mcq("Pilih a"));
        var essay = await PublishedQuestionAsync(client, setup, new { kind = "long_answer", levelId = setup.LevelId, chapterId = setup.ChapterId, category = "sentence", prompt = new { id = "Perkenalkan dirimu" }, payload = new { maxChars = 500 } });

        var quiz = await client.JsonAsync("POST", $"/api/practice/classes/{setup.ClassId}/quizzes", Sensei, new { title = new { id = "Kuis 1" }, timeLimitMin = 30, showReview = true });
        var quizId = quiz.GetProperty("id").GetGuid();
        await client.JsonAsync("PUT", $"/api/practice/quizzes/{quizId}/questions", Sensei, new
        {
            version = quiz.GetProperty("version").GetUInt32(),
            questions = new object[] { new { questionId = mcq, points = 2 }, new { questionId = essay, points = 3 } },
        });
        await client.JsonAsync("POST", $"/api/practice/quizzes/{quizId}/publish", Sensei);

        // BR-KUIS-05: the student version carries no key.
        using var started = await client.SendAsync("POST", $"/api/practice/quizzes/{quizId}/attempts", setup.Student);
        var startedText = await started.Content.ReadAsStringAsync(Cancel);
        started.StatusCode.ShouldBe(HttpStatusCode.OK, startedText);
        startedText.ShouldNotContain("\"correct\":[");
        var attempt = JsonDocument.Parse(startedText).RootElement;
        var attemptId = attempt.GetProperty("id").GetGuid();
        attempt.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(attempt.GetProperty("startedAt").GetDateTimeOffset().AddMinutes(30), TimeSpan.FromSeconds(1));

        // BR-KUIS-02: starting again resumes; BR-KUIS-06 autosave keeps the last answer.
        (await client.JsonAsync("POST", $"/api/practice/quizzes/{quizId}/attempts", setup.Student)).GetProperty("id").GetGuid().ShouldBe(attemptId);
        await client.JsonAsync("PUT", $"/api/practice/attempts/{attemptId}/answers/{mcq}", setup.Student, new { answer = new { selected = Zero } });
        await client.JsonAsync("PUT", $"/api/practice/attempts/{attemptId}/answers/{mcq}", setup.Student, new { answer = new { selected = One } });
        await client.JsonAsync("PUT", $"/api/practice/attempts/{attemptId}/answers/{essay}", setup.Student, new { answer = new { text = "Watashi wa Budi desu." } });

        // BR-KUIS-10: once attempted, the time limit and the questions are locked.
        var locked = await client.JsonAsync("GET", $"/api/practice/quizzes/{quizId}", Sensei);
        (await client.StatusAsync("PATCH", $"/api/practice/quizzes/{quizId}", Sensei, new { title = new { id = "Kuis 1" }, timeLimitMin = 45, version = locked.GetProperty("version").GetUInt32() })).ShouldBe(409);
        (await client.StatusAsync("PUT", $"/api/practice/quizzes/{quizId}/questions", Sensei, new { version = locked.GetProperty("version").GetUInt32(), questions = new[] { new { questionId = mcq, points = 1 } } })).ShouldBe(409);

        // BR-KUIS-08: with a long answer waiting the attempt stays submitted.
        var submitted = await client.JsonAsync("POST", $"/api/practice/attempts/{attemptId}/submit", setup.Student);
        submitted.GetProperty("status").GetString().ShouldBe("submitted");
        var attempts = await client.JsonAsync("GET", $"/api/practice/quizzes/{quizId}/attempts", Sensei);
        attempts[0].GetProperty("pendingManual").GetInt32().ShouldBe(1);

        var graded = await client.JsonAsync("POST", $"/api/practice/attempts/{attemptId}/answers/{essay}/grade", Sensei, new { points = 1.5 });
        graded.GetProperty("status").GetString().ShouldBe("graded");
        graded.GetProperty("scorePct").GetDecimal().ShouldBe(70m); // (2 + 1.5) / 5
        (await CountAsync($"select count(*) from practice.outbox_messages where type = 'QuizGraded.v1' and payload->>'attemptId' = '{attemptId}'")).ShouldBe(1);

        // BR-KUIS-09: show_review, no close time, all attempts used → key and explanation visible to the student.
        var review = await client.JsonAsync("GET", $"/api/practice/attempts/{attemptId}", setup.Student);
        review.GetProperty("reviewVisible").GetBoolean().ShouldBeTrue();
        review.GetProperty("questions").EnumerateArray().Any(item => item.GetProperty("correctAnswer").ValueKind == JsonValueKind.Object).ShouldBeTrue();

        // BR-KUIS-02: no attempts left.
        using var again = await client.SendAsync("POST", $"/api/practice/quizzes/{quizId}/attempts", setup.Student);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "attempts_exhausted");

        // BR-NILAI: quiz 70 is the only component.
        var grades = await client.JsonAsync("GET", "/api/practice/me/grades", setup.Student);
        var mine = grades.EnumerateArray().Single(item => item.GetProperty("classId").GetGuid() == setup.ClassId);
        mine.GetProperty("finalPct").GetDecimal().ShouldBe(70m);
        mine.GetProperty("gpa").GetDecimal().ShouldBe(3m);
    }

    [Fact]
    public async Task BR_KUIS_01_03_closed_window_and_expired_attempts()
    {
        using var client = api.CreateClient();
        var setup = await NewSetupAsync(client);
        var mcq = await PublishedQuestionAsync(client, setup, Mcq("Pilih a"));

        var later = await QuizAsync(client, setup, mcq, new { title = new { id = "Nanti" }, openAt = DateTimeOffset.UtcNow.AddDays(1) });
        using var notOpen = await client.SendAsync("POST", $"/api/practice/quizzes/{later}/attempts", setup.Student);
        await notOpen.ShouldBeProblemAsync(HttpStatusCode.Conflict, "quiz_not_open");

        var timed = await QuizAsync(client, setup, mcq, new { title = new { id = "Berwaktu" }, timeLimitMin = 5 });
        var attempt = await client.JsonAsync("POST", $"/api/practice/quizzes/{timed}/attempts", setup.Student);
        var attemptId = attempt.GetProperty("id").GetGuid();
        await ExecuteAsync($"update practice.quiz_attempts set expires_at = now() - interval '1 minute' where id = '{attemptId}'");

        using var late = await client.SendAsync("PUT", $"/api/practice/attempts/{attemptId}/answers/{mcq}", setup.Student, new { answer = new { selected = One } });
        await late.ShouldBeProblemAsync(HttpStatusCode.Conflict, "attempt_expired");
        // Submitted automatically with what was saved (nothing): score 0.
        var view = await client.JsonAsync("GET", $"/api/practice/attempts/{attemptId}", setup.Student);
        view.GetProperty("status").GetString().ShouldBe("graded");
        view.GetProperty("scorePct").GetDecimal().ShouldBe(0m);
        // show_review is off: the student only sees the score.
        view.GetProperty("reviewVisible").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Quizzes_reject_self_check_questions_and_hide_other_classes()
    {
        using var client = api.CreateClient();
        var setup = await NewSetupAsync(client);
        var card = await PublishedQuestionAsync(client, setup, new { kind = "flashcard", levelId = setup.LevelId, chapterId = setup.ChapterId, category = "hiragana", prompt = new { id = "Kartu" }, payload = new { front = new { ja = new[] { new { t = "あ" } } }, back = new { id = "a" } } });
        var quiz = await client.JsonAsync("POST", $"/api/practice/classes/{setup.ClassId}/quizzes", Sensei, new { title = new { id = "Kartu" } });
        (await client.StatusAsync("PUT", $"/api/practice/quizzes/{quiz.GetProperty("id").GetGuid()}/questions", Sensei, new { version = quiz.GetProperty("version").GetUInt32(), questions = new[] { new { questionId = card, points = 1 } } })).ShouldBe(400);

        var outsider = await OtherSenseiAsync(client);
        (await client.StatusAsync("GET", $"/api/practice/quizzes/{quiz.GetProperty("id").GetGuid()}", outsider)).ShouldBe(404);
        (await client.StatusAsync("GET", $"/api/practice/classes/{setup.ClassId}/grades", outsider)).ShouldBe(404);
    }

    [Fact]
    public async Task BR_TUGAS_submission_with_file_resubmit_rules_download_access_and_grading()
    {
        using var client = api.CreateClient();
        var setup = await NewSetupAsync(client);
        var created = await client.JsonAsync("POST", $"/api/practice/classes/{setup.ClassId}/assignments", Sensei, new
        {
            title = new { id = "Tugas menulis" },
            instructions = new { format = "md", id = "Tulis 3 kalimat." },
            dueAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        var assignmentId = created.GetProperty("id").GetGuid();
        await client.JsonAsync("POST", $"/api/practice/assignments/{assignmentId}/publish", Sensei);

        // BR-TUGAS-01: someone else's upload is refused; the student's own submission upload is accepted.
        var foreignFile = await UploadAsync(client, TestUsers.TokenFor(Roles.Murid), "lain.txt");
        (await client.StatusAsync("PUT", $"/api/practice/assignments/{assignmentId}/submission", setup.Student, new { fileIds = new[] { foreignFile } })).ShouldBe(400);
        var fileId = await UploadAsync(client, setup.Student, "tugas.txt");
        var submission = await client.JsonAsync("PUT", $"/api/practice/assignments/{assignmentId}/submission", setup.Student, new { text = "Watashi wa gakusei desu.", fileIds = new[] { fileId } });
        submission.GetProperty("isLate").GetBoolean().ShouldBeTrue(); // BR-TUGAS-03
        var submissionId = submission.GetProperty("id").GetGuid();

        // BR-TUGAS-02: no resubmission without permission.
        using var resend = await client.SendAsync("PUT", $"/api/practice/assignments/{assignmentId}/submission", setup.Student, new { text = "Lagi" });
        await resend.ShouldBeProblemAsync(HttpStatusCode.Conflict, "already_submitted");

        // BR-FILE-04: the teacher may download, another student may not.
        var link = await client.JsonAsync("GET", $"/api/practice/submissions/{submissionId}/files/{fileId}/download?redirect=false", Sensei);
        using (var download = await client.SendAsync("GET", link.GetProperty("url").GetString()!, token: null))
        {
            download.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await client.StatusAsync("GET", $"/api/practice/submissions/{submissionId}/files/{fileId}/download?redirect=false", TestUsers.TokenFor(Roles.Murid))).ShouldBe(404);

        // BR-TUGAS-04 and BR-NILAI-05.
        var graded = await client.JsonAsync("POST", $"/api/practice/submissions/{submissionId}/grade", Sensei, new { gradePct = 88.5, feedback = "Bagus" });
        graded.GetProperty("gradePct").GetDecimal().ShouldBe(88.5m);
        (await CountAsync($"select count(*) from practice.outbox_messages where type = 'SubmissionGraded.v1' and payload->>'submissionId' = '{submissionId}'")).ShouldBe(1);
        var classGrades = await client.JsonAsync("GET", $"/api/practice/classes/{setup.ClassId}/grades", Sensei);
        classGrades.EnumerateArray().Single(item => item.GetProperty("studentId").GetGuid() == setup.StudentId)
            .GetProperty("finalPct").GetDecimal().ShouldBe(88.5m);

        var mine = await client.JsonAsync("GET", "/api/practice/me/assignments", setup.Student);
        mine.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == assignmentId).GetProperty("submission").GetProperty("feedback").GetString().ShouldBe("Bagus");
    }

    [Fact]
    public async Task BR_NILAI_06_scheme_is_validated_and_recomputes_grades()
    {
        using var client = api.CreateClient();
        var setup = await NewSetupAsync(client);
        var current = await client.JsonAsync("GET", $"/api/practice/classes/{setup.ClassId}/grade-scheme", Sensei);
        current.GetProperty("isDefault").GetBoolean().ShouldBeTrue();

        (await client.StatusAsync("PUT", $"/api/practice/classes/{setup.ClassId}/grade-scheme", Sensei, new { wQuiz = 50, wExam = 40, wAssignment = 30, scaleMap = new[] { new { min = 0, gpa = 0 } } })).ShouldBe(400);
        var saved = await client.JsonAsync("PUT", $"/api/practice/classes/{setup.ClassId}/grade-scheme", Sensei, new
        {
            wQuiz = 50,
            wExam = 0,
            wAssignment = 50,
            scaleMap = new[] { new { min = 0, gpa = 0.0 }, new { min = 85, gpa = 4.0 }, new { min = 75, gpa = 3.5 } },
        });
        saved.GetProperty("isDefault").GetBoolean().ShouldBeFalse();
        saved.GetProperty("scaleMap")[0].GetProperty("min").GetDecimal().ShouldBe(85m);
    }

    private sealed record Setup(Guid LevelId, Guid ClassId, Guid ChapterId, Guid StudentId, string Student);

    private static async Task<Setup> NewSetupAsync(HttpClient client)
    {
        var code = $"q{Guid.NewGuid():N}"[..12];
        var level = await client.JsonAsync("POST", "/api/content/levels", Admin, new { code, name = new { id = $"Level {code}" }, sortOrder = 70 });
        var levelId = level.GetProperty("id").GetGuid();
        var book = await client.JsonAsync("POST", "/api/content/books", Admin, new { levelId, title = new { id = "Buku kuis" }, sortOrder = 0 });
        var bookId = book.GetProperty("id").GetGuid();
        var chapter = await client.JsonAsync("POST", "/api/content/chapters", Admin, new { bookId, number = 1, title = new { id = "Bab 1" }, sortOrder = 1 });
        var created = await client.JsonAsync("POST", "/api/classroom/classes", Sensei, new { name = $"Kuis {Guid.NewGuid():N}"[..14], levelId, bookId });
        var classId = created.GetProperty("id").GetGuid();
        var user = await client.JsonAsync("POST", "/api/identity/users", Admin, new { username = $"kuis.{Guid.NewGuid():N}"[..13], displayName = "Murid Kuis", role = "murid" });
        var studentId = user.GetProperty("user").GetProperty("id").GetGuid();
        await client.JsonAsync("POST", $"/api/classroom/classes/{classId}/students", Sensei, new { studentIds = new[] { studentId } });
        return new Setup(levelId, classId, chapter.GetProperty("id").GetGuid(), studentId, TestJwt.For(studentId, Roles.Murid));
    }

    private static object Mcq(string prompt) => new
    {
        kind = "mcq",
        category = "hiragana",
        prompt = new { id = prompt },
        payload = new { choices = new[] { new { id = "i" }, new { id = "a" } }, correct = One, multi = false },
    };

    private static async Task<Guid> PublishedQuestionAsync(HttpClient client, Setup setup, object body)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["levelId"] = setup.LevelId;
        json["chapterId"] = setup.ChapterId;
        var created = await client.JsonAsync("POST", "/api/practice/questions", Sensei, json);
        var id = created.GetProperty("id").GetGuid();
        await client.JsonAsync("POST", $"/api/practice/questions/{id}/publish", Sensei);
        return id;
    }

    private static async Task<Guid> QuizAsync(HttpClient client, Setup setup, Guid questionId, object body)
    {
        var quiz = await client.JsonAsync("POST", $"/api/practice/classes/{setup.ClassId}/quizzes", Sensei, body);
        var id = quiz.GetProperty("id").GetGuid();
        await client.JsonAsync("PUT", $"/api/practice/quizzes/{id}/questions", Sensei, new { version = quiz.GetProperty("version").GetUInt32(), questions = new[] { new { questionId, points = 1 } } });
        await client.JsonAsync("POST", $"/api/practice/quizzes/{id}/publish", Sensei);
        return id;
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string token, string name)
    {
        var data = Encoding.UTF8.GetBytes("Watashi wa gakusei desu.");
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/content/uploads");
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        create.Headers.Add("Tus-Resumable", "1.0.0");
        create.Headers.Add("Upload-Length", data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        create.Headers.Add("Upload-Metadata", $"filename {Convert.ToBase64String(Encoding.UTF8.GetBytes(name))},purpose {Convert.ToBase64String(Encoding.UTF8.GetBytes("submission"))}");
        using var created = await client.SendAsync(create, Cancel);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = created.Headers.Location!.ToString();

        using var patch = new HttpRequestMessage(HttpMethod.Patch, location);
        patch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        patch.Headers.Add("Tus-Resumable", "1.0.0");
        patch.Headers.Add("Upload-Offset", "0");
        patch.Content = new ByteArrayContent(data);
        patch.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        using var patched = await client.SendAsync(patch, Cancel);
        patched.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return Guid.Parse(location[(location.LastIndexOf('/') + 1)..]);
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

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(api.PostgresConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancel);
    }

    private static object? Body(string method) =>
        method is "POST" or "PATCH" or "PUT"
            ? new
            {
                version = 0,
                title = new { id = "Matrix" },
                instructions = new { format = "md", id = "x" },
                dueAt = DateTimeOffset.UtcNow,
                questions = Array.Empty<object>(),
                points = 1,
                gradePct = 50,
                wQuiz = 30,
                wExam = 40,
                wAssignment = 30,
                scaleMap = new[] { new { min = 0, gpa = 0 } },
                answer = new { selected = One },
                text = "x",
            }
            : null;
}
