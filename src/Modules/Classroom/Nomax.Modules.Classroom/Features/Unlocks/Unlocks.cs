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
using Nomax.Modules.Content.Contracts;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Unlocks;

/// <summary>
/// Chapter unlocks of a class (BR-AKSES-01/02). GET for members, teachers and admins; PUT/DELETE for teachers and admins.
/// Unlocking is idempotent and only allowed for chapters of the class's book. Both changes publish their event through the
/// outbox in the same transaction.
/// </summary>
internal sealed class UnlocksHandler(
    ClassroomDbContext db,
    ClassAccess access,
    IContentCatalog catalog,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<ClassUnlockDto>>> ListAsync(Guid classId, CancellationToken cancellationToken)
    {
        var found = await access.ForMemberAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        return await db.ClassUnlocks.AsNoTracking()
            .Where(unlock => unlock.ClassId == classId)
            .OrderBy(unlock => unlock.UnlockedAt)
            .Select(unlock => new ClassUnlockDto(unlock.ChapterId, unlock.UnlockedAt, unlock.UnlockedBy))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> UnlockAsync(Guid classId, Guid chapterId, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (ClassAccess.EnsureWritable(found.Value) is { } readOnly)
        {
            return readOnly;
        }

        var chapter = await catalog.GetChapterAsync(chapterId, cancellationToken);
        if (chapter is null || chapter.BookId != found.Value.BookId)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["chapterId"] = ["Must be a chapter of the class's book."] });
        }

        if (await db.ClassUnlocks.AnyAsync(unlock => unlock.ClassId == classId && unlock.ChapterId == chapterId, cancellationToken))
        {
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow();
        db.ClassUnlocks.Add(ClassUnlock.Create(classId, chapterId, access.UserId, now));
        db.AddIntegrationEvent(new ChapterUnlocked(classId, chapterId, access.UserId, now), timeProvider);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> LockAsync(Guid classId, Guid chapterId, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (ClassAccess.EnsureWritable(found.Value) is { } readOnly)
        {
            return readOnly;
        }

        var unlock = await db.ClassUnlocks.FirstOrDefaultAsync(unlock => unlock.ClassId == classId && unlock.ChapterId == chapterId, cancellationToken);
        if (unlock is null)
        {
            return Result.Success();
        }

        db.ClassUnlocks.Remove(unlock);
        db.AddIntegrationEvent(new ChapterLocked(classId, chapterId, access.UserId, timeProvider.GetUtcNow()), timeProvider);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/classes/{id:guid}/unlocks", async (Guid id, UnlocksHandler handler, CancellationToken cancellationToken) =>
                (await handler.ListAsync(id, cancellationToken)).ToOkResult())
            .WithName("ListClassUnlocks")
            .WithSummary("Unlocked chapters of a class (members, teachers, admin)")
            .Produces<IReadOnlyList<ClassUnlockDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/classes/{id:guid}/unlocks/{chapterId:guid}", async (Guid id, Guid chapterId, UnlocksHandler handler, CancellationToken cancellationToken) =>
                (await handler.UnlockAsync(id, chapterId, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("UnlockChapter")
            .WithSummary("Open a chapter of the class's book for its students (teachers, admin); idempotent")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/classes/{id:guid}/unlocks/{chapterId:guid}", async (Guid id, Guid chapterId, UnlocksHandler handler, CancellationToken cancellationToken) =>
                (await handler.LockAsync(id, chapterId, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("LockChapter")
            .WithSummary("Close a chapter again (teachers, admin); history and mastery stay (BR-AKSES-02)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
