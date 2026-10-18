using Nomax.SharedKernel.Events;

namespace Nomax.Modules.Classroom.Contracts;

// Integration events published by Classroom (docs/05 §8).

public sealed record ChapterUnlocked(Guid ClassId, Guid ChapterId, Guid UnlockedBy, DateTimeOffset UnlockedAt) : IIntegrationEvent
{
    public static string EventType => "ChapterUnlocked.v1";
}

public sealed record ChapterLocked(Guid ClassId, Guid ChapterId, Guid LockedBy, DateTimeOffset LockedAt) : IIntegrationEvent
{
    public static string EventType => "ChapterLocked.v1";
}

public sealed record SessionCreated(Guid SessionId, Guid ClassId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Sequence)
    : IIntegrationEvent
{
    public static string EventType => "SessionCreated.v1";
}

public sealed record SessionUpdated(Guid SessionId, Guid ClassId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Sequence)
    : IIntegrationEvent
{
    public static string EventType => "SessionUpdated.v1";
}

public sealed record SessionCancelled(Guid SessionId, Guid ClassId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Sequence)
    : IIntegrationEvent
{
    public static string EventType => "SessionCancelled.v1";
}
