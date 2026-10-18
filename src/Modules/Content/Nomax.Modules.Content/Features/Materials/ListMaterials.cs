using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Materials;

/// <summary>
/// GET /api/content/chapters/{id}/materials — BR-KONTEN-01: students need access to the chapter (403 chapter_locked) and
/// only see published materials; staff see everything.
/// </summary>
internal sealed class ListMaterialsHandler(ContentDbContext db, ChapterGate gate, TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<MaterialDto>>> HandleAsync(Guid chapterId, CancellationToken cancellationToken)
    {
        if (!await db.Chapters.AnyAsync(chapter => chapter.Id == chapterId, cancellationToken))
        {
            return AppError.NotFound();
        }

        if (await gate.CheckAsync(chapterId, cancellationToken) is { } locked)
        {
            return locked;
        }

        var materials = db.Materials.AsNoTracking().Where(material => material.ChapterId == chapterId);
        if (gate.IsStudent)
        {
            var now = timeProvider.GetUtcNow();
            materials = materials.Where(material => material.PublishAt == null || material.PublishAt <= now);
        }

        var rows = await materials
            .OrderBy(material => material.SortOrder)
            .ThenBy(material => material.CreatedAt)
            .GroupJoin(db.StoredFiles.AsNoTracking(), material => material.FileId, file => file.Id, (material, files) => new { material, files })
            .SelectMany(entry => entry.files.DefaultIfEmpty(), (entry, file) => new { entry.material, file })
            .ToListAsync(cancellationToken);

        return rows.Select(row => MaterialDto.From(row.material, row.file)).ToList();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/chapters/{id:guid}/materials", async (Guid id, ListMaterialsHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOkResult())
            .WithName("ListMaterials")
            .WithSummary("Materials of a chapter; students only when the chapter is unlocked and the material published")
            .Produces<IReadOnlyList<MaterialDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
