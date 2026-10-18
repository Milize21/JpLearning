using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Domain;

/// <summary>content.levels (docs/03 §5.1): Dasar (kana), N5, N4, N3. Managed by admins (BR-KONTEN-03).</summary>
public sealed class Level : IHasTimestamps, IHasVersion
{
    public const int CodeMaxLength = 16;

    private Level()
    {
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public TextBlock Name { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Level Create(Guid id, string code, TextBlock name, int sortOrder) =>
        new() { Id = id, Code = code, Name = name, SortOrder = sortOrder };

    public void Update(string? code, TextBlock? name, int? sortOrder)
    {
        Code = code ?? Code;
        Name = name ?? Name;
        SortOrder = sortOrder ?? SortOrder;
    }
}

/// <summary>content.books (docs/03 §5.2).</summary>
public sealed class Book : IHasTimestamps, IHasVersion
{
    private Book()
    {
    }

    public Guid Id { get; private set; }

    public Guid LevelId { get; private set; }

    public TextBlock Title { get; private set; } = null!;

    /// <summary>A <c>book_cover</c> file.</summary>
    public Guid? CoverFileId { get; private set; }

    public int SortOrder { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Book Create(Guid id, Guid levelId, TextBlock title, Guid? coverFileId, int sortOrder) =>
        new() { Id = id, LevelId = levelId, Title = title, CoverFileId = coverFileId, SortOrder = sortOrder };

    public void Update(TextBlock? title, int? sortOrder)
    {
        Title = title ?? Title;
        SortOrder = sortOrder ?? SortOrder;
    }

    public void ChangeCover(Guid? coverFileId) => CoverFileId = coverFileId;
}

/// <summary>content.chapters (docs/03 §5.3). <see cref="Number"/> is the chapter number shown to users, unique per book.</summary>
public sealed class Chapter : IHasTimestamps, IHasVersion
{
    private Chapter()
    {
    }

    public Guid Id { get; private set; }

    public Guid BookId { get; private set; }

    public int Number { get; private set; }

    public TextBlock Title { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Chapter Create(Guid id, Guid bookId, int number, TextBlock title, int sortOrder) =>
        new() { Id = id, BookId = bookId, Number = number, Title = title, SortOrder = sortOrder };

    public void Update(int? number, TextBlock? title, int? sortOrder)
    {
        Number = number ?? Number;
        Title = title ?? Title;
        SortOrder = sortOrder ?? SortOrder;
    }
}
