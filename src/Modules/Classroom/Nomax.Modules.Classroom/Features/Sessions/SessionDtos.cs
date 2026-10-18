using Microsoft.AspNetCore.Mvc;
using Nomax.Modules.Classroom.Domain;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Sessions;

/// <param name="MeetingUrl">Only in responses to members, teachers and admins (BR-JADWAL-02); every endpoint returning
/// sessions is limited to them.</param>
public sealed record SessionDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    Guid? ChapterId,
    string Topic,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Timezone,
    SessionPlatform Platform,
    string? MeetingUrl,
    string? Note,
    SessionStatus Status,
    int Sequence)
{
    public static SessionDto From(ClassSession session, string className) => new(
        session.Id,
        session.ClassId,
        className,
        session.ChapterId,
        session.Topic,
        session.StartsAt,
        session.EndsAt,
        session.Timezone,
        session.Platform,
        session.MeetingUrl,
        session.Note,
        session.Status,
        session.Sequence);
}

/// <summary><c>?from=…&amp;to=…</c> (ISO 8601). Defaults: 7 days back to 90 days ahead; at most 400 days.</summary>
public sealed record SessionWindowQuery(
    [property: FromQuery(Name = "from")] DateTimeOffset? From,
    [property: FromQuery(Name = "to")] DateTimeOffset? To)
{
    public const int MaxDays = 400;

    public Result<(DateTimeOffset From, DateTimeOffset To)> Resolve(DateTimeOffset now)
    {
        var from = From ?? now.AddDays(-7);
        var to = To ?? now.AddDays(90);
        if (to <= from || (to - from).TotalDays > MaxDays)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["to"] = [$"Must be after from and at most {MaxDays} days later."] });
        }

        return (from, to);
    }
}
