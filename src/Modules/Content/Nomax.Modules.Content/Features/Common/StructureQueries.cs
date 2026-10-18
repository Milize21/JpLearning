using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Infrastructure;

namespace Nomax.Modules.Content.Features.Common;

internal static class StructureQueries
{
    public static async Task<IReadOnlyList<BookSummaryDto>> LevelBooksAsync(ContentDbContext db, Guid levelId, CancellationToken cancellationToken)
    {
        var books = await db.Books.AsNoTracking()
            .Where(book => book.LevelId == levelId)
            .OrderBy(book => book.SortOrder)
            .ToListAsync(cancellationToken);
        var bookIds = books.Select(book => book.Id).ToList();
        var counts = await db.Chapters.AsNoTracking()
            .Where(chapter => bookIds.Contains(chapter.BookId))
            .GroupBy(chapter => chapter.BookId)
            .Select(group => new { BookId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.BookId, entry => entry.Count, cancellationToken);
        return [.. books.Select(book => BookSummaryDto.From(book, counts.GetValueOrDefault(book.Id)))];
    }

    public static Task<Guid> LevelOfBookAsync(ContentDbContext db, Guid bookId, CancellationToken cancellationToken) =>
        db.Books.AsNoTracking().Where(book => book.Id == bookId).Select(book => book.LevelId).FirstOrDefaultAsync(cancellationToken);
}
