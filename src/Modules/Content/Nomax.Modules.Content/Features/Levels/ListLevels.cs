using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;

namespace Nomax.Modules.Content.Features.Levels;

/// <summary>GET /api/content/levels — every level with its books, for all roles (docs/05 §5).</summary>
internal sealed class ListLevelsHandler(ContentDbContext db)
{
    public async Task<IReadOnlyList<LevelDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var levels = await db.Levels.AsNoTracking().OrderBy(level => level.SortOrder).ThenBy(level => level.Code).ToListAsync(cancellationToken);
        var books = await db.Books.AsNoTracking().OrderBy(book => book.SortOrder).ToListAsync(cancellationToken);
        var chapterCounts = await db.Chapters.AsNoTracking()
            .GroupBy(chapter => chapter.BookId)
            .Select(group => new { BookId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.BookId, entry => entry.Count, cancellationToken);

        var booksByLevel = books.ToLookup(book => book.LevelId);
        return
        [
            .. levels.Select(level => LevelDto.From(
                level,
                [.. booksByLevel[level.Id].Select(book => BookSummaryDto.From(book, chapterCounts.GetValueOrDefault(book.Id)))])),
        ];
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/levels", async (ListLevelsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.HandleAsync(cancellationToken)))
            .WithName("ListLevels")
            .WithSummary("Levels with their books")
            .Produces<IReadOnlyList<LevelDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
