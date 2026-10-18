using Nomax.Modules.Classroom.Contracts;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Classroom.Domain;

/// <summary>Notification types of docs/05 §10.</summary>
public static class NotificationTypes
{
    public const string SessionCreated = "session_created";
    public const string SessionUpdated = "session_updated";
    public const string SessionCancelled = "session_cancelled";
    public const string SessionReminder1d = "session_reminder_1d";
    public const string SessionReminder1h = "session_reminder_1h";

    public static bool IsReminder(string type) => type is SessionReminder1d or SessionReminder1h;
}

public sealed record PlannedReminder(string Type, DateTimeOffset SendAt);

/// <summary>BR-JADWAL-01, 05, 06: session validity and reminder planning, as pure functions.</summary>
public static class ScheduleRules
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    /// <summary>Default <c>Schedule:ReminderOffsets</c>: 24 hours and 1 hour before the start.</summary>
    public static IReadOnlyList<TimeSpan> DefaultReminderOffsets { get; } = [TimeSpan.FromHours(24), TimeSpan.FromHours(1)];

    /// <summary>BR-JADWAL-01. Returns the field that breaks the rule, or null.</summary>
    public static (string Field, string Message)? Validate(DateTimeOffset startsAt, DateTimeOffset endsAt, string? meetingUrl, ClassStatus classStatus)
    {
        if (endsAt <= startsAt)
        {
            return ("endsAt", "Must be after startsAt.");
        }

        if (endsAt - startsAt > MaxDuration)
        {
            return ("endsAt", "A session lasts at most 8 hours.");
        }

        if (meetingUrl is not null && !TextRules.IsHttpsUrl(meetingUrl))
        {
            return ("meetingUrl", "Must start with https://.");
        }

        return classStatus == ClassStatus.Active ? null : ("classId", "Sessions can only be scheduled for active classes.");
    }

    /// <summary>
    /// BR-JADWAL-06: one reminder per offset (the first is <c>session_reminder_1d</c>, the second <c>session_reminder_1h</c>),
    /// only when its time is still in the future. A cancelled session has no reminders.
    /// </summary>
    public static IReadOnlyList<PlannedReminder> PlanReminders(DateTimeOffset startsAt, DateTimeOffset now, IReadOnlyList<TimeSpan>? offsets = null)
    {
        offsets ??= DefaultReminderOffsets;
        var types = new[] { NotificationTypes.SessionReminder1d, NotificationTypes.SessionReminder1h };
        var reminders = new List<PlannedReminder>();
        for (var i = 0; i < offsets.Count && i < types.Length; i++)
        {
            var sendAt = startsAt - offsets[i];
            if (sendAt > now)
            {
                reminders.Add(new PlannedReminder(types[i], sendAt));
            }
        }

        return reminders;
    }

    /// <summary>
    /// Unique per message. Updates and reminders include the session sequence, so reminders planned again after a change
    /// do not collide with the cancelled ones.
    /// </summary>
    public static string DedupeKey(Guid sessionId, string type, int sequence, Guid userId, NotificationChannel channel)
    {
        var channelName = channel == NotificationChannel.Email ? "email" : "web_push";
        return type == NotificationTypes.SessionCreated || type == NotificationTypes.SessionCancelled
            ? $"session:{sessionId}:{type}:{userId}:{channelName}"
            : $"session:{sessionId}:{type}:{sequence}:{userId}:{channelName}";
    }
}
