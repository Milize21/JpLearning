using Nomax.Modules.Content.Domain;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Reference;

// One response type per reference table (docs/03 §5.6) so the generated frontend types stay precise.

public sealed record KanaDto(
    Guid Id, Guid LevelId, Guid? ChapterId, string Source, string License,
    string Character, KanaScript Script, string Romaji, string RowKey, KanaVariant Variant, int SortOrder)
{
    public static KanaDto From(KanaChar item) => new(
        item.Id, item.LevelId, item.ChapterId, item.Source, item.License,
        item.Character, item.Script, item.Romaji, item.RowKey, item.Variant, item.SortOrder);
}

public sealed record KanjiDto(
    Guid Id, Guid LevelId, Guid? ChapterId, string Source, string License,
    string Character, IReadOnlyList<string> Onyomi, IReadOnlyList<string> Kunyomi, Meanings Meanings, short? StrokeCount, string? KanjivgCode)
{
    public static KanjiDto From(Kanji item) => new(
        item.Id, item.LevelId, item.ChapterId, item.Source, item.License,
        item.Character, item.Onyomi, item.Kunyomi, item.Meanings, item.StrokeCount, item.KanjivgCode);
}

public sealed record VocabDto(
    Guid Id, Guid LevelId, Guid? ChapterId, string Source, string License,
    string Word, string Reading, Meanings Meanings, string? Pos, IReadOnlyList<string> Tags, long? JmdictSeq)
{
    public static VocabDto From(Vocab item) => new(
        item.Id, item.LevelId, item.ChapterId, item.Source, item.License,
        item.Word, item.Reading, item.Meanings, item.Pos, item.Tags, item.JmdictSeq);
}

public sealed record GrammarPointDto(
    Guid Id, Guid LevelId, Guid? ChapterId, string Source, string License,
    string Pattern, TextBlock Title, RichText? Explanation, IReadOnlyList<string> Tags)
{
    public static GrammarPointDto From(GrammarPoint item) => new(
        item.Id, item.LevelId, item.ChapterId, item.Source, item.License,
        item.Pattern, item.Title, item.Explanation, item.Tags);
}

public sealed record SentenceDto(
    Guid Id, Guid LevelId, Guid? ChapterId, string Source, string License,
    TextBlock Text, IReadOnlyList<string> Tags, Guid? GrammarPointId, long? TatoebaId)
{
    public static SentenceDto From(Sentence item) => new(
        item.Id, item.LevelId, item.ChapterId, item.Source, item.License,
        item.Text, item.Tags, item.GrammarPointId, item.TatoebaId);
}
