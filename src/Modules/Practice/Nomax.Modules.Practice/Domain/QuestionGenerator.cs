using Nomax.Modules.Content.Contracts;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Domain;

/// <summary>Templates of BR-SOAL-05.</summary>
public enum GeneratorTemplate
{
    KanaToRomaji,
    RomajiToKana,
    KanaTypeRomaji,
    VocabToMeaning,
    MeaningToVocab,
    VocabTypeReading,
    KanjiToReading,
    KanjiToMeaning,
    SentenceTap,
}

/// <summary>Reference data of one level that the generator reads (items mapped to a chapter, BR-KONTEN-04).</summary>
public sealed record GeneratorInput(
    IReadOnlyList<KanaItem> Kana,
    IReadOnlyList<KanjiItem> Kanji,
    IReadOnlyList<VocabItem> Vocab,
    IReadOnlyList<SentenceItem> Sentences);

/// <summary>A question to create; <see cref="Source"/> and <see cref="License"/> come from the reference item (BR-SOAL-01).</summary>
public sealed record GeneratedQuestion(
    GeneratorTemplate Template,
    QuestionKind Kind,
    Guid LevelId,
    Guid ChapterId,
    Category Category,
    IReadOnlyList<string> Tags,
    TextBlock Prompt,
    object Payload,
    RefItemType ItemType,
    Guid ItemId,
    string Source,
    string License);

/// <summary>
/// BR-SOAL-05: questions from reference data. Distractors come from other items of the same level and category,
/// nearest chapters first. Everything is deterministic for the same data so a re-run produces the same questions;
/// the caller skips template + item pairs that already exist.
/// </summary>
public static class QuestionGenerator
{
    public const int DistractorCount = 3;

    // Prompts are Indonesian and English only: the generator never invents Japanese text (AGENTS.md §9).
    private static readonly Dictionary<GeneratorTemplate, TextBlock> Prompts = new()
    {
        [GeneratorTemplate.KanaToRomaji] = new("Pilih romaji untuk kana ini.", "Choose the romaji for this kana.", null),
        [GeneratorTemplate.RomajiToKana] = new("Pilih kana untuk romaji ini.", "Choose the kana for this romaji.", null),
        [GeneratorTemplate.KanaTypeRomaji] = new("Ketik romaji untuk kana ini.", "Type the romaji for this kana.", null),
        [GeneratorTemplate.VocabToMeaning] = new("Pilih arti kata ini.", "Choose the meaning of this word.", null),
        [GeneratorTemplate.MeaningToVocab] = new("Pilih kata untuk arti ini.", "Choose the word for this meaning.", null),
        [GeneratorTemplate.VocabTypeReading] = new("Ketik cara baca kata ini dengan kana.", "Type the reading of this word in kana.", null),
        [GeneratorTemplate.KanjiToReading] = new("Pilih cara baca kanji ini.", "Choose a reading of this kanji.", null),
        [GeneratorTemplate.KanjiToMeaning] = new("Pilih arti kanji ini.", "Choose the meaning of this kanji.", null),
        [GeneratorTemplate.SentenceTap] = new("Susun kalimat yang sesuai dengan terjemahan.", "Build the sentence that matches the translation.", null),
    };

    public static string TagOf(GeneratorTemplate template) => $"gen:{EnumSnakeCase.ToValue(template)}";

    public static TextBlock PromptOf(GeneratorTemplate template) => Prompts[template];

    /// <param name="chapterOrder">Position of each chapter in its book, for "nearest chapter" distractors.</param>
    public static IReadOnlyList<GeneratedQuestion> Generate(
        GeneratorInput input,
        IReadOnlySet<GeneratorTemplate> templates,
        IReadOnlySet<Guid> chapterIds,
        IReadOnlyDictionary<Guid, int> chapterOrder)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(chapterIds);
        ArgumentNullException.ThrowIfNull(chapterOrder);
        var result = new List<GeneratedQuestion>();
        bool Wanted(Guid? chapterId) => chapterId is { } id && (chapterIds.Count == 0 || chapterIds.Contains(id));
        int Distance(Guid? a, Guid? b) =>
            Math.Abs(chapterOrder.GetValueOrDefault(a ?? Guid.Empty, int.MaxValue / 4) - chapterOrder.GetValueOrDefault(b ?? Guid.Empty, int.MaxValue / 4));

