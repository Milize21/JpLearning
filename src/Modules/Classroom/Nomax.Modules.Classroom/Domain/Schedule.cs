using Nomax.BuildingBlocks.Persistence;

namespace Nomax.Modules.Classroom.Domain;

/// <summary>classroom.class_sessions (docs/03 §6.5): a meeting on Zoom/Discord. <see cref="Sequence"/> is the ICS SEQUENCE.</summary>
public sealed class ClassSession : IHasTimestamps
{
    public const int TopicMaxLength = 200;
    public const int TimezoneMaxLength = 64;
    public const int UrlMaxLength = 2048;
    public const int NoteMaxLength = 2000;

    private ClassSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClassId { get; private set; }

    public Guid? ChapterId { get; private set; }

    public string Topic { get; private set; } = string.Empty;

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>Time zone of the creator, for display.</summary>
    public string Timezone { get; private set; } = string.Empty;

    public SessionPlatform Platform { get; private set; }

    /// <summary>Only members, teachers and admins see it (BR-JADWAL-02).</summary>
    public string? MeetingUrl { get; private set; }

    public string? Note { get; private set; }

    public SessionStatus Status { get; private set; }

    public int Sequence { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ClassSession Create(
        Guid id, Guid classId, Guid? chapterId, string topic, DateTimeOffset startsAt, DateTimeOffset endsAt,
        string timezone, SessionPlatform platform, string? meetingUrl, string? note) =>
        new()
        {
            Id = id,
            ClassId = classId,
            ChapterId = chapterId,
            Topic = topic.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            Timezone = timezone,
            Platform = platform,
            MeetingUrl = meetingUrl,
            Note = note,
            Status = SessionStatus.Scheduled,
        };

    /// <summary>BR-JADWAL-05: any change raises the sequence so calendar clients replace the event.</summary>
    public void Update(Guid? chapterId, string topic, DateTimeOffset startsAt, DateTimeOffset endsAt, SessionPlatform platform, string? meetingUrl, string? note)
    {
        ChapterId = chapterId;
        Topic = topic.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        Platform = platform;
        MeetingUrl = meetingUrl;
        Note = note;
        Sequence++;
    }

    public void Cancel()
    {
        Status = SessionStatus.Cancelled;
        Sequence++;
    }
}

/// <summary>classroom.calendar_feed_tokens (docs/03 §6.6): only the SHA-256 of the token is stored (BR-JADWAL-04).</summary>
public sealed class CalendarFeedToken : IHasTimestamps
{
    private CalendarFeedToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static CalendarFeedToken Create(Guid userId, string tokenHash) => new() { UserId = userId, TokenHash = tokenHash };

    public void Rotate(string tokenHash) => TokenHash = tokenHash;
}

/// <summary>classroom.notifications (docs/03 §6.7): one queued message per recipient and channel.</summary>
public sealed class Notification : IHasTimestamps
{
    public const int TypeMaxLength = 50;
    public const int DedupeKeyMaxLength = 200;

    private Notification()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Catalog of docs/05 §10, e.g. <c>session_reminder_1h</c>.</summary>
    public string Type { get; private set; } = string.Empty;

    public NotificationPayload Payload { get; private set; } = null!;

    public NotificationChannel Channel { get; private set; }

    public DateTimeOffset SendAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public NotificationStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public string DedupeKey { get; private set; } = string.Empty;

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Notification Create(Guid id, Guid userId, string type, NotificationPayload payload, NotificationChannel channel, DateTimeOffset sendAt, string dedupeKey) =>
        new()
        {
            Id = id,
            UserId = userId,
            Type = type,
            Payload = payload,
            Channel = channel,
            SendAt = sendAt,
            Status = NotificationStatus.Pending,
            DedupeKey = dedupeKey,
        };

    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    public void Cancel(string? reason = null)
    {
        Status = NotificationStatus.Cancelled;
        LastError = reason;
    }

    /// <summary>After <paramref name="maxAttempts"/> failures the notification is given up.</summary>
    public void RecordFailure(string error, DateTimeOffset retryAt, int maxAttempts)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
        if (Attempts >= maxAttempts)
        {
            Status = NotificationStatus.Failed;
        }
        else
        {
            SendAt = retryAt;
        }
    }
}

/// <summary>
/// Data of a notification template (jsonb). The current session is read when sending, so a reminder always shows the latest
/// time and link; <see cref="Previous"/> keeps the values before a change for <c>session_updated</c>.
/// </summary>
public sealed record NotificationPayload(Guid SessionId, SessionSnapshot? Previous = null);

public sealed record SessionSnapshot(string Topic, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? MeetingUrl);

/// <summary>classroom.push_subscriptions (docs/03 §6.8): one browser/device of a user.</summary>
public sealed class PushSubscription : IHasTimestamps
{
    public const int P256dhMaxLength = 200;
    public const int AuthMaxLength = 100;
    public const int UserAgentMaxLength = 300;

    private PushSubscription()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public string P256dh { get; private set; } = string.Empty;

    public string Auth { get; private set; } = string.Empty;

    public string? UserAgent { get; private set; }

    public DateTimeOffset? LastSuccessAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static PushSubscription Create(Guid id, Guid userId, string endpoint, string p256dh, string auth, string? userAgent) =>
        new() { Id = id, UserId = userId, Endpoint = endpoint, P256dh = p256dh, Auth = auth, UserAgent = Truncate(userAgent) };

    /// <summary>The same browser subscribed again, possibly after another user signed in on it.</summary>
    public void Renew(Guid userId, string p256dh, string auth, string? userAgent)
    {
        UserId = userId;
        P256dh = p256dh;
        Auth = auth;
        UserAgent = Truncate(userAgent);
    }

    public void MarkDelivered(DateTimeOffset now) => LastSuccessAt = now;

    private static string? Truncate(string? userAgent) =>
        userAgent is null ? null : userAgent.Length <= UserAgentMaxLength ? userAgent : userAgent[..UserAgentMaxLength];
}
