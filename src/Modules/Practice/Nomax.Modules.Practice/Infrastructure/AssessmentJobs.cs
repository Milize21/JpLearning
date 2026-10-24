using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Grades;
using Nomax.Modules.Practice.Features.Quizzes;

namespace Nomax.Modules.Practice.Infrastructure;

/// <summary>
/// Job of docs/02 §11 (BR-KUIS-03): every minute, attempts past <c>expires_at</c> plus the grace period are submitted with
/// the answers saved so far.
/// </summary>
internal sealed partial class QuizAttemptExpirer(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<QuizAttemptExpirer> logger)
    : BackgroundService
{
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
        var grading = scope.ServiceProvider.GetRequiredService<AttemptGrading>();
        var grace = TimeSpan.FromSeconds(scope.ServiceProvider.GetRequiredService<IOptions<QuizOptions>>().Value.GraceSeconds);
        var cutoff = timeProvider.GetUtcNow() - grace;
        var expired = await db.QuizAttempts
            .Where(attempt => attempt.Status == AttemptStatus.InProgress && attempt.ExpiresAt != null && attempt.ExpiresAt < cutoff)
            .OrderBy(attempt => attempt.ExpiresAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        foreach (var attempt in expired)
        {
            await grading.SubmitAsync(attempt, cancellationToken);
        }

        return expired.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Submitting expired quiz attempts failed; it runs again in a minute")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}

/// <summary>
/// Job of docs/02 §11 (BR-NILAI-05): every hour, classes whose quizzes closed or assignments fell due since the last run
/// get their grades recomputed (missing work then counts as 0).
/// </summary>
internal sealed partial class GradeRecomputer(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<GradeRecomputer> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(timeProvider.GetUtcNow() - Interval - TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(logger, exception);
            }
        }
    }

    internal async Task<int> RunOnceAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PracticeDbContext>();
        var grades = scope.ServiceProvider.GetRequiredService<GradeService>();
        var now = timeProvider.GetUtcNow();
        var classIds = await db.Quizzes.AsNoTracking()
            .Where(quiz => quiz.Status == PublishStatus.Published && quiz.CloseAt != null && quiz.CloseAt > since && quiz.CloseAt <= now)
            .Select(quiz => quiz.ClassId)
            .Union(db.Assignments.AsNoTracking()
                .Where(item => item.Status == PublishStatus.Published && item.DueAt > since && item.DueAt <= now)
                .Select(item => item.ClassId))
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var classId in classIds)
        {
            await grades.RecomputeAsync(classId, null, cancellationToken);
        }

        return classIds.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recomputing class grades failed; it runs again in an hour")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
