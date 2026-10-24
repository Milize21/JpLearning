using Nomax.Modules.Insight.Domain;

namespace Nomax.Modules.Insight.Tests.Domain;

public sealed class PointRulesTests
{
    private static readonly PointSettings Settings = PointSettings.Default;

    private static readonly TimeZoneInfo Jakarta = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    [Fact]
    public void BR_POIN_case_1_fourth_and_later_correct_answers_of_an_item_count_half()
    {
        var points = new List<decimal>();
        decimal today = 0;
        for (var i = 0; i < 5; i++)
        {
            var award = PointRules.Drill(selfCheck: false, earlierCorrectSameItemToday: i, today, Settings);
            points.Add(award.Points);
            today += award.Points;
        }

        points.ShouldBe([1m, 1m, 1m, 0.5m, 0.5m]);
        points.Sum().ShouldBe(4.0m);
        PointRules.Drill(false, 3, 0m, Settings).Rule.ShouldBe(PointAward.DrillRepeatHalf);
    }

    [Fact]
    public void BR_POIN_case_3_the_daily_cap_gives_the_remainder_then_zero()
    {
        var capped = PointRules.Drill(false, 0, 199.5m, Settings);
        capped.ShouldBe(new PointAward(0.5m, PointAward.DrillDailyCap));
        PointRules.Drill(false, 0, 200m, Settings).ShouldBe(new PointAward(0m, PointAward.DrillDailyCap));
    }

    [Fact]
    public void BR_POIN_case_4_a_new_day_in_Jakarta_resets_the_repeat_count()
    {
        // 23:59 WIB on 22 Oct and 00:01 WIB on 23 Oct fall in different days.
        var lateEvening = new DateTimeOffset(2026, 10, 22, 16, 59, 0, TimeSpan.Zero);
        var afterMidnight = new DateTimeOffset(2026, 10, 22, 17, 1, 0, TimeSpan.Zero);
        var first = PointRules.DayOf(lateEvening, Jakarta);
        var second = PointRules.DayOf(afterMidnight, Jakarta);

        first.ShouldNotBe(second);
        first.Start.ShouldBe(new DateTimeOffset(2026, 10, 21, 17, 0, 0, TimeSpan.Zero));
        first.End.ShouldBe(second.Start);
    }

    [Fact]
    public void BR_POIN_case_5_self_checks_earn_nothing()
    {
        PointRules.Drill(selfCheck: true, 0, 0m, Settings).ShouldBe(new PointAward(0m, PointAward.SelfCheckZero));
    }

    [Fact]
    public void BR_POIN_cases_6_to_9_quizzes_count_the_score_then_only_improvements()
    {
        PointRules.Quiz(85m, isExam: false, pointsGivenForQuiz: 0m, isFirstAttempt: true, Settings).ShouldBe(new PointAward(85.0m, PointAward.QuizScore));
        PointRules.Quiz(92m, false, 85m, false, Settings).ShouldBe(new PointAward(7.0m, PointAward.QuizImprovement));
        PointRules.Quiz(80m, false, 92m, false, Settings).ShouldBe(new PointAward(0m, PointAward.QuizImprovement));
        PointRules.Quiz(70m, isExam: true, 0m, true, Settings).ShouldBe(new PointAward(140.0m, PointAward.QuizScore));
        PointRules.Quiz(66.67m, false, 0m, true, Settings).Points.ShouldBe(66.7m);
    }

    [Fact]
    public void BR_SEASON_03_competition_ranking_with_ties_by_name()
    {
        var ranked = Ranking.Rank(
        [
            (Guid.NewGuid(), "Dina", 90m),
            (Guid.NewGuid(), "Budi", 100m),
            (Guid.NewGuid(), "Ani", 120m),
            (Guid.NewGuid(), "Citra", 100m),
        ]);

        ranked.Select(entry => entry.Rank).ShouldBe([1, 2, 2, 4]);
        ranked.Select(entry => entry.DisplayName).ShouldBe(["Ani", "Budi", "Citra", "Dina"]);
    }

    [Fact]
    public void BR_SEASON_02_new_season_names_count_up()
    {
        Ranking.SeasonName("N5", 2).ShouldBe("N5 · Musim 2");
    }
}
