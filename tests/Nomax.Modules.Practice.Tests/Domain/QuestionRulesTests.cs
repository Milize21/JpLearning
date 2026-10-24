using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Tests.Domain;

public sealed class QuestionRulesTests
{
    [Theory]
    [InlineData(QuestionStatus.Draft, QuestionScope.Private, false, true)]
    [InlineData(QuestionStatus.Review, QuestionScope.Private, false, true)]
    [InlineData(QuestionStatus.Draft, QuestionScope.Builtin, false, false)]
    [InlineData(QuestionStatus.Review, QuestionScope.Builtin, true, true)]
    [InlineData(QuestionStatus.Published, QuestionScope.Private, true, false)]
    [InlineData(QuestionStatus.Archived, QuestionScope.Builtin, true, false)]
    public void BR_SOAL_02_who_may_publish(QuestionStatus status, QuestionScope scope, bool isAdmin, bool expected)
    {
        QuestionRules.CanPublish(status, scope, isAdmin).ShouldBe(expected);
    }

    [Fact]
    public void BR_SOAL_02_generated_questions_start_in_review_and_drafts_go_to_review()
    {
        QuestionRules.InitialStatusForGenerated.ShouldBe(QuestionStatus.Review);
        QuestionRules.CanSubmitForReview(QuestionStatus.Draft).ShouldBeTrue();
        QuestionRules.CanSubmitForReview(QuestionStatus.Published).ShouldBeFalse();
        QuestionRules.CanArchive(QuestionStatus.Archived).ShouldBeFalse();
    }

