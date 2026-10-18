using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Identity.Contracts;
using Nomax.SharedKernel.Identifiers;

namespace Nomax.Modules.Classroom.Infrastructure.Notifications;

/// <summary>
/// Queues the notifications of BR-JADWAL-05/06 for the active students of a class: one row per recipient and channel
/// (email when the profile has an email, web push always; the dispatcher skips users without a subscription). Rows are added
/// to the caller's DbContext and saved in the same transaction as the session change.
/// </summary>
internal sealed class NotificationPlanner(
    ClassroomDbContext db,
    IUserDirectory users,
    IOptions<ScheduleOptions> options,
    TimeProvider timeProvider)
{
    public async Task SessionCreatedAsync(ClassSession session, CancellationToken cancellationToken)
    {
        var recipients = await RecipientsAsync(session.ClassId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        Queue(recipients, session, NotificationTypes.SessionCreated, now, new NotificationPayload(session.Id));
        PlanReminders(recipients, session, now);
    }

    /// <summary>Old pending reminders are cancelled and planned again for the new time.</summary>
    public async Task SessionUpdatedAsync(ClassSession session, SessionSnapshot previous, bool notify, CancellationToken cancellationToken)
    {
        await CancelPendingRemindersAsync(session.Id, cancellationToken);
        var recipients = await RecipientsAsync(session.ClassId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (notify)
        {
            Queue(recipients, session, NotificationTypes.SessionUpdated, now, new NotificationPayload(session.Id, previous));
        }

        PlanReminders(recipients, session, now);
    }

    public async Task SessionCancelledAsync(ClassSession session, CancellationToken cancellationToken)
    {
        await CancelPendingRemindersAsync(session.Id, cancellationToken);
        var recipients = await RecipientsAsync(session.ClassId, cancellationToken);
        Queue(recipients, session, NotificationTypes.SessionCancelled, timeProvider.GetUtcNow(), new NotificationPayload(session.Id));
    }

    private void PlanReminders(IReadOnlyList<UserSummary> recipients, ClassSession session, DateTimeOffset now)
    {
        foreach (var reminder in ScheduleRules.PlanReminders(session.StartsAt, now, options.Value.ReminderOffsets))
        {
            Queue(recipients, session, reminder.Type, reminder.SendAt, new NotificationPayload(session.Id));
        }
    }

    private void Queue(IReadOnlyList<UserSummary> recipients, ClassSession session, string type, DateTimeOffset sendAt, NotificationPayload payload)
    {
        foreach (var user in recipients)
        {
            if (user.Email is not null)
            {
                Add(user.Id, NotificationChannel.Email);
            }

            Add(user.Id, NotificationChannel.WebPush);
        }

        void Add(Guid userId, NotificationChannel channel) =>
            db.Notifications.Add(Notification.Create(
                Uuid7.New(timeProvider),
                userId,
                type,
                payload,
                channel,
                sendAt,
                ScheduleRules.DedupeKey(session.Id, type, session.Sequence, userId, channel)));
    }

    private async Task CancelPendingRemindersAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var prefix = $"session:{sessionId}:session_reminder_";
        var pending = await db.Notifications
            .Where(notification => notification.Status == NotificationStatus.Pending && notification.DedupeKey.StartsWith(prefix))
            .ToListAsync(cancellationToken);
        pending.ForEach(notification => notification.Cancel("Session changed."));
    }

    /// <summary>BR-JADWAL-07: only active accounts receive anything.</summary>
    private async Task<IReadOnlyList<UserSummary>> RecipientsAsync(Guid classId, CancellationToken cancellationToken)
    {
        var studentIds = await db.ClassStudents.AsNoTracking()
            .Where(member => member.ClassId == classId && member.Status == MemberStatus.Active)
            .Select(member => member.StudentId)
            .ToListAsync(cancellationToken);
        var profiles = await users.GetUsersAsync(studentIds, cancellationToken);
        return [.. profiles.Values.Where(user => user.IsActive)];
    }
}
