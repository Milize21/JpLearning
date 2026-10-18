using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Materials;
using Nomax.Modules.Content.Infrastructure;
using Nomax.Modules.Content.Infrastructure.Files;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Books;

/// <summary>
/// GET /api/content/books/{id}/cover — every signed-in user (BR-FILE-04 <c>book_cover</c>). The signed link allows inline
/// display; the file endpoint still only shows it as an image after the magic-byte check (BR-FILE-02).
/// </summary>
internal sealed class GetBookCoverHandler(ContentDbContext db, SignedUrls signedUrls)
{
    public async Task<Result<DownloadLinkDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var coverId = await db.Books.AsNoTracking().Where(book => book.Id == id).Select(book => book.CoverFileId).FirstOrDefaultAsync(cancellationToken);
        if (coverId is not { } fileId
            || !await db.StoredFiles.AnyAsync(file => file.Id == fileId && file.Status == FileStatus.Ready, cancellationToken))
        {
            return AppError.NotFound();
        }

        return new DownloadLinkDto(signedUrls.Create(fileId, inline: true));
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/books/{id:guid}/cover", async (Guid id, bool? redirect, GetBookCoverHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken))
                    .ToHttpResult(link => redirect == false ? TypedResults.Ok(link) : TypedResults.Redirect(link.Url)))
            .WithName("GetBookCover")
            .WithSummary("Redirect to a signed link of the book cover image")
            .Produces(StatusCodes.Status302Found)
            .Produces<DownloadLinkDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
