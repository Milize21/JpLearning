using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Messaging;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.Modules.Insight.Leaderboard;
using Nomax.Modules.Practice.Contracts;
using Nomax.SharedKernel.Events;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Insight.IntegrationEvents;

// Insight consumes the events of docs/05 §8: points (BR-POIN) and the activity log (docs/05 §11). Each consumer is
// idempotent through the inbox; Redis and hub pushes happen only after the commit.

/// <summary>Activity log types of docs/05 §11.</summary>
internal static class ActivityTypes
{
    public const string UserCreated = "user_created";
    public const string UserDeactivated = "user_deactivated";
    public const string UserReactivated = "user_reactivated";
    public const string ChapterUnlocked = "chapter_unlocked";
    public const string ChapterLocked = "chapter_locked";
    public const string DrillSessionEnded = "drill_session_ended";
    public const string QuizGraded = "quiz_graded";
    public const string SubmissionGraded = "submission_graded";
    public const string SeasonClosed = "season_closed";

    /// <summary>D-41: what counts as a student being active ("terakhir aktif").</summary>
    public static readonly string[] StudentActivity = [DrillSessionEnded, QuizGraded];
}

internal abstract class ActivityConsumer<TEvent>(InsightDbContext db, TimeProvider clock)
    : InboxConsumer<InsightDbContext, TEvent>(db, clock)
    where TEvent : IIntegrationEvent
{
    protected void Log(Guid userId, string type, string? reference, object meta, DateTimeOffset ts) =>
        Db.ActivityLogs.Add(ActivityLog.Create(
            Uuid7.New(Clock), userId, type, reference, JsonSerializer.SerializeToElement(meta, NomaxJson.Options), ts));

    protected Task<List<Guid>> ActiveSeasonIdsAsync(CancellationToken cancellationToken) =>
        Db.Seasons.Where(season => season.Status == SeasonStatus.Active).Select(season => season.Id).ToListAsync(cancellationToken);
}

/// <summary>BR-POIN-02…07: drilling points. Wrong answers produce nothing.</summary>
internal sealed class AnswerSubmittedConsumer(InsightDbContext db, TimeProvider clock, PointLedger ledger, LeaderboardNotifier notifier)
    : InboxConsumer<InsightDbContext, AnswerSubmitted>(db, clock)
{
    private PointEvent? _awarded;

    protected override async Task ApplyAsync(IntegrationEventEnvelope<AnswerSubmitted> envelope, CancellationToken cancellationToken) =>
        _awarded = await ledger.AwardDrillAsync(envelope.Payload, cancellationToken);

    protected override Task AfterCommitAsync(IntegrationEventEnvelope<AnswerSubmitted> envelope, CancellationToken cancellationToken) =>
        _awarded is null ? Task.CompletedTask : notifier.PointsAwardedAsync(_awarded, cancellationToken);
}

/// <summary>BR-POIN-08: quiz and exam points, and the <c>quiz_graded</c> log.</summary>
internal sealed class QuizGradedConsumer(InsightDbContext db, TimeProvider clock, PointLedger ledger, LeaderboardNotifier notifier)
    : ActivityConsumer<QuizGraded>(db, clock)
{
    private PointEvent? _awarded;

    protected override async Task ApplyAsync(IntegrationEventEnvelope<QuizGraded> envelope, CancellationToken cancellationToken)
    {
        var graded = envelope.Payload;
        Log(graded.StudentId, ActivityTypes.QuizGraded, $"quiz_attempt:{graded.AttemptId}",
            new { graded.QuizId, graded.QuizKind, graded.ClassId, graded.ScorePct, graded.AttemptNo }, graded.GradedAt);
        _awarded = await ledger.AwardQuizAsync(graded, cancellationToken);
    }

    protected override Task AfterCommitAsync(IntegrationEventEnvelope<QuizGraded> envelope, CancellationToken cancellationToken) =>
        _awarded is null ? Task.CompletedTask : notifier.PointsAwardedAsync(_awarded, cancellationToken);
}

internal sealed class DrillSessionEndedConsumer(InsightDbContext db, TimeProvider clock) : ActivityConsumer<DrillSessionEnded>(db, clock)
{
    protected override Task ApplyAsync(IntegrationEventEnvelope<DrillSessionEnded> envelope, CancellationToken cancellationToken)
    {
        var ended = envelope.Payload;
        Log(ended.StudentId, ActivityTypes.DrillSessionEnded, $"drill_session:{ended.SessionId}",
            new { ended.AnswerCount, ended.CorrectCount, ended.StartedAt }, ended.EndedAt);
        return Task.CompletedTask;
    }
}