        foreach (var kana in input.Kana.Where(item => Wanted(item.ChapterId)))
        {
            var category = kana.Script == "katakana" ? Category.Katakana : Category.Hiragana;
            var sameScript = input.Kana.Where(other => other.Id != kana.Id && other.Script == kana.Script).ToList();
            var tags = new List<string> { $"kana_row:{kana.RowKey}" };
            var character = new TextBlock(null, null, [new JaSegment(kana.Character)]);

            if (templates.Contains(GeneratorTemplate.KanaToRomaji))
            {
                var distractors = Distractors(sameScript, kana, other => other.Romaji, kana.Romaji, item => item.ChapterId, Distance, item => item.Id);
                AddMcq(result, GeneratorTemplate.KanaToRomaji, kana.Id, kana.LevelId, kana.ChapterId!.Value, category, tags, character,
                    kana.Romaji, distractors, text => new TextBlock(text, null, null), JaDisplay.NoRomaji, RefItemType.Kana, kana.Source, kana.License);
            }

            if (templates.Contains(GeneratorTemplate.RomajiToKana))
            {
                var distractors = Distractors(sameScript, kana, other => other.Character, kana.Character, item => item.ChapterId, Distance, item => item.Id);
                AddMcq(result, GeneratorTemplate.RomajiToKana, kana.Id, kana.LevelId, kana.ChapterId!.Value, category, tags,
                    new TextBlock(kana.Romaji, null, null), kana.Character, distractors,
                    text => new TextBlock(null, null, [new JaSegment(text)]), JaDisplay.NoRomaji, RefItemType.Kana, kana.Source, kana.License);
            }

            if (templates.Contains(GeneratorTemplate.KanaTypeRomaji))
            {
                result.Add(new GeneratedQuestion(
                    GeneratorTemplate.KanaTypeRomaji, QuestionKind.TypeAnswer, kana.LevelId, kana.ChapterId!.Value, category,
                    WithTemplate(tags, GeneratorTemplate.KanaTypeRomaji), PromptOf(GeneratorTemplate.KanaTypeRomaji),
                    new TypeAnswerPayload(character, [kana.Romaji], AnswerScript.Romaji, null, JaDisplay.NoRomaji),
                    RefItemType.Kana, kana.Id, kana.Source, kana.License));
            }
        }

        foreach (var vocab in input.Vocab.Where(item => Wanted(item.ChapterId)))
        {
            var meaning = Meaning(vocab.MeaningsId, vocab.MeaningsEn);
            var word = WordBlock(vocab.Word, vocab.Reading);
            var others = input.Vocab.Where(other => other.Id != vocab.Id).ToList();
            var tags = vocab.Tags.ToList();

            if (meaning is not null && templates.Contains(GeneratorTemplate.VocabToMeaning))
            {
                var distractors = Distractors(others.Where(other => Meaning(other.MeaningsId, other.MeaningsEn) is not null).ToList(), vocab,
                    other => other.MeaningsId[0], vocab.MeaningsId[0], item => item.ChapterId, Distance, item => item.Id);
                var byText = others.Where(other => other.MeaningsId.Count > 0).GroupBy(other => other.MeaningsId[0]).ToDictionary(group => group.Key, group => group.First());
                AddMcq(result, GeneratorTemplate.VocabToMeaning, vocab.Id, vocab.LevelId, vocab.ChapterId!.Value, Category.Vocab, tags, word,
                    vocab.MeaningsId[0], distractors,
                    text => text == vocab.MeaningsId[0] ? meaning : Meaning(byText[text].MeaningsId, byText[text].MeaningsEn)!,
                    JaDisplay.UserPref, RefItemType.Vocab, vocab.Source, vocab.License);
            }

            if (meaning is not null && templates.Contains(GeneratorTemplate.MeaningToVocab))
            {
                var distractors = Distractors(others, vocab, other => other.Word, vocab.Word, item => item.ChapterId, Distance, item => item.Id);
                var byWord = others.GroupBy(other => other.Word).ToDictionary(group => group.Key, group => group.First());
                AddMcq(result, GeneratorTemplate.MeaningToVocab, vocab.Id, vocab.LevelId, vocab.ChapterId!.Value, Category.Vocab, tags, meaning,
                    vocab.Word, distractors,
                    text => text == vocab.Word ? word : WordBlock(byWord[text].Word, byWord[text].Reading),
                    JaDisplay.UserPref, RefItemType.Vocab, vocab.Source, vocab.License);
            }

            if (JapaneseScript.ContainsKanji(vocab.Word) && JapaneseScript.IsKana(vocab.Reading) && templates.Contains(GeneratorTemplate.VocabTypeReading))
            {
                result.Add(new GeneratedQuestion(
                    GeneratorTemplate.VocabTypeReading, QuestionKind.TypeAnswer, vocab.LevelId, vocab.ChapterId!.Value, Category.Vocab,
                    WithTemplate(tags, GeneratorTemplate.VocabTypeReading), PromptOf(GeneratorTemplate.VocabTypeReading),
                    new TypeAnswerPayload(word, [vocab.Reading], AnswerScript.Kana, true, JaDisplay.Plain),
                    RefItemType.Vocab, vocab.Id, vocab.Source, vocab.License));
            }
        }

