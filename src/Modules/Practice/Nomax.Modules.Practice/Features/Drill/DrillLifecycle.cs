using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Messaging;
using Nomax.Modules.Practice.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Infrastructure;

namespace Nomax.Modules.Practice.Features.Drill;

/// <summary>BR-DRILL-08: sessions end by request or after 30 idle minutes, at the time of their last answer.</summary>
internal sealed class DrillLifecycle(PracticeDbContext db, IOptions<DrillOptions> options, TimeProvider timeProvider)
{
    /// <summary>Last activity of a session: its newest answer, else its start.</summary>
    public async Task<DateTimeOffset> LastActivityAsync(DrillSession session, CancellationToken cancellationToken) =>
        await db.DrillAnswers.Where(answer => answer.SessionId == session.Id).MaxAsync(answer => (DateTimeOffset?)answer.CreatedAt, cancellationToken)
        ?? session.StartedAt;

    /// <summary>Ends the session when it is idle; true when it is (now) ended. The caller saves.</summary>
    public async Task<bool> EndIfIdleAsync(DrillSession session, CancellationToken cancellationToken)
    {
        if (session.EndedAt is not null)
        {
            return true;
        }

        var last = await LastActivityAsync(session, cancellationToken);
        if (timeProvider.GetUtcNow() - last < TimeSpan.FromMinutes(options.Value.IdleMinutes))
        {
            return false;
        }

        await EndAsync(session, last, cancellationToken);
        return true;
    }

    /// <summary>Ends the session and queues <c>DrillSessionEnded</c>; the caller saves.</summary>
    public async Task EndAsync(DrillSession session, DateTimeOffset endedAt, CancellationToken cancellationToken)
    {
        if (session.EndedAt is not null)
        {
            return;
        }

        session.End(endedAt);
        var counts = await db.DrillAnswers.Where(answer => answer.SessionId == session.Id)
            .GroupBy(_ => 1)
            .Select(group => new { Total = group.Count(), Correct = group.Count(answer => answer.IsCorrect) })
            .FirstOrDefaultAsync(cancellationToken);
        db.AddIntegrationEvent(
            new DrillSessionEnded(session.Id, session.StudentId, counts?.Total ?? 0, counts?.Correct ?? 0, session.StartedAt, endedAt),
            timeProvider);
    }
}
