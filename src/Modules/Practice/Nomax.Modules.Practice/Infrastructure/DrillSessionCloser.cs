using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomax.Modules.Practice.Features.Drill;

namespace Nomax.Modules.Practice.Infrastructure;

/// <summary>
/// Job of docs/02 §11 (BR-DRILL-08): every minute, sessions idle for <see cref="DrillOptions.IdleMinutes"/> end at the time of
/// their last answer and publish <c>DrillSessionEnded</c>.
/// </summary>
internal sealed partial class DrillSessionCloser(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<DrillSessionCloser> logger)
    : BackgroundService
{
    private const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), timeProvider);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PracticeDbContext>();
        var lifecycle = scope.ServiceProvider.GetRequiredService<DrillLifecycle>();
        var idle = TimeSpan.FromMinutes(scope.ServiceProvider.GetRequiredService<IOptions<DrillOptions>>().Value.IdleMinutes);
        var cutoff = timeProvider.GetUtcNow() - idle;

        var sessions = await db.DrillSessions
            .Where(session => session.EndedAt == null && session.StartedAt < cutoff)
            .Where(session => !db.DrillAnswers.Any(answer => answer.SessionId == session.Id && answer.CreatedAt >= cutoff))
            .OrderBy(session => session.StartedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            await lifecycle.EndAsync(session, await lifecycle.LastActivityAsync(session, cancellationToken), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Closing idle drill sessions failed; it runs again in a minute")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
