using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Content.Infrastructure;

namespace Nomax.Modules.Content.Public;

/// <summary><see cref="IContentCatalog"/> for other modules (docs/02 §6.2); read-only queries on the content schema.</summary>
internal sealed class ContentCatalog(ContentDbContext db) : IContentCatalog
{
    public async Task<LevelInfo?> GetLevelAsync(Guid levelId, CancellationToken cancellationToken) =>
        await db.Levels.AsNoTracking()
            .Where(level => level.Id == levelId)
            .Select(level => new LevelInfo(level.Id, level.Code, level.Name, level.SortOrder))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<BookInfo?> GetBookAsync(Guid bookId, CancellationToken cancellationToken) =>
        await db.Books.AsNoTracking()
            .Where(book => book.Id == bookId)
            .Select(book => new BookInfo(book.Id, book.LevelId, book.Title, book.SortOrder))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ChapterInfo?> GetChapterAsync(Guid chapterId, CancellationToken cancellationToken) =>
        (await GetChaptersAsync([chapterId], cancellationToken)).GetValueOrDefault(chapterId);

    public async Task<IReadOnlyDictionary<Guid, ChapterInfo>> GetChaptersAsync(IReadOnlyCollection<Guid> chapterIds, CancellationToken cancellationToken)
    {
        if (chapterIds.Count == 0)
        {
            return new Dictionary<Guid, ChapterInfo>();
        }

        var ids = chapterIds.Distinct().ToList();
        return await ChapterInfos(db.Chapters.Where(chapter => ids.Contains(chapter.Id)))
            .ToDictionaryAsync(chapter => chapter.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ChapterInfo>> GetBookChaptersAsync(Guid bookId, CancellationToken cancellationToken) =>
        [
            .. (await ChapterInfos(db.Chapters.Where(chapter => chapter.BookId == bookId)).ToListAsync(cancellationToken))
                .OrderBy(chapter => chapter.SortOrder)
                .ThenBy(chapter => chapter.Number),
        ];

    // Filter on the entities first: EF cannot translate a filter on the projected record.
    private IQueryable<ChapterInfo> ChapterInfos(IQueryable<Domain.Chapter> chapters) =>
        chapters.AsNoTracking().Join(
            db.Books.AsNoTracking(),
            chapter => chapter.BookId,
            book => book.Id,
            (chapter, book) => new ChapterInfo(chapter.Id, chapter.BookId, book.LevelId, chapter.Number, chapter.Title, chapter.SortOrder));
}
