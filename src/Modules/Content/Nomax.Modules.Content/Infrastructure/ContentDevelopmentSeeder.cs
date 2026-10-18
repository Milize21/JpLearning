using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Content.Domain;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Infrastructure;

/// <summary>
/// Development content of docs/03 §13: the four levels, "Buku Dasar Kana" with chapters 1–3 (rows a, ka, sa) and the
/// hiragana of those rows. Fixed ids so Classroom can seed the demo class against them. Idempotent.
/// </summary>
internal sealed class ContentDevelopmentSeeder(ContentDbContext db) : IDevelopmentSeeder
{
    public static readonly Guid LevelDasar = Guid.Parse("b0000000-0000-4000-8000-000000000001");
    public static readonly Guid LevelN5 = Guid.Parse("b0000000-0000-4000-8000-000000000002");
    public static readonly Guid LevelN4 = Guid.Parse("b0000000-0000-4000-8000-000000000003");
    public static readonly Guid LevelN3 = Guid.Parse("b0000000-0000-4000-8000-000000000004");
    public static readonly Guid BookKana = Guid.Parse("b1000000-0000-4000-8000-000000000001");
    public static readonly Guid Chapter1 = Guid.Parse("b2000000-0000-4000-8000-000000000001");
    public static readonly Guid Chapter2 = Guid.Parse("b2000000-0000-4000-8000-000000000002");
    public static readonly Guid Chapter3 = Guid.Parse("b2000000-0000-4000-8000-000000000003");

    private const string KanaSource = "Tabel kana (fakta umum)";
    private const string KanaLicense = "public-domain";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await db.Levels.AnyAsync(level => level.Id == LevelDasar, cancellationToken))
        {
            // ja-review: 基礎 (きそ) as the Japanese name of the "Dasar" level.
            db.Levels.Add(Level.Create(LevelDasar, "dasar", new TextBlock("Dasar", "Basics", [new JaSegment("基礎", "きそ")]), 0));
            db.Levels.Add(Level.Create(LevelN5, "n5", new TextBlock("N5", "N5", null), 1));
            db.Levels.Add(Level.Create(LevelN4, "n4", new TextBlock("N4", "N4", null), 2));
            db.Levels.Add(Level.Create(LevelN3, "n3", new TextBlock("N3", "N3", null), 3));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Books.AnyAsync(book => book.Id == BookKana, cancellationToken))
        {
            // ja-review: 仮名 (かな) の 本 (ほん) — "the kana book".
            db.Books.Add(Book.Create(
                BookKana,
                LevelDasar,
                new TextBlock("Buku Dasar Kana", "Kana Basics", [new JaSegment("仮名", "かな"), new JaSegment("の"), new JaSegment("本", "ほん")]),
                coverFileId: null,
                sortOrder: 0));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Chapters.AnyAsync(chapter => chapter.BookId == BookKana, cancellationToken))
        {
            // ja-review: あ行 (あぎょう), か行 (かぎょう), さ行 (さぎょう) — reading of 行 here is ぎょう.
            db.Chapters.Add(Chapter.Create(Chapter1, BookKana, 1, Row("Baris a", "Row a", "あ"), 1));
            db.Chapters.Add(Chapter.Create(Chapter2, BookKana, 2, Row("Baris ka", "Row ka", "か"), 2));
            db.Chapters.Add(Chapter.Create(Chapter3, BookKana, 3, Row("Baris sa", "Row sa", "さ"), 3));
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingKana = await db.KanaChars.Select(kana => kana.Character).ToListAsync(cancellationToken);
        var sortOrder = 0;
        foreach (var (row, chapterId, characters) in Rows)
        {
            foreach (var (character, romaji) in characters)
            {
                sortOrder++;
                if (existingKana.Contains(character))
                {
                    continue;
                }

                db.KanaChars.Add(KanaChar.Create(
                    Guid.CreateVersion7(), LevelDasar, chapterId, KanaSource, KanaLicense,
                    character, KanaScript.Hiragana, romaji, row, KanaVariant.Seion, sortOrder));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static TextBlock Row(string indonesian, string english, string kana) =>
        new(indonesian, english, [new JaSegment(kana), new JaSegment("行", "ぎょう")]);

    private static readonly (string Row, Guid Chapter, (string Char, string Romaji)[] Characters)[] Rows =
    [
        ("a", Chapter1, [("あ", "a"), ("い", "i"), ("う", "u"), ("え", "e"), ("お", "o")]),
        ("ka", Chapter2, [("か", "ka"), ("き", "ki"), ("く", "ku"), ("け", "ke"), ("こ", "ko")]),
        ("sa", Chapter3, [("さ", "sa"), ("し", "shi"), ("す", "su"), ("せ", "se"), ("そ", "so")]),
    ];
}
