using Nomax.Modules.Content.Domain;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Common;

// Response shapes of /api/content (docs/05 §5). Editable entities carry `version` (xmin) for PATCH (docs/05 §1).

public sealed record LevelDto(Guid Id, string Code, TextBlock Name, int SortOrder, uint Version, IReadOnlyList<BookSummaryDto> Books)
{
    public static LevelDto From(Level level, IReadOnlyList<BookSummaryDto> books) =>
        new(level.Id, level.Code, level.Name, level.SortOrder, level.Version, books);
}

public sealed record BookSummaryDto(Guid Id, Guid LevelId, TextBlock Title, Guid? CoverFileId, int SortOrder, int ChapterCount, uint Version)
{
    public static BookSummaryDto From(Book book, int chapterCount) =>
        new(book.Id, book.LevelId, book.Title, book.CoverFileId, book.SortOrder, chapterCount, book.Version);
}

public sealed record BookDto(
    Guid Id,
    Guid LevelId,
    TextBlock Title,
    Guid? CoverFileId,
    int SortOrder,
    uint Version,
    IReadOnlyList<ChapterSummaryDto> Chapters)
{
    public static BookDto From(Book book, IReadOnlyList<ChapterSummaryDto> chapters) =>
        new(book.Id, book.LevelId, book.Title, book.CoverFileId, book.SortOrder, book.Version, chapters);
}

/// <param name="IsUnlocked">For students: whether the chapter is open for one of their classes (BR-AKSES-01). Null for staff.</param>
public sealed record ChapterSummaryDto(Guid Id, int Number, TextBlock Title, int SortOrder, uint Version, bool? IsUnlocked)
{
    public static ChapterSummaryDto From(Chapter chapter, bool? isUnlocked) =>
        new(chapter.Id, chapter.Number, chapter.Title, chapter.SortOrder, chapter.Version, isUnlocked);
}

public sealed record ChapterDto(Guid Id, Guid BookId, Guid LevelId, int Number, TextBlock Title, int SortOrder, uint Version)
{
    public static ChapterDto From(Chapter chapter, Guid levelId) =>
        new(chapter.Id, chapter.BookId, levelId, chapter.Number, chapter.Title, chapter.SortOrder, chapter.Version);
}
