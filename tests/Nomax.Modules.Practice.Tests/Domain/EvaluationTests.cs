using System.Text.Json;
using Nomax.Modules.Practice.Domain;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Tests.Domain;

public sealed class EvaluationTests
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, NomaxJson.Options);

    private static TextBlock Ja(string text) => new(null, null, [new JaSegment(text)]);

    private static bool Typed(string accepted, AnswerScript script, string input, bool? kanaInsensitive = null) =>
        AnswerEvaluator.Evaluate(
            QuestionKind.TypeAnswer,
            Json(new TypeAnswerPayload(null, [accepted], script, kanaInsensitive, null)),
            Json(new TypedAnswer(input, null))).IsCorrect;

    // docs/04 BR-DRILL-06, cases 1–8.
    [Theory]
    [InlineData("たべる", AnswerScript.Kana, "たべる ", true)]
    [InlineData("たべる", AnswerScript.Kana, "タベル", true)]
    [InlineData("たべる", AnswerScript.Kana, "たべる", true)] // the client converted "taberu" with WanaKana
    [InlineData("たべる", AnswerScript.Kana, "たべる。", true)]
    [InlineData("toukyou", AnswerScript.Romaji, "tōkyō", true)]
    [InlineData("toukyou", AnswerScript.Romaji, "tokyo", false)]
    [InlineData("ka", AnswerScript.Romaji, "KA", true)]
    [InlineData("たべる", AnswerScript.Kana, "たべた", false)]
    public void BR_DRILL_06_normalization_cases(string accepted, AnswerScript script, string input, bool expected)
    {
        Typed(accepted, script, input).ShouldBe(expected);
    }

    [Fact]
    public void BR_DRILL_06_kana_answers_must_arrive_as_kana()
    {
        Typed("たべる", AnswerScript.Kana, "taberu").ShouldBeFalse();
    }

    [Fact]
    public void BR_DRILL_06_kana_insensitivity_can_be_switched_off()
    {
        Typed("たべる", AnswerScript.Kana, "タベル", kanaInsensitive: false).ShouldBeFalse();
    }

    [Fact]
    public void BR_DRILL_06_spaces_trailing_punctuation_and_romaji_separators_are_ignored()
    {
        AnswerNormalization.Normalize("  たべ　る！ ", AnswerScript.Free, kanaInsensitive: false).ShouldBe("たべ る");
        AnswerNormalization.Normalize("Kon'nichi-wa", AnswerScript.Romaji, kanaInsensitive: false).ShouldBe("konnichiwa");
    }

    [Fact]
    public void BR_DRILL_05_mcq_needs_exactly_the_correct_set()
    {
        var payload = Json(new McqPayload(null, [Ja("あ"), Ja("い"), Ja("う")], [0, 2], true, null));
        AnswerEvaluator.Evaluate(QuestionKind.Mcq, payload, Json(new McqAnswer([2, 0]))).IsCorrect.ShouldBeTrue();
        AnswerEvaluator.Evaluate(QuestionKind.Mcq, payload, Json(new McqAnswer([0]))).IsCorrect.ShouldBeFalse();
        AnswerEvaluator.Evaluate(QuestionKind.Mcq, payload, Json(new McqAnswer([0, 1, 2]))).IsCorrect.ShouldBeFalse();
        AnswerEvaluator.Evaluate(QuestionKind.Mcq, payload, Json(new McqAnswer([0, 0, 2]))).IsCorrect.ShouldBeFalse();
    }

    [Fact]
    public void BR_DRILL_05_tap_sentence_accepts_any_listed_order()
    {
        var payload = Json(new TapSentencePayload(new TextBlock("Saya makan.", null, null), [Ja("わたし"), Ja("は"), Ja("たべます")], [[0, 1, 2], [2, 0, 1]], null));
        AnswerEvaluator.Evaluate(QuestionKind.TapSentence, payload, Json(new TapAnswer([0, 1, 2]))).IsCorrect.ShouldBeTrue();
        AnswerEvaluator.Evaluate(QuestionKind.TapSentence, payload, Json(new TapAnswer([2, 0, 1]))).IsCorrect.ShouldBeTrue();
        AnswerEvaluator.Evaluate(QuestionKind.TapSentence, payload, Json(new TapAnswer([1, 0, 2]))).IsCorrect.ShouldBeFalse();
    }

    [Fact]
    public void BR_DRILL_05_match_in_drilling_needs_every_pair()
    {
        var payload = Json(new MatchPayload([new MatchPair(Ja("あ"), new("a", null, null)), new MatchPair(Ja("い"), new("i", null, null)), new MatchPair(Ja("う"), new("u", null, null))], null));
        int[][] all = [[0, 0], [1, 1], [2, 2]];
        int[][] oneWrong = [[0, 0], [1, 2], [2, 1]];
        int[][] missing = [[0, 0], [1, 1]];
        AnswerEvaluator.Evaluate(QuestionKind.Match, payload, Json(new { pairs = all })).IsCorrect.ShouldBeTrue();
        AnswerEvaluator.Evaluate(QuestionKind.Match, payload, Json(new { pairs = oneWrong })).IsCorrect.ShouldBeFalse();
        AnswerEvaluator.Evaluate(QuestionKind.Match, payload, Json(new { pairs = missing })).IsCorrect.ShouldBeFalse();
    }

    [Fact]
    public void BR_DRILL_05_flashcards_are_a_self_check()
    {
        var payload = Json(new FlashcardPayload(Ja("か"), new TextBlock("ka", null, null), null));
        var remembered = AnswerEvaluator.Evaluate(QuestionKind.Flashcard, payload, Json(new FlashcardAnswer(true)));
        remembered.IsCorrect.ShouldBeTrue();
        remembered.InputMode.ShouldBe(InputMode.SelfCheck);
        AnswerEvaluator.Evaluate(QuestionKind.Flashcard, payload, Json(new FlashcardAnswer(false))).IsCorrect.ShouldBeFalse();
    }

    [Fact]
    public void BR_DRILL_05_wrongly_shaped_answers_are_rejected()
    {
        var payload = Json(new McqPayload(null, [Ja("あ"), Ja("い")], [0], false, null));
        Should.Throw<JsonException>(() => AnswerEvaluator.Evaluate(QuestionKind.Mcq, payload, Json(new { selected = "zero" })));
        Should.Throw<JsonException>(() => AnswerEvaluator.Evaluate(QuestionKind.Mcq, payload, JsonDocument.Parse("[1]").RootElement));
    }

    [Fact]
    public void Payload_validation_reports_problems_per_kind()
    {
        Payloads.Validate(QuestionKind.Mcq, Json(new McqPayload(null, [Ja("あ")], [3], false, null)))
            .Select(issue => issue.Path).ShouldBe(["choices", "correct"], ignoreOrder: true);
        Payloads.Validate(QuestionKind.Mcq, Json(new McqPayload(null, [Ja("あ"), Ja("い")], [0, 1], false, null)))
            .ShouldHaveSingleItem().Path.ShouldBe("correct");
        Payloads.Validate(QuestionKind.TypeAnswer, Json(new TypeAnswerPayload(null, ["taberu"], AnswerScript.Kana, null, null)))
            .ShouldHaveSingleItem().Path.ShouldBe("accepted[0]");
        Payloads.Validate(QuestionKind.Match, Json(new MatchPayload([new MatchPair(Ja("あ"), Ja("a"))], null)))
            .ShouldHaveSingleItem().Path.ShouldBe("pairs");
        Payloads.Validate(QuestionKind.Mcq, Json(new McqPayload(new TextBlock(null, null, [new JaSegment("漢字")]), [Ja("あ"), Ja("い")], [0], false, null)))
            .ShouldHaveSingleItem().Path.ShouldBe("stimulus.ja[0].r");
        Payloads.Validate(QuestionKind.Flashcard, Json(new { front = new { id = "x" } })).ShouldNotBeEmpty();
    }
}
