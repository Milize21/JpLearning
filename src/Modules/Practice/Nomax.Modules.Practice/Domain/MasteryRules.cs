using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Practice.Domain;

/// <summary>Counters of one item for one student (practice.item_masteries).</summary>
public sealed record MasteryState(int Attempts, int Correct, int Streak, decimal Mastery)
{
    public static MasteryState New { get; } = new(0, 0, 0, 0m);
}

/// <summary>Thresholds of docs/04 §12 (<c>Practice:Mastery:*</c>).</summary>
public sealed record MasterySettings(decimal Alpha, decimal WeakThreshold, int WeakMinAttempts, decimal MasteredThreshold, int MasteredMinStreak)
{
    public static MasterySettings Default { get; } = new(0.3m, 0.6m, 3, 0.85m, 3);
}

/// <summary>BR-MASTERY-01 and BR-MASTERY-02 as pure functions.</summary>
public static class MasteryRules
{
    /// <summary>
    /// BR-MASTERY-01: attempts and correct count up, the streak restarts on a wrong answer, and
    /// <c>mastery = round(mastery + α × (x − mastery), 3)</c> rounded away from zero.
    /// </summary>
    public static MasteryState Apply(MasteryState state, bool isCorrect, MasterySettings settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settings);
        var x = isCorrect ? 1m : 0m;
        var mastery = Math.Round(state.Mastery + (settings.Alpha * (x - state.Mastery)), 3, MidpointRounding.AwayFromZero);
        return new MasteryState(
            state.Attempts + 1,
            state.Correct + (isCorrect ? 1 : 0),
            isCorrect ? state.Streak + 1 : 0,
            Math.Clamp(mastery, 0m, 1m));
    }

    /// <summary>BR-MASTERY-02: not started, weak (enough data and low), mastered (high and a streak), else learning.</summary>
    public static MasteryStatus StatusOf(int attempts, decimal mastery, int streak, MasterySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (attempts == 0)
        {
            return MasteryStatus.NotStarted;
        }

        if (attempts >= settings.WeakMinAttempts && mastery < settings.WeakThreshold)
        {
            return MasteryStatus.Weak;
        }

        return mastery >= settings.MasteredThreshold && streak >= settings.MasteredMinStreak
            ? MasteryStatus.Mastered
            : MasteryStatus.Learning;
    }

    public static MasteryStatus StatusOf(MasteryState state, MasterySettings settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        return StatusOf(state.Attempts, state.Mastery, state.Streak, settings);
    }
}

/// <summary>One item row as the weakness aggregation reads it.</summary>
public sealed record MasteryRow(
    MasteryItemType ItemType,
    Guid ItemId,
    int Attempts,
    int Correct,
    int Streak,
    decimal Mastery,
    Guid ChapterId,
    Category Category,
    IReadOnlyList<string> Tags,
    DateTimeOffset LastSeenAt);

/// <summary>Kind of weakness group (BR-MASTERY-03).</summary>
public enum WeaknessGroupKind
{
    Category,
    Tag,
    Chapter,
}

/// <param name="Key">Category value, tag text or chapter id.</param>
public sealed record WeaknessGroup(WeaknessGroupKind Kind, string Key, int ItemCount, decimal AverageMastery, decimal Accuracy, int WeakCount);

public sealed record WeaknessReport(IReadOnlyList<WeaknessGroup> Groups, IReadOnlyList<MasteryRow> WeakestItems);

/// <summary>BR-MASTERY-03: weakness groups per category, tag and chapter, from items with at least one attempt.</summary>
public static class WeaknessAggregation
{
    public const int TopGroups = 5;
    public const int WeakestItems = 10;

    public static WeaknessReport Aggregate(IEnumerable<MasteryRow> rows, MasterySettings settings, int topGroups = TopGroups, int weakestItems = WeakestItems)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(settings);
        var tried = rows.Where(row => row.Attempts >= 1).ToList();

        var memberships = tried.SelectMany(row => GroupsOf(row).Select(group => (group.Kind, group.Key, Row: row)));
        var groups = memberships
            .GroupBy(entry => (entry.Kind, entry.Key))
            .Select(group =>
            {
                var items = group.Select(entry => entry.Row).ToList();
                var attempts = items.Sum(item => item.Attempts);
                return new WeaknessGroup(
                    group.Key.Kind,
                    group.Key.Key,
                    items.Count,
                    Math.Round(items.Average(item => item.Mastery), 3, MidpointRounding.AwayFromZero),
                    attempts == 0 ? 0m : Math.Round((decimal)items.Sum(item => item.Correct) / attempts, 3, MidpointRounding.AwayFromZero),
                    items.Count(item => MasteryRules.StatusOf(item.Attempts, item.Mastery, item.Streak, settings) == MasteryStatus.Weak));
            })
            .OrderBy(group => group.AverageMastery)
            .ThenByDescending(group => group.WeakCount)
            .ThenBy(group => group.Kind)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(topGroups)
            .ToList();

        var weakest = tried
            .OrderBy(row => row.Mastery)
            .ThenByDescending(row => row.Attempts)
            .ThenBy(row => row.ItemId)
            .Take(weakestItems)
            .ToList();

        return new WeaknessReport(groups, weakest);
    }

    private static IEnumerable<(WeaknessGroupKind Kind, string Key)> GroupsOf(MasteryRow row)
    {
        yield return (WeaknessGroupKind.Category, EnumSnakeCase.ToValue(row.Category));
        foreach (var tag in row.Tags.Where(tag => !QuestionRules.IsInternalTag(tag)).Distinct(StringComparer.Ordinal))
        {
            yield return (WeaknessGroupKind.Tag, tag);
        }

        yield return (WeaknessGroupKind.Chapter, row.ChapterId.ToString());
    }
}
