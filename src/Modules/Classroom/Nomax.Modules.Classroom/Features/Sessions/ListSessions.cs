using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Sessions;

/// <summary>
/// GET /api/classroom/classes/{id}/sessions (members, teachers, admin) and GET /api/classroom/me/schedule (the caller's
/// classes as active student or teacher, docs/05 §6). Cancelled sessions are included so calendars can show them.
/// </summary>
internal sealed class ListSessionsHandler(ClassroomDbContext db, ClassAccess access, TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<SessionDto>>> ClassAsync(Guid classId, SessionWindowQuery query, CancellationToken cancellationToken)
    {
        var window = query.Resolve(timeProvider.GetUtcNow());
        if (!window.IsSuccess)
        {
            return window.Error;
        }

        var found = await access.ForMemberAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var (from, to) = window.Value;
        var sessions = await db.ClassSessions.AsNoTracking()
            .Where(session => session.ClassId == classId && session.EndsAt >= from && session.StartsAt <= to)
            .OrderBy(session => session.StartsAt)
            .ToListAsync(cancellationToken);
        return sessions.Select(session => SessionDto.From(session, found.Value.Name)).ToList();
    }

    public async Task<Result<IReadOnlyList<SessionDto>>> MineAsync(SessionWindowQuery query, CancellationToken cancellationToken)
    {
        var window = query.Resolve(timeProvider.GetUtcNow());
        if (!window.IsSuccess)
        {
            return window.Error;
        }

        var (from, to) = window.Value;
        return await ScheduleQueries.ForUser(db, access.UserId, from, to).ToListAsync(cancellationToken);
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/classes/{id:guid}/sessions", async (Guid id, [AsParameters] SessionWindowQuery query, ListSessionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.ClassAsync(id, query, cancellationToken)).ToOkResult())
            .WithName("ListClassSessions")
            .WithSummary("Sessions of a class in a time window (members, teachers, admin)")
            .Produces<IReadOnlyList<SessionDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/me/schedule", async ([AsParameters] SessionWindowQuery query, ListSessionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.MineAsync(query, cancellationToken)).ToOkResult())
            .WithName("GetMySchedule")
            .WithSummary("Sessions of all the caller's classes in a time window")
            .Produces<IReadOnlyList<SessionDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}

internal static class ScheduleQueries
{
    /// <summary>Sessions of classes where the user is an active student (class not draft) or a teacher.</summary>
    public static IQueryable<SessionDto> ForUser(ClassroomDbContext db, Guid userId, DateTimeOffset windowStart, DateTimeOffset windowEnd) =>
        from session in db.ClassSessions.AsNoTracking()
        join item in db.Classes.AsNoTracking() on session.ClassId equals item.Id
        where session.EndsAt >= windowStart && session.StartsAt <= windowEnd
            && (db.ClassTeachers.Any(teacher => teacher.ClassId == item.Id && teacher.TeacherId == userId)
                || (item.Status != Contracts.ClassStatus.Draft && db.ClassStudents.Any(member =>
                    member.ClassId == item.Id && member.StudentId == userId && member.Status == MemberStatus.Active)))
        orderby session.StartsAt
        select new SessionDto(
            session.Id, session.ClassId, item.Name, session.ChapterId, session.Topic, session.StartsAt, session.EndsAt,
            session.Timezone, session.Platform, session.MeetingUrl, session.Note, session.Status, session.Sequence);
}
