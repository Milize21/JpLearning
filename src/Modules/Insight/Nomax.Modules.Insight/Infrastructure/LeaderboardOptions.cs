using System.ComponentModel.DataAnnotations;
using Nomax.Modules.Insight.Domain;

namespace Nomax.Modules.Insight.Infrastructure;

/// <summary>Configuration section <c>Leaderboard</c> with the defaults of docs/04 §12.</summary>
public sealed class LeaderboardOptions
{
    public const string SectionName = "Leaderboard";

    /// <summary>BR-POIN-02.</summary>
    [Range(0.1, 100)]
    public decimal DrillPointPerCorrect { get; set; } = PointSettings.Default.DrillPointPerCorrect;

    /// <summary>BR-POIN-03.</summary>
    [Range(1, 100)]
    public int RepeatThreshold { get; set; } = PointSettings.Default.RepeatThreshold;

    [Range(0, 1)]
    public decimal RepeatMultiplier { get; set; } = PointSettings.Default.RepeatMultiplier;

    /// <summary>BR-POIN-04.</summary>
    [Range(1, 100000)]
    public decimal DailyDrillCap { get; set; } = PointSettings.Default.DailyDrillCap;

    /// <summary>BR-POIN-05 (D-17).</summary>
    [Required]
    public string DayTimeZone { get; set; } = "Asia/Jakarta";

    /// <summary>BR-POIN-08.</summary>
    [Range(0, 100)]
    public decimal QuizFactor { get; set; } = PointSettings.Default.QuizFactor;

    [Range(0, 100)]
    public decimal ExamFactor { get; set; } = PointSettings.Default.ExamFactor;

    /// <summary>BR-SEASON-04.</summary>
    [Range(1, 100)]
    public int TopN { get; set; } = 20;

    [Range(100, 60000)]
    public int BroadcastIntervalMs { get; set; } = 1000;

    /// <summary>D-40: key of the <c>entryKey</c> HMAC. Empty in development means a random key per process.</summary>
    public string? EntryKeySecret { get; set; }

    public PointSettings ToSettings() => new(DrillPointPerCorrect, RepeatThreshold, RepeatMultiplier, DailyDrillCap, QuizFactor, ExamFactor);

    public TimeZoneInfo TimeZone() => TimeZoneInfo.FindSystemTimeZoneById(DayTimeZone);
}
