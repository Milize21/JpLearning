using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Messaging;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Classroom.Infrastructure.Notifications;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Sessions;

/// <summary>
/// POST /api/classroom/sessions/{id}/cancel — teachers of the class and admins. BR-JADWAL-05: status cancelled, sequence + 1,
/// pending reminders cancelled and <c>session_cancelled</c> sent. Cancelling twice changes nothing.
/// </summary>
internal sealed class CancelSessionHandler(ClassroomDbContext db, ClassAccess access, NotificationPlanner planner, TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await db.ClassSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
        if (session is null)
        {
            return AppError.NotFound();
        }

        var found = await access.ForTeacherAsync(session.ClassId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (session.Status == SessionStatus.Cancelled)
        {
            return Result.Success();
        }

        session.Cancel();
        await planner.SessionCancelledAsync(session, cancellationToken);
        db.AddIntegrationEvent(new SessionCancelled(session.Id, session.ClassId, session.StartsAt, session.EndsAt, session.Sequence), timeProvider);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/sessions/{id:guid}/cancel", async (Guid id, CancelSessionHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("CancelSession")
            .WithSummary("Cancel a session; members are told and reminders stop (teachers, admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
