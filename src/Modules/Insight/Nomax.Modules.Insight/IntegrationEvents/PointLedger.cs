using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.Modules.Practice.Contracts;
using Nomax.SharedKernel.Identifiers;

namespace Nomax.Modules.Insight.IntegrationEvents;

/// <summary>
/// Turns a correct drilling answer or a graded quiz into one <c>point_events</c> row (BR-POIN-02…10) in the active season of
/// the source level (BR-POIN-09). The counts the rules need come from earlier point events only, never from Practice.
/// </summary>
internal sealed class PointLedger(InsightDbContext db, SeasonService seasons, IOptions<LeaderboardOptions> options, TimeProvider clock)
{
    public const string SelfCheck = "self_check";
    public const string Exam = "exam";

    private readonly PointSettings _settings = options.Value.ToSettings();
    private readonly TimeZoneInfo _dayZone = options.Value.TimeZone();

    public async Task<PointEvent?> AwardDrillAsync(AnswerSubmitted answer, CancellationToken cancellationToken)
    {
        if (!answer.IsCorrect || await seasons.ActiveAsync(answer.LevelId, cancellationToken) is not { } season)
        {
            return null;
        }

        var itemKey = Truncate($"{answer.ItemType}:{answer.ItemId}");
        var (start, end) = PointRules.DayOf(answer.AnsweredAt, _dayZone);
        var today = db.PointEvents.Where(item =>
            item.StudentId == answer.StudentId && item.SourceType == PointSource.Drill && item.Ts >= start && item.Ts < end);
        var sameItem = await today.CountAsync(item => item.ItemKey == itemKey, cancellationToken);
        var drillPoints = await today.SumAsync(item => item.Points, cancellationToken);

        var award = PointRules.Drill(answer.InputMode == SelfCheck, sameItem, drillPoints, _settings);
        var awarded = PointEvent.Create(
            Uuid7.New(clock), answer.StudentId, answer.LevelId, season.Id, PointSource.Drill, answer.AnswerId, award, itemKey, answer.AnsweredAt);
        db.PointEvents.Add(awarded);
        return awarded;
    }

    public async Task<PointEvent?> AwardQuizAsync(QuizGraded graded, CancellationToken cancellationToken)
    {
        if (await seasons.ActiveAsync(graded.LevelId, cancellationToken) is not { } season)
        {
            return null;
        }

        var itemKey = $"quiz:{graded.QuizId}";
        var earlier = db.PointEvents.Where(item => item.StudentId == graded.StudentId && item.ItemKey == itemKey);
        var isFirst = !await earlier.AnyAsync(cancellationToken);
        var given = isFirst ? 0m : await earlier.SumAsync(item => item.Points, cancellationToken);
        var isExam = graded.QuizKind == Exam;

        var award = PointRules.Quiz(graded.ScorePct, isExam, given, isFirst, _settings);
        var awarded = PointEvent.Create(
            Uuid7.New(clock), graded.StudentId, graded.LevelId, season.Id, isExam ? PointSource.Exam : PointSource.Quiz,
            graded.AttemptId, award, itemKey, graded.GradedAt);
        db.PointEvents.Add(awarded);
        return awarded;
    }

    private static string Truncate(string value) => value.Length <= PointEvent.ItemKeyMaxLength ? value : value[..PointEvent.ItemKeyMaxLength];
}
