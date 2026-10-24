using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Public;

/// <summary><see cref="IReferenceDataReader"/> for Practice (docs/02 §6.2); read-only queries on the content schema.</summary>
internal sealed class ReferenceDataReader(ContentDbContext db) : IReferenceDataReader
{
    public async Task<IReadOnlyList<KanaItem>> GetKanaAsync(Guid levelId, CancellationToken cancellationToken) =>
        [
            .. (await db.KanaChars.AsNoTracking()
                    .Where(item => item.LevelId == levelId && item.ChapterId != null)
                    .OrderBy(item => item.SortOrder)
                    .ToListAsync(cancellationToken))
                .Select(item => new KanaItem(
                    item.Id, item.LevelId, item.ChapterId, item.Source, item.License, item.Character,
                    EnumSnakeCase.ToValue(item.Script), item.Romaji, item.RowKey)),
        ];

    public async Task<IReadOnlyList<KanjiItem>> GetKanjiAsync(Guid levelId, CancellationToken cancellationToken) =>
        [
            .. (await db.Kanji.AsNoTracking()
                    .Where(item => item.LevelId == levelId && item.ChapterId != null)
                    .OrderBy(item => item.Character)
                    .ToListAsync(cancellationToken))
                .Select(item => new KanjiItem(
                    item.Id, item.LevelId, item.ChapterId, item.Source, item.License, item.Character,
                    item.Onyomi, item.Kunyomi, item.Meanings.Indonesian, item.Meanings.English)),
        ];

    public async Task<IReadOnlyList<VocabItem>> GetVocabAsync(Guid levelId, CancellationToken cancellationToken) =>
        [
            .. (await db.Vocab.AsNoTracking()
                    .Where(item => item.LevelId == levelId && item.ChapterId != null)
                    .OrderBy(item => item.Reading)
                    .ToListAsync(cancellationToken))
                .Select(item => new VocabItem(
                    item.Id, item.LevelId, item.ChapterId, item.Source, item.License, item.Word, item.Reading,
                    item.Meanings.Indonesian, item.Meanings.English, item.Tags)),
        ];

    public async Task<IReadOnlyList<SentenceItem>> GetSentencesAsync(Guid levelId, CancellationToken cancellationToken) =>
        [
            .. (await db.Sentences.AsNoTracking()
                    .Where(item => item.LevelId == levelId && item.ChapterId != null)
                    .OrderBy(item => item.CreatedAt)
                    .ToListAsync(cancellationToken))
                .Select(item => new SentenceItem(item.Id, item.LevelId, item.ChapterId, item.Source, item.License, item.Text, item.Tags)),
        ];

    public async Task<IReadOnlyList<ReferenceItemLabel>> GetLabelsAsync(
        IReadOnlyCollection<(ReferenceItemKind Kind, Guid Id)> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        var ids = items.ToLookup(item => item.Kind, item => item.Id);
        var labels = new List<ReferenceItemLabel>();

        if (ids[ReferenceItemKind.Kana].ToList() is { Count: > 0 } kanaIds)
        {
            labels.AddRange((await db.KanaChars.AsNoTracking().Where(item => kanaIds.Contains(item.Id)).ToListAsync(cancellationToken))
                .Select(item => new ReferenceItemLabel(ReferenceItemKind.Kana, item.Id, new TextBlock(item.Romaji, null, [new JaSegment(item.Character)]))));
        }

        if (ids[ReferenceItemKind.Kanji].ToList() is { Count: > 0 } kanjiIds)
        {
            labels.AddRange((await db.Kanji.AsNoTracking().Where(item => kanjiIds.Contains(item.Id)).ToListAsync(cancellationToken))
                .Select(item => new ReferenceItemLabel(ReferenceItemKind.Kanji, item.Id, new TextBlock(
                    FirstOf(item.Meanings.Indonesian),
                    FirstOf(item.Meanings.English),
                    [new JaSegment(item.Character, FirstReading(item))]))));
        }

        if (ids[ReferenceItemKind.Vocab].ToList() is { Count: > 0 } vocabIds)
        {
            labels.AddRange((await db.Vocab.AsNoTracking().Where(item => vocabIds.Contains(item.Id)).ToListAsync(cancellationToken))
                .Select(item => new ReferenceItemLabel(ReferenceItemKind.Vocab, item.Id, new TextBlock(
                    FirstOf(item.Meanings.Indonesian),
                    FirstOf(item.Meanings.English),
                    [new JaSegment(item.Word, JapaneseScript.ContainsKanji(item.Word) ? item.Reading : null)]))));
        }

        if (ids[ReferenceItemKind.Grammar].ToList() is { Count: > 0 } grammarIds)
        {
            labels.AddRange((await db.GrammarPoints.AsNoTracking().Where(item => grammarIds.Contains(item.Id)).ToListAsync(cancellationToken))
                .Select(item => new ReferenceItemLabel(ReferenceItemKind.Grammar, item.Id, item.Title)));
        }

        if (ids[ReferenceItemKind.Sentence].ToList() is { Count: > 0 } sentenceIds)
        {
            labels.AddRange((await db.Sentences.AsNoTracking().Where(item => sentenceIds.Contains(item.Id)).ToListAsync(cancellationToken))
                .Select(item => new ReferenceItemLabel(ReferenceItemKind.Sentence, item.Id, item.Text)));
        }

        return labels;
    }

    private static string? FirstOf(IReadOnlyList<string> values) => values.Count > 0 ? values[0] : null;

    /// <summary>The kanji's own part of the first kun reading (before the okurigana dot), else the first on reading.</summary>
    private static string? FirstReading(Kanji kanji) =>
        kanji.Kunyomi.Select(reading => reading.Split('.')[0]).FirstOrDefault(reading => reading.Length > 0)
        ?? kanji.Onyomi.FirstOrDefault();
}