        foreach (var kanji in input.Kanji.Where(item => Wanted(item.ChapterId)))
        {
            var others = input.Kanji.Where(other => other.Id != kanji.Id).ToList();
            var (reading, fromKun) = MainReading(kanji);
            if (reading is null)
            {
                continue;
            }

            var stimulus = new TextBlock(null, null, [new JaSegment(kanji.Character, reading)]);
            var allReadings = kanji.Kunyomi.Select(Stem).Concat(kanji.Onyomi).ToHashSet(StringComparer.Ordinal);

            if (templates.Contains(GeneratorTemplate.KanjiToReading))
            {
                var pool = others
                    .Select(other => (Item: other, Reading: fromKun ? other.Kunyomi.Select(Stem).FirstOrDefault(r => r.Length > 0) : FirstOrNull(other.Onyomi)))
                    .Where(entry => entry.Reading is not null && !allReadings.Contains(entry.Reading))
                    .ToList();
                var distractors = pool
                    .OrderBy(entry => Distance(entry.Item.ChapterId, kanji.ChapterId))
                    .ThenBy(entry => Mix(entry.Item.Id, kanji.Id))
                    .Select(entry => entry.Reading!)
                    .Distinct(StringComparer.Ordinal)
                    .Take(DistractorCount)
                    .ToList();
                AddMcq(result, GeneratorTemplate.KanjiToReading, kanji.Id, kanji.LevelId, kanji.ChapterId!.Value, Category.Kanji, [], stimulus,
                    reading, distractors, text => new TextBlock(null, null, [new JaSegment(text)]), JaDisplay.Plain, RefItemType.Kanji, kanji.Source, kanji.License);
            }

            var meaning = Meaning(kanji.MeaningsId, kanji.MeaningsEn);
            if (meaning is not null && templates.Contains(GeneratorTemplate.KanjiToMeaning))
            {
                var withMeaning = others.Where(other => other.MeaningsId.Count > 0).ToList();
                var byText = withMeaning.GroupBy(other => other.MeaningsId[0]).ToDictionary(group => group.Key, group => group.First());
                var distractors = Distractors(withMeaning, kanji, other => other.MeaningsId[0], kanji.MeaningsId[0], item => item.ChapterId, Distance, item => item.Id);
                AddMcq(result, GeneratorTemplate.KanjiToMeaning, kanji.Id, kanji.LevelId, kanji.ChapterId!.Value, Category.Kanji, [], stimulus,
                    kanji.MeaningsId[0], distractors,
                    text => text == kanji.MeaningsId[0] ? meaning : Meaning(byText[text].MeaningsId, byText[text].MeaningsEn)!,
                    JaDisplay.Plain, RefItemType.Kanji, kanji.Source, kanji.License);
            }
        }

        if (templates.Contains(GeneratorTemplate.SentenceTap))
        {
            foreach (var sentence in input.Sentences.Where(item => Wanted(item.ChapterId)))
            {
                var words = Words(sentence.Text.Japanese ?? []);
                // Repeated words would need several accepted orders; such sentences are left to the staff editor.
                if (words.Count < 2 || words.Count > 20 || string.IsNullOrWhiteSpace(sentence.Text.Indonesian)
                    || words.Select(FuriganaMarkup.PlainText).Distinct(StringComparer.Ordinal).Count() != words.Count)
                {
                    continue;
                }

                result.Add(new GeneratedQuestion(
                    GeneratorTemplate.SentenceTap, QuestionKind.TapSentence, sentence.LevelId, sentence.ChapterId!.Value, Category.Sentence,
                    WithTemplate(sentence.Tags, GeneratorTemplate.SentenceTap), PromptOf(GeneratorTemplate.SentenceTap),
                    new TapSentencePayload(
                        new TextBlock(sentence.Text.Indonesian, sentence.Text.English, null),
                        [.. words.Select(segments => new TextBlock(null, null, segments))],
                        [[.. Enumerable.Range(0, words.Count)]],
                        JaDisplay.UserPref),
                    RefItemType.Sentence, sentence.Id, sentence.Source, sentence.License));
            }
        }

