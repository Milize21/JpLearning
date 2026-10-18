using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Books;

/// <summary>GET /api/content/books/{id} — all roles; students also get <c>isUnlocked</c> per chapter (BR-AKSES-01).</summary>
internal sealed class GetBookHandler(ContentDbContext db, ChapterGate gate)
{
    public async Task<Result<BookDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
        if (book is null)
        {
            return AppError.NotFound();
        }

        var chapters = await db.Chapters.AsNoTracking()
            .Where(chapter => chapter.BookId == id)
            .OrderBy(chapter => chapter.SortOrder)
            .ThenBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);
        var accessible = await gate.AccessibleChaptersAsync(cancellationToken);

        return BookDto.From(book, [.. chapters.Select(chapter => ChapterSummaryDto.From(chapter, accessible?.Contains(chapter.Id)))]);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/books/{id:guid}", async (Guid id, GetBookHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOkResult())
            .WithName("GetBook")
            .WithSummary("A book with its chapters; students see which chapters are unlocked")
            .Produces<BookDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
