using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Classes;

/// <summary>
/// POST /api/classroom/classes/{id}/archive — primary teacher or admin. An archived class stays readable for its students
/// (materials and drilling), without new quizzes or assignments (BR-AKSES-01).
/// </summary>
internal sealed class ArchiveClassHandler(ClassroomDbContext db, ClassAccess access)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (!access.IsAdmin && !await access.IsPrimaryTeacherAsync(id, cancellationToken))
        {
            return AppError.Forbidden("Only the primary teacher or an admin archives a class.");
        }

        found.Value.Archive();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/classes/{id:guid}/archive", async (Guid id, ArchiveClassHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ArchiveClass")
            .WithSummary("Archive a class: read only from now on (primary teacher, admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
