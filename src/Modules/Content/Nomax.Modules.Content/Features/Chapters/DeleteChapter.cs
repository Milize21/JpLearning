using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Chapters;

/// <summary>
/// DELETE /api/content/chapters/{id} — admin; BR-KONTEN-03: no materials or reference data in the chapter, and no unlock,
/// session or question elsewhere that refers to it.
/// </summary>
internal sealed class DeleteChapterHandler(ContentDbContext db, ContentUsage usage)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await db.Chapters.FirstOrDefaultAsync(chapter => chapter.Id == id, cancellationToken);
        if (chapter is null)
        {
            return AppError.NotFound();
        }

        var hasDependents =
            await db.Materials.AnyAsync(material => material.ChapterId == id, cancellationToken)
            || await db.KanaChars.AnyAsync(item => item.ChapterId == id, cancellationToken)
            || await db.Kanji.AnyAsync(item => item.ChapterId == id, cancellationToken)
            || await db.Vocab.AnyAsync(item => item.ChapterId == id, cancellationToken)
            || await db.GrammarPoints.AnyAsync(item => item.ChapterId == id, cancellationToken)
            || await db.Sentences.AnyAsync(item => item.ChapterId == id, cancellationToken)
            || await usage.IsUsedElsewhereAsync(ContentUsageTarget.Chapter, id, cancellationToken);
        if (hasDependents)
        {
            return ContentErrors.HasDependents("chapter");
        }

        db.Chapters.Remove(chapter);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/chapters/{id:guid}", async (Guid id, DeleteChapterHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("DeleteChapter")
            .WithSummary("Delete an unused chapter (admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
