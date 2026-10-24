using System.Text.Json;
using Nomax.Modules.Practice.Domain;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Tests.Domain;

public sealed class AssessmentRulesTests
{
    private static readonly DateTimeOffset Ten = new(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(7));
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, NomaxJson.Options);

    public static TheoryData<int?, int?, int?> ExpiryCases => new()
    {
        // docs/04 BR-KUIS-03 cases 1–4: time limit (minutes), close (minutes after start), expected expiry (minutes).
        { 30, null, 30 },
        { 30, 20, 20 },
        { null, 120, 120 },
        { null, null, null },
    };

    [Theory]
    [MemberData(nameof(ExpiryCases))]
    public void BR_KUIS_03_expiry_is_the_earlier_of_time_limit_and_close(int? limit, int? closeAfter, int? expected)
    {
        var close = closeAfter is { } minutes ? Ten.AddMinutes(minutes) : (DateTimeOffset?)null;
        QuizRules.ExpiresAt(Ten, limit, close).ShouldBe(expected is { } value ? Ten.AddMinutes(value) : null);
    }

    [Fact]
    public void BR_KUIS_03_answers_are_refused_after_the_grace_period()
    {
        var expires = Ten.AddMinutes(30);
        var grace = TimeSpan.FromSeconds(10);
        QuizRules.IsExpired(expires, expires.AddSeconds(10), grace).ShouldBeFalse();
        QuizRules.IsExpired(expires, expires.AddSeconds(11), grace).ShouldBeTrue();
        QuizRules.IsExpired(null, expires.AddDays(30), grace).ShouldBeFalse();
    }

    [Fact]
    public void BR_KUIS_01_quizzes_are_open_only_inside_their_window()
    {
        QuizRules.IsOpen(null, null, Now).ShouldBeTrue();
        QuizRules.IsOpen(Now.AddMinutes(1), null, Now).ShouldBeFalse();
        QuizRules.IsOpen(Now, Now.AddMinutes(1), Now).ShouldBeTrue();
        QuizRules.IsOpen(null, Now, Now).ShouldBeFalse();
    }

    [Fact]
    public void BR_KUIS_07_match_earns_partial_points_and_other_kinds_all_or_nothing()
    {
        var match = Json(new MatchPayload(
            [new(new("a", null, null), new("1", null, null)), new(new("b", null, null), new("2", null, null)), new(new("c", null, null), new("3", null, null))], null));
        int[][] twoOfThree = [[0, 0], [1, 1], [2, 0]];
        QuizRules.Grade(QuestionKind.Match, match, Json(new { pairs = twoOfThree }), 2m).ShouldBe(new AnswerGrade(false, 1.33m));

        var mcq = Json(new McqPayload(null, [new("x", null, null), new("y", null, null)], [1], false, null));
        QuizRules.Grade(QuestionKind.Mcq, mcq, Json(new McqAnswer([1])), 3m).ShouldBe(new AnswerGrade(true, 3m));
        QuizRules.Grade(QuestionKind.Mcq, mcq, Json(new McqAnswer([0])), 3m).ShouldBe(new AnswerGrade(false, 0m));
        QuizRules.Grade(QuestionKind.Mcq, mcq, null, 3m).ShouldBe(new AnswerGrade(false, 0m));
    }

    [Fact]
    public void BR_KUIS_07_long_answers_wait_for_a_teacher_unless_empty_and_self_checks_are_not_allowed()
    {
        var payload = Json(new LongAnswerPayload(null, null));
        QuizRules.Grade(QuestionKind.LongAnswer, payload, Json(new { text = "Watashi wa..." }), 5m).ShouldBe(new AnswerGrade(null, null));
        QuizRules.Grade(QuestionKind.LongAnswer, payload, null, 5m).ShouldBe(new AnswerGrade(false, 0m));
        QuizRules.IsAllowedInQuiz(QuestionKind.Flashcard).ShouldBeFalse();
        QuizRules.IsAllowedInQuiz(QuestionKind.HandwriteKanji).ShouldBeFalse();
        QuizRules.IsAllowedInQuiz(QuestionKind.LongAnswer).ShouldBeTrue();
    }

    [Fact]
    public void BR_KUIS_08_score_is_a_rounded_percentage_of_the_points()
    {
        QuizRules.ScorePct(2m, 3m).ShouldBe(66.67m);
        QuizRules.ScorePct(0m, 0m).ShouldBe(0m);
    }

    [Theory]
    [InlineData(true, true, 1, 3, true)] // after close_at
    [InlineData(true, false, 1, 3, false)] // before close_at
    [InlineData(false, true, 3, 3, false)] // show_review off: score only
    public void BR_KUIS_09_review_with_a_close_time(bool showReview, bool closed, int used, int max, bool expected)
    {
        var close = closed ? Now.AddMinutes(-1) : Now.AddMinutes(1);
        QuizRules.CanSeeReview(showReview, close, Now, used, max).ShouldBe(expected);
    }

    [Fact]
    public void BR_KUIS_09_review_without_a_close_time_needs_all_attempts_used()
    {
        QuizRules.CanSeeReview(true, null, Now, 1, 2).ShouldBeFalse();
        QuizRules.CanSeeReview(true, null, Now, 2, 2).ShouldBeTrue();
    }

    [Fact]
    public void BR_KUIS_10_after_the_first_attempt_only_safe_fields_change()
    {
        var quiz = Nomax.Modules.Practice.Domain.Quiz.Create(Guid.NewGuid(), Guid.NewGuid(), null, QuizKind.Quiz, new TextBlock("Kuis", null, null), 30, 1, null, Now, true, false, Guid.NewGuid());

        QuizRules.LockedChange(quiz, QuizKind.Quiz, null, 30, 2, null, Now.AddDays(1), true).ShouldBeNull();
        QuizRules.LockedChange(quiz, QuizKind.Quiz, null, 45, 1, null, Now, true).ShouldBe("timeLimitMin");
        QuizRules.LockedChange(quiz, QuizKind.Exam, null, 30, 1, null, Now, true).ShouldBe("kind");
        QuizRules.LockedChange(quiz, QuizKind.Quiz, null, 30, 0, null, Now, true).ShouldBe("maxAttempts");
        QuizRules.LockedChange(quiz, QuizKind.Quiz, null, 30, 1, null, Now.AddHours(-1), true).ShouldBe("closeAt");
    }

    [Fact]
    public void BR_TUGAS_rules()
    {
        AssignmentRules.CanResubmit(allowResubmit: true, gradePct: null).ShouldBeTrue();
        AssignmentRules.CanResubmit(allowResubmit: true, gradePct: 80m).ShouldBeFalse();
        AssignmentRules.CanResubmit(allowResubmit: false, gradePct: null).ShouldBeFalse();
        AssignmentRules.IsLate(Now.AddSeconds(1), Now).ShouldBeTrue();
        AssignmentRules.IsLate(Now, Now).ShouldBeFalse();
        AssignmentRules.HasContent("  ", []).ShouldBeFalse();
        AssignmentRules.HasContent(null, [Guid.NewGuid()]).ShouldBeTrue();
    }

    private static QuizResultInput QuizOf(decimal? best, DateTimeOffset? close = null) => new(QuizKind.Quiz, close, best);

    private static QuizResultInput ExamOf(decimal? best, DateTimeOffset? close = null) => new(QuizKind.Exam, close, best);

    private static AssignmentResultInput TaskOf(decimal? grade, bool submitted = true, DateTimeOffset? due = null) => new(due ?? Now.AddDays(-1), submitted, grade);

    [Fact]
    public void BR_NILAI_case_1_all_three_components()
    {
        var result = GradeCalculator.Compute([QuizOf(85m), ExamOf(72m)], [TaskOf(90m)], GradeSettings.Default, Now);
        result.FinalPct.ShouldBe(81.30m);
        result.Gpa.ShouldBe(4m);
    }

    [Fact]
    public void BR_NILAI_case_2_weights_are_normalized_without_exams()
    {
        var result = GradeCalculator.Compute([QuizOf(70m)], [TaskOf(80m)], GradeSettings.Default, Now);
        result.FinalPct.ShouldBe(75.00m);
        result.Gpa.ShouldBe(3m);
        result.Breakdown.WeightsUsed.Keys.ShouldBe(["quiz", "assignment"], ignoreOrder: true);
    }

    [Fact]
    public void BR_NILAI_case_3_best_attempt_and_missed_closed_quiz_counts_zero()
    {
        // Quiz A: attempts 60 and 85 → the caller passes the best (85); quiz B closed without an attempt → 0.
        GradeCalculator.QuizAverage([QuizOf(85m), QuizOf(null, Now.AddHours(-1))], Now).ShouldBe(42.50m);
        GradeCalculator.QuizAverage([QuizOf(null, Now.AddHours(1))], Now).ShouldBeNull();
    }

    [Fact]
    public void BR_NILAI_case_4_submitted_but_ungraded_is_not_counted()
    {
        GradeCalculator.AssignmentAverage([TaskOf(null, submitted: true)], Now).ShouldBeNull();
        GradeCalculator.AssignmentAverage([TaskOf(null, submitted: false)], Now).ShouldBe(0m);
        GradeCalculator.AssignmentAverage([TaskOf(null, submitted: false, due: Now.AddDays(1))], Now).ShouldBeNull();
    }

    [Theory]
    [InlineData("79.995", "80.00", "4")]
    [InlineData("79.994", "79.99", "3")]
    public void BR_NILAI_cases_5_6_rounding_happens_before_the_scale(string raw, string final, string gpa)
    {
        var rounded = GradeCalculator.Round(decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture));
        rounded.ShouldBe(decimal.Parse(final, System.Globalization.CultureInfo.InvariantCulture));
        GradeCalculator.ToGpa(rounded, GradeSettings.DefaultScale).ShouldBe(decimal.Parse(gpa, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BR_NILAI_case_7_no_component_gives_no_grade()
    {
        var result = GradeCalculator.Compute([], [], GradeSettings.Default, Now);
        result.FinalPct.ShouldBeNull();
        result.Gpa.ShouldBeNull();
    }

    [Fact]
    public void BR_NILAI_case_8_custom_scale()
    {
        ScaleStep[] scale = [new(85m, 4m), new(75m, 3.5m), new(65m, 3m), new(0m, 0m)];
        GradeCalculator.ToGpa(78m, scale).ShouldBe(3.5m);
    }

    [Fact]
    public void BR_NILAI_06_scheme_validation()
    {
        GradeCalculator.Validate(30m, 40m, 30m, GradeSettings.DefaultScale).ShouldBeEmpty();
        GradeCalculator.Validate(30m, 40m, 20m, GradeSettings.DefaultScale).ShouldContain("weights");
        GradeCalculator.Validate(30m, 40m, 30m, [new(80m, 4m), new(50m, 1m)]).ShouldContain("scaleMap.min");
        GradeCalculator.Validate(30m, 40m, 30m, [new(80m, 4m), new(80m, 3m), new(0m, 0m)]).ShouldContain("scaleMap.min");
        GradeCalculator.Validate(30m, 40m, 30m, [new(80m, 5m), new(0m, 0m)]).ShouldContain("scaleMap.gpa");
    }

    [Fact]
    public void Answer_keys_exist_for_every_auto_graded_kind()
    {
        AnswerEvaluator.KeyOf(QuestionKind.Mcq, Json(new McqPayload(null, [new("a", null, null), new("b", null, null)], [1], false, null)))!
            .Value.GetProperty("correct")[0].GetInt32().ShouldBe(1);
        AnswerEvaluator.KeyOf(QuestionKind.LongAnswer, Json(new LongAnswerPayload(new RichText("md", "rubrik", null), null))).ShouldBeNull();
    }
}