        // Defensive: only payloads that pass the same validation as the staff editor.
        return [.. result.Where(question => Payloads.Validate(question.Kind, Payloads.ToJson(question.Payload)).Count == 0)];
    }

    private static void AddMcq(
        List<GeneratedQuestion> result, GeneratorTemplate template, Guid itemId, Guid levelId, Guid chapterId, Category category,
        IReadOnlyList<string> tags, TextBlock stimulus, string correct, List<string> distractors, Func<string, TextBlock> toChoice,
        JaDisplay jaDisplay, RefItemType itemType, string source, string license)
    {
        if (distractors.Count == 0)
        {
            return;
        }

        // The correct choice sits at a position derived from the item, so payloads differ but re-runs are identical.
        var position = (int)(Mix(itemId, Guid.Empty) % (ulong)(distractors.Count + 1));
        var texts = distractors.ToList();
        texts.Insert(position, correct);
        result.Add(new GeneratedQuestion(
            template, QuestionKind.Mcq, levelId, chapterId, category, WithTemplate(tags, template), PromptOf(template),
            new McqPayload(stimulus, [.. texts.Select(toChoice)], [position], false, jaDisplay),
            itemType, itemId, source, license));
    }

    private static List<string> Distractors<T>(
        IReadOnlyList<T> others, T item, Func<T, string> text, string correct, Func<T, Guid?> chapter, Func<Guid?, Guid?, int> distance, Func<T, Guid> id) =>
        [
            .. others
                .Where(other => !string.IsNullOrWhiteSpace(text(other)) && !string.Equals(text(other), correct, StringComparison.Ordinal))
                .OrderBy(other => distance(chapter(other), chapter(item)))
                .ThenBy(other => Mix(id(other), id(item)))
                .Select(text)
                .Distinct(StringComparer.Ordinal)
                .Take(DistractorCount),
        ];

    private static IReadOnlyList<string> WithTemplate(IEnumerable<string> tags, GeneratorTemplate template) =>
        [.. tags.Where(tag => !QuestionRules.IsInternalTag(tag)).Append(TagOf(template)).Distinct(StringComparer.Ordinal).Take(Question.MaxTags)];

    private static TextBlock? Meaning(IReadOnlyList<string> indonesian, IReadOnlyList<string> english) =>
        indonesian.Count == 0 || string.IsNullOrWhiteSpace(indonesian[0]) ? null : new TextBlock(indonesian[0], FirstOrNull(english), null);

    private static string? FirstOrNull(IReadOnlyList<string> values) => values.Count > 0 ? values[0] : null;

    private static TextBlock WordBlock(string word, string reading) =>
        new(null, null, [new JaSegment(word, JapaneseScript.ContainsKanji(word) ? reading : null)]);

    private static string Stem(string kunyomi) => kunyomi.Split('.')[0];

    /// <summary>The kanji's own kun reading (before the okurigana dot) when there is one, else its first on reading.</summary>
    private static (string? Reading, bool FromKun) MainReading(KanjiItem kanji)
    {
        var kun = kanji.Kunyomi.Select(Stem).FirstOrDefault(reading => reading.Length > 0);
        return kun is not null ? (kun, true) : (FirstOrNull(kanji.Onyomi), false);
    }

    /// <summary>Japanese segments split at word boundaries (" " segments, docs/06 §3).</summary>
    private static List<IReadOnlyList<JaSegment>> Words(IReadOnlyList<JaSegment> segments)
    {
        var words = new List<IReadOnlyList<JaSegment>>();
        var current = new List<JaSegment>();
        foreach (var segment in segments)
        {
            if (segment.Text == " ")
            {
                if (current.Count > 0)
                {
                    words.Add(current);
                    current = [];
                }

                continue;
            }

            current.Add(segment);
        }

        if (current.Count > 0)
        {
            words.Add(current);
        }

        return words;
    }

    private static ulong Mix(Guid a, Guid b) => unchecked((ulong)(uint)SafeQuestions.StableSeed(a, b));
}
