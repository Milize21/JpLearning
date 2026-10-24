using Nomax.Modules.Practice.Domain;

namespace Nomax.Modules.Practice.Tests.Domain;

public sealed class MasteryRulesTests
{
    private static readonly MasterySettings Settings = MasterySettings.Default;

    [Fact]
    public void BR_MASTERY_01_the_documented_sequence_of_answers()
    {
        // docs/04 BR-MASTERY-01: ✓ ✓ ✗ ✓ ✓ ✓ ✓ starting from 0.
        bool[] answers = [true, true, false, true, true, true, true];
        decimal[] mastery = [0.300m, 0.510m, 0.357m, 0.550m, 0.685m, 0.780m, 0.846m];
        int[] streak = [1, 2, 0, 1, 2, 3, 4];

        var state = MasteryState.New;
        for (var i = 0; i < answers.Length; i++)
        {
            state = MasteryRules.Apply(state, answers[i], Settings);
            state.Mastery.ShouldBe(mastery[i], $"answer {i + 1}");
            state.Streak.ShouldBe(streak[i], $"answer {i + 1}");
        }

        state.Attempts.ShouldBe(7);
        state.Correct.ShouldBe(6);
    }

    [Fact]
    public void BR_MASTERY_01_rounds_half_away_from_zero_to_three_decimals()
    {
        // 0.685 + 0.3 × 0.315 = 0.7795 → 0.780 (banker's rounding would give 0.780 too, so check a .xxx5 that differs).
        var state = MasteryRules.Apply(new MasteryState(1, 1, 1, 0.125m), false, new MasterySettings(0.5m, 0.6m, 3, 0.85m, 3));
        state.Mastery.ShouldBe(0.063m); // 0.0625 → 0.063 away from zero (to-even would be 0.062)
    }

    [Fact]
    public void BR_MASTERY_01_stays_within_zero_and_one()
    {
        var state = new MasteryState(10, 10, 10, 1m);
        MasteryRules.Apply(state, true, Settings).Mastery.ShouldBe(1m);
        MasteryRules.Apply(new MasteryState(10, 0, 0, 0m), false, Settings).Mastery.ShouldBe(0m);
    }

    [Theory]
    [InlineData(2, "0.20", 0, MasteryStatus.Learning)]
    [InlineData(5, "0.55", 1, MasteryStatus.Weak)]
    [InlineData(6, "0.86", 3, MasteryStatus.Mastered)]
    [InlineData(6, "0.86", 2, MasteryStatus.Learning)]
    [InlineData(0, "0", 0, MasteryStatus.NotStarted)]
    public void BR_MASTERY_02_status_cases(int attempts, string mastery, int streak, MasteryStatus expected)
    {
        MasteryRules.StatusOf(attempts, decimal.Parse(mastery, System.Globalization.CultureInfo.InvariantCulture), streak, Settings).ShouldBe(expected);
    }

    [Fact]
    public void BR_MASTERY_03_groups_by_category_tag_and_chapter_weakest_first()
    {
        var chapterA = Guid.NewGuid();
        var chapterB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        MasteryRow Row(decimal mastery, int attempts, int correct, Guid chapter, string tag) =>
            new(MasteryItemType.Kana, Guid.NewGuid(), attempts, correct, 0, mastery, chapter, Category.Hiragana, [tag, "gen:kana_to_romaji"], now);

        var rows = new[]
        {
            Row(0.2m, 5, 1, chapterA, "kana_row:sa"),
            Row(0.3m, 4, 1, chapterA, "kana_row:sa"),
            Row(0.9m, 6, 6, chapterB, "kana_row:a"),
            new MasteryRow(MasteryItemType.Kana, Guid.NewGuid(), 0, 0, 0, 0m, chapterB, Category.Hiragana, ["kana_row:ka"], now),
        };

        var report = WeaknessAggregation.Aggregate(rows, Settings);

        // Items never answered are ignored; generator tags are not groups.
        report.Groups.ShouldNotContain(group => group.Key == "kana_row:ka");
        report.Groups.ShouldNotContain(group => group.Key.StartsWith("gen:", StringComparison.Ordinal));
        var first = report.Groups[0];
        first.AverageMastery.ShouldBe(0.25m);
        first.WeakCount.ShouldBe(2);
        first.ItemCount.ShouldBe(2);
        first.Accuracy.ShouldBe(0.222m); // 2 / 9
        report.Groups.Select(group => group.AverageMastery).ShouldBeInOrder(SortDirection.Ascending);
        report.Groups.Count.ShouldBeLessThanOrEqualTo(WeaknessAggregation.TopGroups);
        report.WeakestItems.Select(item => item.Mastery).ShouldBe([0.2m, 0.3m, 0.9m]);
    }
}
