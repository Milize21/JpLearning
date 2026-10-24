using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Contracts;

/// <summary>Which reference table an item comes from (docs/03 §3.3 <c>ref_item_type</c>).</summary>
public enum ReferenceItemKind
{
    Kana,
    Kanji,
    Vocab,
    Grammar,
    Sentence,
}

/// <param name="Script"><c>hiragana</c> or <c>katakana</c>.</param>
/// <param name="RowKey">Kana row such as <c>ka</c>, used for the <c>kana_row:*</c> tag.</param>
public sealed record KanaItem(Guid Id, Guid LevelId, Guid? ChapterId, string Source, string License, string Character, string Script, string Romaji, string RowKey);

public sealed record KanjiItem(
    Guid Id,
    Guid LevelId,
    Guid? ChapterId,
    string Source,
    string License,
    string Character,
    IReadOnlyList<string> Onyomi,
    IReadOnlyList<string> Kunyomi,
    IReadOnlyList<string> MeaningsId,
    IReadOnlyList<string> MeaningsEn);

public sealed record VocabItem(
    Guid Id,
    Guid LevelId,
    Guid? ChapterId,
    string Source,
    string License,
    string Word,
    string Reading,
    IReadOnlyList<string> MeaningsId,
    IReadOnlyList<string> MeaningsEn,
    IReadOnlyList<string> Tags);

public sealed record SentenceItem(Guid Id, Guid LevelId, Guid? ChapterId, string Source, string License, TextBlock Text, IReadOnlyList<string> Tags);

/// <summary>Short display text of any reference item, e.g. for the weakest items list (BR-MASTERY-03).</summary>
public sealed record ReferenceItemLabel(ReferenceItemKind Kind, Guid Id, TextBlock Label);

/// <summary>
/// Reference data for Practice (docs/02 §6.2): the question generator (BR-SOAL-05) reads items per level and chapter,
/// and progress pages show item labels. Only items mapped to a chapter are returned for generation (BR-KONTEN-04).
/// </summary>
public interface IReferenceDataReader
{
    Task<IReadOnlyList<KanaItem>> GetKanaAsync(Guid levelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<KanjiItem>> GetKanjiAsync(Guid levelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VocabItem>> GetVocabAsync(Guid levelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SentenceItem>> GetSentencesAsync(Guid levelId, CancellationToken cancellationToken);

    /// <summary>Unknown items are absent from the result.</summary>
    Task<IReadOnlyList<ReferenceItemLabel>> GetLabelsAsync(IReadOnlyCollection<(ReferenceItemKind Kind, Guid Id)> items, CancellationToken cancellationToken);
}
