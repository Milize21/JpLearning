using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Infrastructure;

namespace Nomax.Modules.Practice.Features.Drill;

/// <param name="Fallback"><c>normal</c> when weak focus had too few practised items (BR-DRILL-04).</param>
public sealed record DrillBatchDto(IReadOnlyList<SafeQuestion> Questions, string? Fallback);

/// <summary>Picks the next questions of a session (BR-DRILL-03/04) and turns them into student versions (docs/03 §8.3).</summary>
internal sealed class DrillBatches(PracticeDbContext db, DrillPool pool, IOptions<DrillOptions> options, TimeProvider timeProvider)
{
    public async Task<(DrillBatchDto Batch, int PoolSize)> NextAsync(DrillSession session, int? count, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(count ?? options.Value.BatchSize, 1, 50);
        var scope = await pool.ScopeForAsync(session.StudentId, cancellationToken);
        var entries = await DrillPool.EntriesAsync(pool.Query(scope, session.Category, session.Config), cancellationToken);
        if (entries.Count == 0)
        {
            return (new DrillBatchDto([], null), 0);
        }

        IReadOnlyList<Guid> ids;
        string? fallback = null;
        var cursor = await db.DrillAnswers.CountAsync(answer => answer.SessionId == session.Id, cancellationToken);
        if (session.Config.Mode == DrillMode.WeakFocus && await WeakCandidatesAsync(session.StudentId, entries, cancellationToken) is { } candidates
            && candidates.Count >= options.Value.WeakFocusMinCandidates)
        {
            ids = DrillSelection.WeakFocusBatch(candidates, size, timeProvider.GetUtcNow(), Random.Shared);
        }
        else
        {
            fallback = session.Config.Mode == DrillMode.WeakFocus ? "normal" : null;
            ids = DrillSelection.NormalBatch([.. entries.Select(entry => entry.QuestionId)], session.Id, cursor, size);
        }

        var questions = await db.Questions.AsNoTracking().Where(question => ids.Contains(question.Id)).ToDictionaryAsync(question => question.Id, cancellationToken);
        var safe = ids.Where(questions.ContainsKey)
            .Select(id => SafeQuestions.For(questions[id], SafeQuestions.SeededRandom(session.Id, id)))
            .ToList();
        return (new DrillBatchDto(safe, fallback), entries.Count);
    }

    /// <summary>BR-DRILL-04 candidates: pool questions whose item the student already answered (attempts ≥ 1).</summary>
    private async Task<IReadOnlyList<WeakCandidate>> WeakCandidatesAsync(Guid studentId, IReadOnlyList<PoolEntry> entries, CancellationToken cancellationToken)
    {
        var itemIds = entries.Select(entry => entry.ItemId).Distinct().ToList();
        var masteries = await db.ItemMasteries.AsNoTracking()
            .Where(mastery => mastery.StudentId == studentId && mastery.Attempts >= 1 && itemIds.Contains(mastery.ItemId))
            .Select(mastery => new { mastery.ItemType, mastery.ItemId, mastery.Mastery, mastery.LastSeenAt })
            .ToListAsync(cancellationToken);
        var byKey = masteries.ToDictionary(mastery => (mastery.ItemType, mastery.ItemId));
        return
        [
            .. entries
                .Where(entry => byKey.ContainsKey((entry.ItemType, entry.ItemId)))
                .Select(entry =>
                {
                    var mastery = byKey[(entry.ItemType, entry.ItemId)];
                    return new WeakCandidate(entry.QuestionId, mastery.Mastery, mastery.LastSeenAt);
                }),
        ];
    }
}
