namespace Nomax.Modules.Classroom.Domain;

// Stored as snake_case text with CHECK constraints (docs/03 §3.3). ClassStatus lives in the Contracts.

public enum MemberStatus
{
    Active,
    Removed,
}

public enum SessionPlatform
{
    Zoom,
    Discord,
    Other,
}

public enum SessionStatus
{
    Scheduled,
    Cancelled,
}

public enum NotificationChannel
{
    Email,
    WebPush,
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
    Cancelled,
}
