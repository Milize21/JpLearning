using System.Text.Json;
using Nomax.Modules.Practice.Domain;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Tests.Domain;

public sealed class DrillSelectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BR_DRILL_03_normal_mode_has_no_repeats_until_the_pool_is_used_up()
    {
        var pool = Enumerable.Range(0, 23).Select(_ => Guid.NewGuid()).ToList();
        var session = Guid.NewGuid();

        var firstCycle = new List<Guid>();
        for (var cursor = 0; cursor < pool.Count; cursor += 10)
        {
            firstCycle.AddRange(DrillSelection.NormalBatch(pool, session, cursor, Math.Min(10, pool.Count - cursor)));
        }

        firstCycle.Count.ShouldBe(pool.Count);
        firstCycle.ShouldBeUnique();
        firstCycle.ShouldBe(pool, ignoreOrder: true);
        firstCycle.ShouldNotBe(pool.Order().ToList()); // shuffled

        // The next cycle is shuffled anew, and one batch never repeats a question.
        var crossing = DrillSelection.NormalBatch(pool, session, 20, 10);
        crossing.ShouldBeUnique();
        crossing.Count.ShouldBe(10);
    }

    [Fact]
    public void BR_DRILL_03_the_order_is_stable_for_a_session()
    {
        var pool = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToList();
        var session = Guid.NewGuid();
        DrillSelection.NormalBatch(pool, session, 0, 10).ShouldBe(DrillSelection.NormalBatch([.. pool.AsEnumerable().Reverse()], session, 0, 10));
        DrillSelection.NormalBatch(pool, session, 0, 10).ShouldNotBe(DrillSelection.NormalBatch(pool, Guid.NewGuid(), 0, 10));
    }

    [Fact]
    public void BR_DRILL_04_weight_formula()
    {
        DrillSelection.Weight(new WeakCandidate(Guid.NewGuid(), 0.3m, Now.AddDays(-1)), Now).ShouldBe(0.8, 1e-9);
        DrillSelection.Weight(new WeakCandidate(Guid.NewGuid(), 0.3m, Now.AddDays(-8)), Now).ShouldBe(1.0, 1e-9);
        DrillSelection.Weight(new WeakCandidate(Guid.NewGuid(), 1m, Now), Now).ShouldBe(0.1, 1e-9);
    }

    [Fact]
    public void BR_DRILL_04_weak_focus_picks_low_mastery_items_more_often()
    {
        // Simple statistical test with a fixed seed: 5 weak (0.1) and 5 strong (0.95) items, 2,000 batches of 3.
        var weak = Enumerable.Range(0, 5).Select(_ => new WeakCandidate(Guid.NewGuid(), 0.1m, Now)).ToList();
        var strong = Enumerable.Range(0, 5).Select(_ => new WeakCandidate(Guid.NewGuid(), 0.95m, Now)).ToList();
        var weakIds = weak.Select(candidate => candidate.QuestionId).ToHashSet();
        var random = new Random(20261020);
        var weakPicks = 0;
        var total = 0;
        for (var i = 0; i < 2000; i++)
        {
            var batch = DrillSelection.WeakFocusBatch([.. weak, .. strong], 3, Now, random);
            batch.ShouldBeUnique();
            weakPicks += batch.Count(weakIds.Contains);
            total += batch.Count;
        }

        // Weights 1.0 vs 0.15: weak items should fill well over 70 % of the picks.
        ((double)weakPicks / total).ShouldBeGreaterThan(0.7);
    }

    [Fact]
    public void BR_DRILL_04_batches_never_exceed_the_candidates()
    {
        var candidates = new[] { new WeakCandidate(Guid.NewGuid(), 0.5m, Now), new WeakCandidate(Guid.NewGuid(), 0.5m, Now) };
        DrillSelection.WeakFocusBatch(candidates, 10, Now, new Random(1)).Count.ShouldBe(2);
    }

    [Fact]
    public void Student_version_never_contains_the_answer_key()
    {
        // docs/03 §8.3: no correct/answers/accepted/rubric; options shuffled with their original index as id.
        var payloads = new (QuestionKind Kind, object Payload)[]
        {
            (QuestionKind.Mcq, new McqPayload(new("x", null, null), [new("a", null, null), new("b", null, null), new("c", null, null)], [2], false, JaDisplay.Plain)),
            (QuestionKind.TapSentence, new TapSentencePayload(new("x", null, null), [new("a", null, null), new("b", null, null)], [[1, 0]], null)),
            (QuestionKind.TypeAnswer, new TypeAnswerPayload(new("x", null, null), ["secret"], AnswerScript.Romaji, null, null)),
            (QuestionKind.Match, new MatchPayload([new(new("a", null, null), new("1", null, null)), new(new("b", null, null), new("2", null, null)), new(new("c", null, null), new("3", null, null))], null)),
            (QuestionKind.LongAnswer, new LongAnswerPayload(new RichText("md", "secret rubric", null), 500)),
        };

        foreach (var (kind, payload) in payloads)
        {
            var question = Question.Create(
                Guid.NewGuid(), kind, Guid.NewGuid(), Guid.NewGuid(), Category.Vocab, [], new TextBlock("Prompt", null, null),
                Payloads.ToJson(payload), new RichText("md", "explanation", null), null, null, QuestionStatus.Published,
                QuestionScope.Builtin, Guid.NewGuid(), Guid.NewGuid());

            var json = JsonSerializer.Serialize(SafeQuestions.For(question, new Random(7)), NomaxJson.Options);

            json.ShouldNotContain("\"correct\"");
            json.ShouldNotContain("\"answers\"");
            json.ShouldNotContain("\"accepted\"");
            json.ShouldNotContain("secret");
            json.ShouldNotContain("explanation");
        }
    }

    [Fact]
    public void Student_version_shuffles_choices_but_keeps_their_ids()
    {
        var choices = Enumerable.Range(0, 6).Select(i => new TextBlock($"c{i}", null, null)).ToList();
        var question = Question.Create(
            Guid.NewGuid(), QuestionKind.Mcq, Guid.NewGuid(), Guid.NewGuid(), Category.Vocab, [], new TextBlock("Prompt", null, null),
            Payloads.ToJson(new McqPayload(null, choices, [0], false, null)), null, null, null, QuestionStatus.Published,
            QuestionScope.Builtin, Guid.NewGuid(), Guid.NewGuid());

        var safe = SafeQuestions.For(question, new Random(3));

        safe.Choices.ShouldNotBeNull();
        safe.Choices.Select(choice => choice.ChoiceId).ShouldBe([0, 1, 2, 3, 4, 5], ignoreOrder: true);
        safe.Choices.ShouldAllBe(choice => choice.Text.Indonesian == $"c{choice.ChoiceId}");
        safe.JaDisplay.ShouldBe(JaDisplay.UserPref);
    }
}
