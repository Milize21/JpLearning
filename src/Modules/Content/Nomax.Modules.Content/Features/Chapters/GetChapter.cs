using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Chapters;

/// <summary>GET /api/content/chapters/{id} — all roles; a student needs access to the chapter (403 chapter_locked).</summary>
internal sealed class GetChapterHandler(ContentDbContext db, ChapterGate gate)
{
    public async Task<Result<ChapterDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await db.Chapters.AsNoTracking().FirstOrDefaultAsync(chapter => chapter.Id == id, cancellationToken);
        if (chapter is null)
        {
            return AppError.NotFound();
        }

        if (await gate.CheckAsync(id, cancellationToken) is { } locked)
        {
            return locked;
        }

        return ChapterDto.From(chapter, await StructureQueries.LevelOfBookAsync(db, chapter.BookId, cancellationToken));
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/chapters/{id:guid}", async (Guid id, GetChapterHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOkResult())
            .WithName("GetChapter")
            .WithSummary("A chapter; students only when it is unlocked for one of their classes")
            .Produces<ChapterDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
