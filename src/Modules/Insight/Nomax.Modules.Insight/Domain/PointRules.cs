namespace Nomax.Modules.Insight.Domain;

/// <summary>Points of one source and the rule that decided them (<c>point_events.rule</c>).</summary>
public sealed record PointAward(decimal Points, string Rule)
{
    public const string DrillBase = "drill_base";
    public const string DrillRepeatHalf = "drill_repeat_half";
    public const string DrillDailyCap = "drill_daily_cap";
    public const string SelfCheckZero = "self_check_zero";
    public const string QuizScore = "quiz_score";
    public const string QuizImprovement = "quiz_improvement";
}

/// <summary>Settings of docs/04 §12 (<c>Leaderboard:*</c>).</summary>
public sealed record PointSettings(decimal DrillPointPerCorrect, int RepeatThreshold, decimal RepeatMultiplier, decimal DailyDrillCap, decimal QuizFactor, decimal ExamFactor)
{
    public static PointSettings Default { get; } = new(1m, 3, 0.5m, 200m, 1.0m, 2.0m);
}

/// <summary>BR-POIN-02…08 as pure functions.</summary>
public static class PointRules
{
    /// <summary>
    /// BR-POIN-02/03/04/07 for one correct drilling answer. <paramref name="earlierCorrectSameItemToday"/> counts the
    /// student's earlier correct answers of this item on the same day; <paramref name="drillPointsToday"/> the drilling
    /// points already given that day.
    /// </summary>
    public static PointAward Drill(bool selfCheck, int earlierCorrectSameItemToday, decimal drillPointsToday, PointSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (selfCheck)
        {
            return new PointAward(0m, PointAward.SelfCheckZero);
        }

        var repeated = earlierCorrectSameItemToday >= settings.RepeatThreshold;
        var computed = repeated ? settings.DrillPointPerCorrect * settings.RepeatMultiplier : settings.DrillPointPerCorrect;
        var room = Math.Max(0m, settings.DailyDrillCap - drillPointsToday);
        if (computed > room)
        {
            return new PointAward(room, PointAward.DrillDailyCap);
        }

        return new PointAward(computed, repeated ? PointAward.DrillRepeatHalf : PointAward.DrillBase);
    }

    /// <summary>
    /// BR-POIN-08: <c>round(score × factor, 1)</c>; a later attempt of the same quiz adds only the improvement over the
    /// points already given for it (<paramref name="pointsGivenForQuiz"/>).
    /// </summary>
    public static PointAward Quiz(decimal scorePct, bool isExam, decimal pointsGivenForQuiz, bool isFirstAttempt, PointSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var points = Math.Round(scorePct * (isExam ? settings.ExamFactor : settings.QuizFactor), 1, MidpointRounding.AwayFromZero);
        return isFirstAttempt
            ? new PointAward(points, PointAward.QuizScore)
            : new PointAward(Math.Max(0m, points - pointsGivenForQuiz), PointAward.QuizImprovement);
    }

    /// <summary>BR-POIN-05: the calendar day of <paramref name="at"/> in <paramref name="timeZone"/>, as a UTC range.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) DayOf(DateTimeOffset at, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var local = TimeZoneInfo.ConvertTime(at, timeZone);
        var startLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(startLocal, timeZone.GetUtcOffset(startLocal));
        var endLocal = startLocal.AddDays(1);
        var end = new DateTimeOffset(endLocal, timeZone.GetUtcOffset(endLocal));
        return (start.ToUniversalTime(), end.ToUniversalTime());
    }
}

/// <summary>One row of a ranking.</summary>
public sealed record RankedEntry(Guid StudentId, string DisplayName, decimal Points, int Rank);

/// <summary>BR-SEASON-03: points descending, equal points share a rank (1, 2, 2, 4), ties listed by display name A–Z.</summary>
public static class Ranking
{
    public static IReadOnlyList<RankedEntry> Rank(IEnumerable<(Guid StudentId, string DisplayName, decimal Points)> entries)
    {
        var ordered = entries
            .OrderByDescending(entry => entry.Points)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.StudentId)
            .ToList();
        var result = new List<RankedEntry>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i > 0 && ordered[i].Points == ordered[i - 1].Points ? result[i - 1].Rank : i + 1;
            result.Add(new RankedEntry(ordered[i].StudentId, ordered[i].DisplayName, ordered[i].Points, rank));
        }

        return result;
    }

    /// <summary>"{level} · Musim {n}" (BR-SEASON-02).</summary>
    public static string SeasonName(string levelName, int number) => $"{levelName} · Musim {number}";
}