    [Fact]
    public void BR_SOAL_03_visibility_and_management()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        QuestionRules.IsVisibleToStaff(QuestionScope.Builtin, owner, other, isAdmin: false).ShouldBeTrue();
        QuestionRules.IsVisibleToStaff(QuestionScope.Private, owner, other, isAdmin: false).ShouldBeFalse();
        QuestionRules.IsVisibleToStaff(QuestionScope.Private, owner, other, isAdmin: true).ShouldBeTrue();
        QuestionRules.CanManage(QuestionScope.Builtin, owner, owner, isAdmin: false).ShouldBeFalse();
        QuestionRules.CanManage(QuestionScope.Private, owner, owner, isAdmin: false).ShouldBeTrue();
    }

    [Fact]
    public void BR_SOAL_04_answered_questions_keep_their_content()
    {
        QuestionRules.CanEditContent(QuestionStatus.Published, hasAnswers: true).ShouldBeFalse();
        QuestionRules.CanEditContent(QuestionStatus.Published, hasAnswers: false).ShouldBeTrue();
        QuestionRules.CanEditContent(QuestionStatus.Archived, hasAnswers: false).ShouldBeFalse();
    }

    [Theory]
    [InlineData(QuestionKind.LongAnswer, InputMode.Choice, false)]
    [InlineData(QuestionKind.HandwriteKana, InputMode.Choice, false)]
    [InlineData(QuestionKind.HandwriteKana, InputMode.Handwrite, true)]
    [InlineData(QuestionKind.Mcq, InputMode.Choice, true)]
    [InlineData(QuestionKind.Mcq, InputMode.Type, false)]
    [InlineData(QuestionKind.TypeAnswer, InputMode.Type, true)]
    public void BR_DRILL_02_kinds_in_the_pool_depend_on_the_input_mode(QuestionKind kind, InputMode mode, bool expected)
    {
        QuestionRules.IsDrillable(kind, mode).ShouldBe(expected);
    }

    [Fact]
    public void BR_SOAL_05_generator_makes_kana_questions_without_duplicates_and_with_distractors()
    {
        var level = Guid.NewGuid();
        var chapter1 = Guid.NewGuid();
        var chapter2 = Guid.NewGuid();
        KanaItem Kana(string character, string romaji, string row, Guid chapter) =>
            new(Guid.NewGuid(), level, chapter, "Tabel kana", "public-domain", character, "hiragana", romaji, row);
        var kana = new[]
        {
            Kana("あ", "a", "a", chapter1), Kana("い", "i", "a", chapter1), Kana("う", "u", "a", chapter1),
            Kana("か", "ka", "ka", chapter2), Kana("き", "ki", "ka", chapter2),
        };
        var input = new GeneratorInput(kana, [], [], []);
        var templates = new HashSet<GeneratorTemplate> { GeneratorTemplate.KanaToRomaji, GeneratorTemplate.RomajiToKana, GeneratorTemplate.KanaTypeRomaji };
        var order = new Dictionary<Guid, int> { [chapter1] = 1, [chapter2] = 2 };

        var questions = QuestionGenerator.Generate(input, templates, new HashSet<Guid> { chapter1 }, order);

        questions.Count.ShouldBe(9); // 3 kana of chapter 1 × 3 templates
        questions.ShouldAllBe(question => question.ChapterId == chapter1 && question.Category == Category.Hiragana);
        questions.Select(question => (question.Template, question.ItemId)).ShouldBeUnique();
        questions.ShouldAllBe(question => question.Tags.Contains(QuestionGenerator.TagOf(question.Template)) && question.Tags.Contains("kana_row:a"));
        questions.ShouldAllBe(question => question.Source == "Tabel kana" && question.License == "public-domain");

        var mcq = questions.First(question => question.Template == GeneratorTemplate.KanaToRomaji && question.ItemId == kana[0].Id);
        var payload = (McqPayload)mcq.Payload;
        payload.JaDisplay.ShouldBe(JaDisplay.NoRomaji);
        payload.Choices.Count.ShouldBe(4);
        payload.Choices[payload.Correct[0]].Indonesian.ShouldBe("a");
        payload.Choices.Select(choice => choice.Indonesian).ShouldBeUnique();
        // Nearest chapter first: both other kana of chapter 1 are distractors.
        payload.Choices.Select(choice => choice.Indonesian).ShouldContain("i");
        payload.Choices.Select(choice => choice.Indonesian).ShouldContain("u");

        // Deterministic: the same data gives the same questions.
        var again = QuestionGenerator.Generate(input, templates, new HashSet<Guid> { chapter1 }, order);
        again.Select(question => Payloads.ToJson(question.Payload).GetRawText())
            .ShouldBe(questions.Select(question => Payloads.ToJson(question.Payload).GetRawText()));
    }

    [Fact]
    public void BR_SOAL_05_generated_prompts_contain_no_invented_japanese()
    {
        foreach (var template in Enum.GetValues<GeneratorTemplate>())
        {
            var prompt = QuestionGenerator.PromptOf(template);
            prompt.Indonesian.ShouldNotBeNullOrWhiteSpace();
            prompt.English.ShouldNotBeNullOrWhiteSpace();
            prompt.Japanese.ShouldBeNull();
        }
    }

    [Fact]
    public void BR_SOAL_05_sentence_tap_splits_at_word_boundaries()
    {
        var level = Guid.NewGuid();
        var chapter = Guid.NewGuid();
        var sentence = new SentenceItem(Guid.NewGuid(), level, chapter, "Tatoeba", "CC BY 2.0 FR",
            new TextBlock("Saya makan.", "I eat.", [new JaSegment("私", "わたし"), new JaSegment("は", null, "wa"), new JaSegment(" "), new JaSegment("食", "た"), new JaSegment("べます")]),
            ["grammar:wa"]);

        var question = QuestionGenerator.Generate(new GeneratorInput([], [], [], [sentence]), new HashSet<GeneratorTemplate> { GeneratorTemplate.SentenceTap }, new HashSet<Guid>(), new Dictionary<Guid, int>())
            .ShouldHaveSingleItem();

        var payload = (TapSentencePayload)question.Payload;
        payload.Tokens.Count.ShouldBe(2);
        payload.Answers.ShouldHaveSingleItem().ShouldBe([0, 1]);
        payload.Stimulus.Japanese.ShouldBeNull();
        question.Tags.ShouldContain("grammar:wa");
    }
}
