using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Contracts;

public sealed record LevelInfo(Guid Id, string Code, TextBlock Name, int SortOrder);

public sealed record BookInfo(Guid Id, Guid LevelId, TextBlock Title, int SortOrder);

/// <param name="LevelId">Level of the chapter's book.</param>
public sealed record ChapterInfo(Guid Id, Guid BookId, Guid LevelId, int Number, TextBlock Title, int SortOrder);

/// <summary>
/// Level/book/chapter lookups for Classroom and Practice (docs/02 §6.2): validating references and finding the level of a
/// chapter or the chapter order of a book.
/// </summary>
public interface IContentCatalog
{
    Task<LevelInfo?> GetLevelAsync(Guid levelId, CancellationToken cancellationToken);

    Task<BookInfo?> GetBookAsync(Guid bookId, CancellationToken cancellationToken);

    Task<ChapterInfo?> GetChapterAsync(Guid chapterId, CancellationToken cancellationToken);

    /// <summary>Unknown ids are absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, ChapterInfo>> GetChaptersAsync(IReadOnlyCollection<Guid> chapterIds, CancellationToken cancellationToken);

    /// <summary>Chapters of a book in display order (sort_order, then number).</summary>
    Task<IReadOnlyList<ChapterInfo>> GetBookChaptersAsync(Guid bookId, CancellationToken cancellationToken);
}

/// <summary>What another module may still refer to (BR-KONTEN-03 deletion check, BR-FILE-05 orphan files).</summary>
public enum ContentUsageTarget
{
    Level,
    Book,
    Chapter,
    ReferenceItem,
    File,
}

/// <summary>
/// Implemented by every module that stores references to content (Classroom: classes, unlocks, sessions; Practice:
/// questions, submissions). Content asks all implementations before deleting a level/book/chapter/reference item and
/// before treating a file as orphaned, so it never has to query another schema.
/// </summary>
public interface IContentUsageProbe
{
    Task<bool> IsUsedAsync(ContentUsageTarget target, Guid id, CancellationToken cancellationToken);

    /// <summary>The subset of <paramref name="fileIds"/> this module still refers to.</summary>
    Task<IReadOnlySet<Guid>> GetUsedFileIdsAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken);
}
