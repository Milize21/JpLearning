using System.Text.Json.Serialization;
using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Domain;

/// <summary>Meanings of a kanji or word per UI language, stored as jsonb <c>{"id": [...], "en": [...]}</c> (docs/03 §5.6).</summary>
public sealed record Meanings(
    [property: JsonPropertyName("id")] IReadOnlyList<string> Indonesian,
    [property: JsonPropertyName("en")] IReadOnlyList<string> English);

/// <summary>
/// Columns shared by every reference data table (docs/03 §5.6): level, optional chapter, and the mandatory source and
/// license of the data (AGENTS.md §12). Items without a chapter are not used by the question generator (BR-KONTEN-04).
/// </summary>
public abstract class ReferenceItem : IHasTimestamps
{
    public const int SourceMaxLength = 200;
    public const int LicenseMaxLength = 100;

    public Guid Id { get; protected set; }

    public Guid LevelId { get; protected set; }

    public Guid? ChapterId { get; protected set; }

    public string Source { get; protected set; } = string.Empty;

    public string License { get; protected set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void MapToChapter(Guid? chapterId) => ChapterId = chapterId;

    public void ChangeSource(string source, string license)
    {
        Source = source;
        License = license;
    }

    protected void Init(Guid id, Guid levelId, Guid? chapterId, string source, string license)
    {
        Id = id;
        LevelId = levelId;
        ChapterId = chapterId;
        Source = source;
        License = license;
    }
}

/// <summary>content.kana_chars.</summary>
public sealed class KanaChar : ReferenceItem
{
    private KanaChar()
    {
    }

    /// <summary>Column <c>char</c>.</summary>
    public string Character { get; private set; } = string.Empty;

    public KanaScript Script { get; private set; }

    public string Romaji { get; private set; } = string.Empty;

    /// <summary>Kana row (<c>a</c>, <c>ka</c>, …, <c>kya</c>) used by the "kana row" weakness grouping.</summary>
    public string RowKey { get; private set; } = string.Empty;

    public KanaVariant Variant { get; private set; }

    public int SortOrder { get; private set; }

    public static KanaChar Create(
        Guid id, Guid levelId, Guid? chapterId, string source, string license,
        string character, KanaScript script, string romaji, string rowKey, KanaVariant variant, int sortOrder)
    {
        var item = new KanaChar
        {
            Character = character,
            Script = script,
            Romaji = romaji,
            RowKey = rowKey,
            Variant = variant,
            SortOrder = sortOrder,
        };
        item.Init(id, levelId, chapterId, source, license);
        return item;
    }

    public void Update(string? romaji, string? rowKey, KanaVariant? variant, int? sortOrder)
    {
        Romaji = romaji ?? Romaji;
        RowKey = rowKey ?? RowKey;
        Variant = variant ?? Variant;
        SortOrder = sortOrder ?? SortOrder;
    }
}

/// <summary>content.kanji.</summary>
public sealed class Kanji : ReferenceItem
{
    private Kanji()
    {
    }

    public string Character { get; private set; } = string.Empty;

    /// <summary>Katakana readings.</summary>
    public List<string> Onyomi { get; private set; } = [];

    /// <summary>Hiragana readings; okurigana after a dot, e.g. <c>た.べる</c>.</summary>
    public List<string> Kunyomi { get; private set; } = [];

    public Meanings Meanings { get; private set; } = null!;

    public short? StrokeCount { get; private set; }

    /// <summary>Hex code of the KanjiVG file, e.g. <c>098df</c>.</summary>
    public string? KanjivgCode { get; private set; }

    public static Kanji Create(
        Guid id, Guid levelId, Guid? chapterId, string source, string license,
        string character, IEnumerable<string> onyomi, IEnumerable<string> kunyomi, Meanings meanings, short? strokeCount, string? kanjivgCode)
    {
        var item = new Kanji
        {
            Character = character,
            Onyomi = [.. onyomi],
            Kunyomi = [.. kunyomi],
            Meanings = meanings,
            StrokeCount = strokeCount,
            KanjivgCode = kanjivgCode,
        };
        item.Init(id, levelId, chapterId, source, license);
        return item;
    }