internal sealed class SubmissionGradedConsumer(InsightDbContext db, TimeProvider clock) : ActivityConsumer<SubmissionGraded>(db, clock)
{
    protected override Task ApplyAsync(IntegrationEventEnvelope<SubmissionGraded> envelope, CancellationToken cancellationToken)
    {
        var graded = envelope.Payload;
        Log(graded.StudentId, ActivityTypes.SubmissionGraded, $"submission:{graded.SubmissionId}",
            new { graded.AssignmentId, graded.ClassId, graded.GradePct, graded.GradedBy }, graded.GradedAt);
        return Task.CompletedTask;
    }
}

internal sealed class ChapterUnlockedConsumer(InsightDbContext db, TimeProvider clock) : ActivityConsumer<ChapterUnlocked>(db, clock)
{
    protected override Task ApplyAsync(IntegrationEventEnvelope<ChapterUnlocked> envelope, CancellationToken cancellationToken)
    {
        var unlocked = envelope.Payload;
        Log(unlocked.UnlockedBy, ActivityTypes.ChapterUnlocked, $"chapter:{unlocked.ChapterId}", new { unlocked.ClassId }, unlocked.UnlockedAt);
        return Task.CompletedTask;
    }
}

internal sealed class ChapterLockedConsumer(InsightDbContext db, TimeProvider clock) : ActivityConsumer<ChapterLocked>(db, clock)
{
    protected override Task ApplyAsync(IntegrationEventEnvelope<ChapterLocked> envelope, CancellationToken cancellationToken)
    {
        var locked = envelope.Payload;
        Log(locked.LockedBy, ActivityTypes.ChapterLocked, $"chapter:{locked.ChapterId}", new { locked.ClassId }, locked.LockedAt);
        return Task.CompletedTask;
    }
}

internal sealed class UserAccountCreatedConsumer(InsightDbContext db, TimeProvider clock) : ActivityConsumer<UserAccountCreated>(db, clock)
{
    protected override Task ApplyAsync(IntegrationEventEnvelope<UserAccountCreated> envelope, CancellationToken cancellationToken)
    {
        var created = envelope.Payload;
        Log(created.UserId, ActivityTypes.UserCreated, null, new { created.Role, created.CreatedBy }, envelope.OccurredAt);
        return Task.CompletedTask;
    }
}

/// <summary>BR-SEASON-05: hidden from the live boards.</summary>
internal sealed class UserDeactivatedConsumer(InsightDbContext db, TimeProvider clock, LeaderboardNotifier notifier)
    : ActivityConsumer<UserDeactivated>(db, clock)
{
    private List<Guid> _seasons = [];

    protected override async Task ApplyAsync(IntegrationEventEnvelope<UserDeactivated> envelope, CancellationToken cancellationToken)
    {
        Log(envelope.Payload.UserId, ActivityTypes.UserDeactivated, null, new { }, envelope.OccurredAt);
        _seasons = await ActiveSeasonIdsAsync(cancellationToken);
    }

    protected override Task AfterCommitAsync(IntegrationEventEnvelope<UserDeactivated> envelope, CancellationToken cancellationToken) =>
        notifier.UserChangedAsync(envelope.Payload.UserId, _seasons, UserChange.Deactivated);
}

/// <summary>BR-SEASON-05: back on the boards, recomputed from <c>point_events</c>.</summary>
internal sealed class UserReactivatedConsumer(InsightDbContext db, TimeProvider clock, LeaderboardNotifier notifier)
    : ActivityConsumer<UserReactivated>(db, clock)
{
    private List<Guid> _seasons = [];

    protected override async Task ApplyAsync(IntegrationEventEnvelope<UserReactivated> envelope, CancellationToken cancellationToken)
    {
        Log(envelope.Payload.UserId, ActivityTypes.UserReactivated, null, new { }, envelope.OccurredAt);
        _seasons = await ActiveSeasonIdsAsync(cancellationToken);
    }

    protected override Task AfterCommitAsync(IntegrationEventEnvelope<UserReactivated> envelope, CancellationToken cancellationToken) =>
        notifier.UserChangedAsync(envelope.Payload.UserId, _seasons, UserChange.Reactivated);
}

/// <summary>Keeps the cached display names of the boards current.</summary>
internal sealed class UserDisplayNameChangedConsumer(InsightDbContext db, TimeProvider clock, LeaderboardNotifier notifier)
    : ActivityConsumer<UserDisplayNameChanged>(db, clock)
{
    private List<Guid> _seasons = [];

    protected override async Task ApplyAsync(IntegrationEventEnvelope<UserDisplayNameChanged> envelope, CancellationToken cancellationToken) =>
        _seasons = await ActiveSeasonIdsAsync(cancellationToken);

    protected override Task AfterCommitAsync(IntegrationEventEnvelope<UserDisplayNameChanged> envelope, CancellationToken cancellationToken) =>
        notifier.UserChangedAsync(envelope.Payload.UserId, _seasons, UserChange.Renamed, envelope.Payload.DisplayName);
}
