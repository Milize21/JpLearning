using System.ComponentModel.DataAnnotations;
using Nomax.Modules.Practice.Domain;

namespace Nomax.Modules.Practice.Infrastructure;

/// <summary>Configuration section <c>Practice:Drill</c> with the defaults of docs/04 §12.</summary>
public sealed class DrillOptions
{
    public const string SectionName = "Practice:Drill";

    /// <summary>BR-DRILL-03.</summary>
    [Range(1, 50)]
    public int BatchSize { get; set; } = DrillSettings.Default.BatchSize;

    /// <summary>BR-DRILL-08.</summary>
    [Range(1, 1440)]
    public int IdleMinutes { get; set; } = DrillSettings.Default.IdleMinutes;

    /// <summary>BR-DRILL-04.</summary>
    [Range(1, 100)]
    public int WeakFocusMinCandidates { get; set; } = DrillSettings.Default.WeakFocusMinCandidates;

    public DrillSettings ToSettings() => new(BatchSize, IdleMinutes, WeakFocusMinCandidates);
}

/// <summary>Configuration section <c>Practice:Quiz</c> with the defaults of docs/04 §12.</summary>
public sealed class QuizOptions
{
    public const string SectionName = "Practice:Quiz";

    /// <summary>BR-KUIS-03 tolerance after <c>expires_at</c>.</summary>
    [Range(0, 600)]
    public int GraceSeconds { get; set; } = 10;
}

/// <summary>Configuration section <c>Practice:Mastery</c> with the defaults of docs/04 §12.</summary>
public sealed class MasteryOptions
{
    public const string SectionName = "Practice:Mastery";

    /// <summary>BR-MASTERY-01.</summary>
    [Range(typeof(decimal), "0.01", "1")]
    public decimal Alpha { get; set; } = MasterySettings.Default.Alpha;

    /// <summary>BR-MASTERY-02.</summary>
    [Range(typeof(decimal), "0", "1")]
    public decimal WeakThreshold { get; set; } = MasterySettings.Default.WeakThreshold;

    [Range(1, 1000)]
    public int WeakMinAttempts { get; set; } = MasterySettings.Default.WeakMinAttempts;

    [Range(typeof(decimal), "0", "1")]
    public decimal MasteredThreshold { get; set; } = MasterySettings.Default.MasteredThreshold;

    [Range(1, 1000)]
    public int MasteredMinStreak { get; set; } = MasterySettings.Default.MasteredMinStreak;

    public MasterySettings ToSettings() => new(Alpha, WeakThreshold, WeakMinAttempts, MasteredThreshold, MasteredMinStreak);
}