    public void Update(IEnumerable<string>? onyomi, IEnumerable<string>? kunyomi, Meanings? meanings, short? strokeCount)
    {
        Onyomi = onyomi is null ? Onyomi : [.. onyomi];
        Kunyomi = kunyomi is null ? Kunyomi : [.. kunyomi];
        Meanings = meanings ?? Meanings;
        StrokeCount = strokeCount ?? StrokeCount;
    }
}

/// <summary>content.vocab.</summary>
public sealed class Vocab : ReferenceItem
{
    private Vocab()
    {
    }

    /// <summary>Written form (kanji or kana).</summary>
    public string Word { get; private set; } = string.Empty;

    /// <summary>Reading in kana.</summary>
    public string Reading { get; private set; } = string.Empty;

    public Meanings Meanings { get; private set; } = null!;

    /// <summary>Part of speech, e.g. <c>noun</c>, <c>verb_godan</c>.</summary>
    public string? Pos { get; private set; }

    public List<string> Tags { get; private set; } = [];

    public long? JmdictSeq { get; private set; }

    public static Vocab Create(
        Guid id, Guid levelId, Guid? chapterId, string source, string license,
        string word, string reading, Meanings meanings, string? pos, IEnumerable<string> tags, long? jmdictSeq)
    {
        var item = new Vocab
        {
            Word = word,
            Reading = reading,
            Meanings = meanings,
            Pos = pos,
            Tags = [.. tags],
            JmdictSeq = jmdictSeq,
        };
        item.Init(id, levelId, chapterId, source, license);
        return item;
    }

    public void Update(Meanings? meanings, string? pos, IEnumerable<string>? tags)
    {
        Meanings = meanings ?? Meanings;
        Pos = pos ?? Pos;
        Tags = tags is null ? Tags : [.. tags];
    }
}

/// <summary>content.grammar_points.</summary>
public sealed class GrammarPoint : ReferenceItem
{
    private GrammarPoint()
    {
    }

    /// <summary>E.g. <c>〜は〜です</c>.</summary>
    public string Pattern { get; private set; } = string.Empty;

    public TextBlock Title { get; private set; } = null!;

    public RichText? Explanation { get; private set; }

    /// <summary>E.g. <c>particle:wa</c>, used by the "particle type" weakness grouping.</summary>
    public List<string> Tags { get; private set; } = [];

    public static GrammarPoint Create(
        Guid id, Guid levelId, Guid? chapterId, string source, string license,
        string pattern, TextBlock title, RichText? explanation, IEnumerable<string> tags)
    {
        var item = new GrammarPoint { Pattern = pattern, Title = title, Explanation = explanation, Tags = [.. tags] };
        item.Init(id, levelId, chapterId, source, license);
        return item;
    }

    public void Update(TextBlock? title, RichText? explanation, IEnumerable<string>? tags)
    {
        Title = title ?? Title;
        Explanation = explanation ?? Explanation;
        Tags = tags is null ? Tags : [.. tags];
    }
}

/// <summary>content.sentences.</summary>
public sealed class Sentence : ReferenceItem
{
    private Sentence()
    {
    }

    /// <summary>Indonesian and Japanese required (docs/03 §3.1).</summary>
    public TextBlock Text { get; private set; } = null!;

    public List<string> Tags { get; private set; } = [];

    public Guid? GrammarPointId { get; private set; }

    public long? TatoebaId { get; private set; }

    public static Sentence Create(
        Guid id, Guid levelId, Guid? chapterId, string source, string license,
        TextBlock text, IEnumerable<string> tags, Guid? grammarPointId, long? tatoebaId)
    {
        var item = new Sentence { Text = text, Tags = [.. tags], GrammarPointId = grammarPointId, TatoebaId = tatoebaId };
        item.Init(id, levelId, chapterId, source, license);
        return item;
    }

    public void Update(TextBlock? text, IEnumerable<string>? tags)
    {
        Text = text ?? Text;
        Tags = tags is null ? Tags : [.. tags];
    }
}
