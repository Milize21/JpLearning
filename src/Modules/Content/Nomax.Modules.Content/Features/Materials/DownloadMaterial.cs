using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.Modules.Content.Infrastructure.Files;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Materials;

/// <summary>Where the browser goes next: a signed link valid for a few minutes.</summary>
public sealed record DownloadLinkDto(string Url);

/// <summary>
/// GET /api/content/materials/{id}/download — BR-FILE-04: students need chapter access and a published material, staff
/// always. Answers 302 to the signed link (or the link as JSON with <c>?redirect=false</c>, for clients that add the token
/// in a header and cannot follow redirects with it).
/// </summary>
internal sealed class DownloadMaterialHandler(ContentDbContext db, ChapterGate gate, SignedUrls signedUrls, TimeProvider timeProvider)
{
    public async Task<Result<DownloadLinkDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(material => material.Id == id, cancellationToken);
        if (material is null || material.Kind != MaterialKind.File || material.FileId is not { } fileId)
        {
            return AppError.NotFound();
        }

        if (gate.IsStudent && !MaterialVisibility.IsPublished(material.PublishAt, timeProvider.GetUtcNow()))
        {
            return AppError.NotFound();
        }

        if (await gate.CheckAsync(material.ChapterId, cancellationToken) is { } locked)
        {
            return locked;
        }

        var ready = await db.StoredFiles.AnyAsync(file => file.Id == fileId && file.Status == FileStatus.Ready, cancellationToken);
        return ready ? new DownloadLinkDto(signedUrls.Create(fileId)) : new AppError(ErrorCodes.FileNotReady, "The file is not available.");
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/materials/{id:guid}/download", async (Guid id, bool? redirect, DownloadMaterialHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken))
                    .ToHttpResult(link => redirect == false ? TypedResults.Ok(link) : TypedResults.Redirect(link.Url)))
            .WithName("DownloadMaterial")
            .WithSummary("Redirect to a signed download link of a file material")
            .Produces(StatusCodes.Status302Found)
            .Produces<DownloadLinkDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
