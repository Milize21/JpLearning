using System.Globalization;
using System.Text.Json;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Domain;

/// <summary>A cell that blocks an import. <see cref="Row"/> counts like a spreadsheet (header = row 1; JSON: item 1 = row 2).</summary>
public sealed record ImportCellError(int Row, string Column, string Code);

/// <summary>One data row with its cells by column name (trimmed; empty cells absent).</summary>
public sealed record RawImportRow(int Row, IReadOnlyDictionary<string, string> Cells)
{
    public string? Get(string column) => Cells.TryGetValue(column, out var value) ? value : null;
}

public sealed record KanaImportRow(int Row, string Character, KanaScript Script, string Romaji, string RowKey, KanaVariant Variant, int SortOrder, int? Chapter);

public sealed record KanjiImportRow(
    int Row, string Character, IReadOnlyList<string> Onyomi, IReadOnlyList<string> Kunyomi, Meanings Meanings, short? StrokeCount, string? KanjivgCode, int? Chapter);

public sealed record VocabImportRow(
    int Row, string Word, string Reading, Meanings Meanings, string? Pos, IReadOnlyList<string> Tags, long? JmdictSeq, int? Chapter);

public sealed record GrammarImportRow(int Row, string Pattern, TextBlock Title, RichText? Explanation, IReadOnlyList<string> Tags, int? Chapter);

public sealed record SentenceImportRow(int Row, TextBlock Text, IReadOnlyList<string> Tags, string? GrammarPattern, long? TatoebaId, int? Chapter);

/// <summary>
/// Reference data import (docs/05 §5 <c>POST /reference/{type}/import</c>): CSV (same reader as the account import) or a
/// JSON array of objects with the same keys. Every row is validated before anything is written. Lists inside a cell are
/// separated by <c>|</c>; Japanese text uses the furigana markup of docs/06 §3. The optional <c>chapter</c> column holds a
/// chapter number of the book chosen in the request.
/// </summary>
public static class ReferenceImport
{
    public const int MaxRows = 2000;
    public const char ListSeparator = '|';
    public const string ChapterColumn = "chapter";

    public static class Codes
    {
        public const string EmptyFile = "empty_file";
        public const string MissingColumn = "missing_column";
        public const string UnknownColumn = "unknown_column";
        public const string TooManyRows = "too_many_rows";
        public const string Required = "required";
        public const string InvalidFormat = "invalid_format";
        public const string TooLong = "too_long";
        public const string InvalidValue = "invalid_value";
        public const string DuplicateInFile = "duplicate_in_file";
        public const string UnknownChapter = "unknown_chapter";
        public const string UnknownGrammar = "unknown_grammar";
    }

    public static IReadOnlyList<string> ColumnsOf(ReferenceType type) => type switch
    {
        ReferenceType.Kana => ["char", "script", "romaji", "row_key", "variant", "sort_order", ChapterColumn],
        ReferenceType.Kanji => ["character", "onyomi", "kunyomi", "meanings_id", "meanings_en", "stroke_count", "kanjivg_code", ChapterColumn],
        ReferenceType.Vocab => ["word", "reading", "meanings_id", "meanings_en", "pos", "tags", "jmdict_seq", ChapterColumn],
        ReferenceType.Grammar => ["pattern", "title_id", "title_en", "title_ja", "explanation_id", "explanation_en", "tags", ChapterColumn],
        ReferenceType.Sentences => ["text_id", "text_en", "text_ja", "tags", "grammar_pattern", "tatoeba_id", ChapterColumn],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static IReadOnlyList<string> RequiredOf(ReferenceType type) => type switch
    {
        ReferenceType.Kana => ["char", "script", "romaji", "row_key", "variant"],
        ReferenceType.Kanji => ["character", "meanings_id"],
        ReferenceType.Vocab => ["word", "reading", "meanings_id"],
        ReferenceType.Grammar => ["pattern", "title_id"],
        ReferenceType.Sentences => ["text_id", "text_ja"],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>Reads CSV or JSON (detected by the first non-blank character) into raw rows; structural errors go to <paramref name="errors"/>.</summary>
    public static IReadOnlyList<RawImportRow> Read(string text, ReferenceType type, List<ImportCellError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var trimmed = (text ?? string.Empty).TrimStart('﻿', ' ', '\t', '\r', '\n');
        var (rows, header) = trimmed.StartsWith('[') ? (ReadJson(trimmed, errors), []) : ReadCsv(trimmed);
        if (errors.Count > 0)
        {
            return [];
        }

        if (rows.Count == 0)
        {
            errors.Add(new ImportCellError(1, string.Empty, Codes.EmptyFile));
            return [];
        }

        if (rows.Count > MaxRows)
        {
            errors.Add(new ImportCellError(1, string.Empty, Codes.TooManyRows));
            return [];
        }

        var known = ColumnsOf(type);
        // A CSV header counts as present even when every cell of the column is empty.
        var present = rows.SelectMany(row => row.Cells.Keys).Concat(header).Distinct(StringComparer.Ordinal).ToList();
        foreach (var column in present.Where(column => !known.Contains(column, StringComparer.Ordinal)))
        {
            errors.Add(new ImportCellError(1, column, Codes.UnknownColumn));
        }

        foreach (var column in RequiredOf(type).Where(column => !present.Contains(column, StringComparer.Ordinal)))
        {
            errors.Add(new ImportCellError(1, column, Codes.MissingColumn));
        }

        return errors.Count > 0 ? [] : rows;
    }

    private static (List<RawImportRow> Rows, List<string> Header) ReadCsv(string text)
    {
        var records = CsvReader.Read(text);
        if (records.Count == 0)
        {
            return ([], []);
        }

        var header = records[0].Select(column => column.Trim().ToLowerInvariant()).ToList();
        var rows = new List<RawImportRow>();
        for (var i = 1; i < records.Count; i++)
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var c = 0; c < header.Count && c < records[i].Count; c++)
            {
                var value = records[i][c].Trim();
                if (header[c].Length > 0 && value.Length > 0)
                {
                    cells[header[c]] = value;
                }
            }

            rows.Add(new RawImportRow(i + 1, cells));
        }

        return (rows, [.. header.Where(column => column.Length > 0)]);
    }

    private static List<RawImportRow> ReadJson(string text, List<ImportCellError> errors)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            errors.Add(new ImportCellError(1, string.Empty, Codes.InvalidFormat));
            return [];
        }

        using (document)
        {
            var rows = new List<RawImportRow>();
            var index = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                index++;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new ImportCellError(index + 1, string.Empty, Codes.InvalidFormat));
                    continue;
                }

                var cells = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in item.EnumerateObject())
                {
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString()!.Trim(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.Array => string.Join(ListSeparator, property.Value.EnumerateArray().Select(element => element.ToString().Trim())),
                        JsonValueKind.Null => string.Empty,
                        _ => property.Value.GetRawText(),
                    };
                    if (value.Length > 0)
                    {
                        cells[property.Name.ToLowerInvariant()] = value;
                    }
                }

                rows.Add(new RawImportRow(index + 1, cells));
            }

            return rows;
        }
    }

    public static IReadOnlyList<KanaImportRow> ParseKana(IReadOnlyList<RawImportRow> rows, List<ImportCellError> errors)
    {
        var result = new List<KanaImportRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = new Cells(row, errors);
            var character = cells.Required("char", 4);
            var script = cells.Enum<KanaScript>("script", required: true);
            var romaji = cells.Required("romaji", 8);
            var rowKey = cells.Required("row_key", 8);
            var variant = cells.Enum<KanaVariant>("variant", required: true);
            var sortOrder = cells.Int("sort_order") ?? 0;
            var chapter = cells.Int(ChapterColumn);
            if (character is not null && !JapaneseScript.IsKana(character))
            {
                cells.Fail("char", Codes.InvalidValue);
            }

            if (romaji is not null && !romaji.All(char.IsAsciiLetterLower))
            {
                cells.Fail("romaji", Codes.InvalidFormat);
            }

            if (cells.Ok && !seen.Add($"{script}:{character}"))
            {
                cells.Fail("char", Codes.DuplicateInFile);
            }

            if (cells.Ok)
            {
                result.Add(new KanaImportRow(row.Row, character!, script!.Value, romaji!, rowKey!, variant!.Value, sortOrder, chapter));
            }
        }

        return result;
    }

    public static IReadOnlyList<KanjiImportRow> ParseKanji(IReadOnlyList<RawImportRow> rows, List<ImportCellError> errors)
    {
        var result = new List<KanjiImportRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = new Cells(row, errors);
            var character = cells.Required("character", 4);
            var onyomi = cells.List("onyomi");
            var kunyomi = cells.List("kunyomi");
            var meanings = cells.Meanings();
            var strokes = cells.Int("stroke_count");
            var kanjivg = cells.Optional("kanjivg_code", 8);
            var chapter = cells.Int(ChapterColumn);
            if (character is not null && !JapaneseScript.ContainsKanji(character))
            {
                cells.Fail("character", Codes.InvalidValue);
            }

            if (onyomi.Any(reading => !JapaneseScript.IsReading(reading)))
            {
                cells.Fail("onyomi", Codes.InvalidFormat);
            }

            if (kunyomi.Any(reading => !JapaneseScript.IsReading(reading.Replace(".", string.Empty, StringComparison.Ordinal))))
            {
                cells.Fail("kunyomi", Codes.InvalidFormat);
            }

            if (strokes is < 1 or > 100)
            {
                cells.Fail("stroke_count", Codes.InvalidValue);
            }

            if (cells.Ok && !seen.Add(character!))
            {
                cells.Fail("character", Codes.DuplicateInFile);
            }

            if (cells.Ok)
            {
                result.Add(new KanjiImportRow(row.Row, character!, onyomi, kunyomi, meanings!, (short?)strokes, kanjivg, chapter));
            }
        }

        return result;
    }

    public static IReadOnlyList<VocabImportRow> ParseVocab(IReadOnlyList<RawImportRow> rows, List<ImportCellError> errors)
    {
        var result = new List<VocabImportRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = new Cells(row, errors);
            var word = cells.Required("word", 100);
            var reading = cells.Required("reading", 100);
            var meanings = cells.Meanings();
            var pos = cells.Optional("pos", 32);
            var tags = cells.List("tags");
            var jmdict = cells.Long("jmdict_seq");
            var chapter = cells.Int(ChapterColumn);
            if (reading is not null && !JapaneseScript.IsReading(reading))
            {
                cells.Fail("reading", Codes.InvalidFormat);
            }

            if (cells.Ok && !seen.Add($"{word}\u0000{reading}"))
            {
                cells.Fail("word", Codes.DuplicateInFile);
            }

            if (cells.Ok)
            {
                result.Add(new VocabImportRow(row.Row, word!, reading!, meanings!, pos, tags, jmdict, chapter));
            }
        }

        return result;
    }

    public static IReadOnlyList<GrammarImportRow> ParseGrammar(IReadOnlyList<RawImportRow> rows, List<ImportCellError> errors)
    {
        var result = new List<GrammarImportRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = new Cells(row, errors);
            var pattern = cells.Required("pattern", 100);
            var title = cells.TextBlock("title_id", "title_en", "title_ja", TextBlockUsage.Title);
            var explanationId = cells.Optional("explanation_id", TextRules.MaxRichTextLength);
            var explanationEn = cells.Optional("explanation_en", TextRules.MaxRichTextLength);
            var tags = cells.List("tags");
            var chapter = cells.Int(ChapterColumn);
            RichText? explanation = null;
            if (explanationId is not null)
            {
                explanation = new RichText(RichText.MarkdownFormat, explanationId, explanationEn);
                if (TextRules.Validate(explanation).Count > 0)
                {
                    cells.Fail("explanation_id", Codes.InvalidFormat);
                }
            }
            else if (explanationEn is not null)
            {
                cells.Fail("explanation_id", Codes.Required);
            }

            if (cells.Ok && !seen.Add(pattern!))
            {
                cells.Fail("pattern", Codes.DuplicateInFile);
            }

            if (cells.Ok)
            {
                result.Add(new GrammarImportRow(row.Row, pattern!, title!, explanation, tags, chapter));
            }
        }

        return result;
    }

    public static IReadOnlyList<SentenceImportRow> ParseSentences(IReadOnlyList<RawImportRow> rows, List<ImportCellError> errors)
    {
        var result = new List<SentenceImportRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var cells = new Cells(row, errors);
            var text = cells.TextBlock("text_id", "text_en", "text_ja", TextBlockUsage.Sentence);
            var tags = cells.List("tags");
            var grammar = cells.Optional("grammar_pattern", 100);
            var tatoeba = cells.Long("tatoeba_id");
            var chapter = cells.Int(ChapterColumn);
            if (cells.Ok && !seen.Add(FuriganaMarkup.PlainText(text!.Japanese ?? [])))
            {
                cells.Fail("text_ja", Codes.DuplicateInFile);
            }

            if (cells.Ok)
            {
                result.Add(new SentenceImportRow(row.Row, text!, tags, grammar, tatoeba, chapter));
            }
        }

        return result;
    }

    /// <summary>Typed access to the cells of one row; every failure is recorded once per cell.</summary>
    private sealed class Cells(RawImportRow row, List<ImportCellError> errors)
    {
        public bool Ok { get; private set; } = true;

        public void Fail(string column, string code)
        {
            Ok = false;
            errors.Add(new ImportCellError(row.Row, column, code));
        }

        public string? Required(string column, int maxLength)
        {
            var value = row.Get(column);
            if (value is null)
            {
                Fail(column, Codes.Required);
                return null;
            }

            return CheckLength(column, value, maxLength);
        }

        public string? Optional(string column, int maxLength)
        {
            var value = row.Get(column);
            return value is null ? null : CheckLength(column, value, maxLength);
        }

        public TEnum? Enum<TEnum>(string column, bool required)
            where TEnum : struct, System.Enum
        {
            var value = row.Get(column);
            if (value is null)
            {
                if (required)
                {
                    Fail(column, Codes.Required);
                }

                return null;
            }

            if (EnumSnakeCase.TryParse<TEnum>(value.ToLowerInvariant(), out var parsed))
            {
                return parsed;
            }

            Fail(column, Codes.InvalidValue);
            return null;
        }

        public int? Int(string column)
        {
            var value = row.Get(column);
            if (value is null)
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            {
                return parsed;
            }

            Fail(column, Codes.InvalidFormat);
            return null;
        }

        public long? Long(string column)
        {
            var value = row.Get(column);
            if (value is null)
            {
                return null;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                return parsed;
            }

            Fail(column, Codes.InvalidFormat);
            return null;
        }

        public string[] List(string column)
        {
            var value = row.Get(column);
            if (value is null)
            {
                return [];
            }

            var items = value.Split(ListSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (items.Any(item => item.Length > 100))
            {
                Fail(column, Codes.TooLong);
                return [];
            }

            return items;
        }

        public Meanings? Meanings()
        {
            var indonesian = List("meanings_id");
            var english = List("meanings_en");
            if (row.Get("meanings_id") is null)
            {
                Fail("meanings_id", Codes.Required);
                return null;
            }

            return new Meanings(indonesian, english);
        }

        public TextBlock? TextBlock(string idColumn, string enColumn, string jaColumn, TextBlockUsage usage)
        {
            var indonesian = Optional(idColumn, TextRules.MaxTextLength);
            var english = Optional(enColumn, TextRules.MaxTextLength);
            var markup = row.Get(jaColumn);
            IReadOnlyList<JaSegment>? japanese = null;
            if (markup is not null)
            {
                var parsed = FuriganaMarkup.Parse(markup);
                if (!parsed.IsValid)
                {
                    Fail(jaColumn, Codes.InvalidFormat);
                    return null;
                }

                japanese = parsed.Segments;
            }

            var block = new TextBlock(indonesian, english, japanese);
            foreach (var issue in TextRules.Validate(block, usage))
            {
                var column = issue.Path.StartsWith("ja", StringComparison.Ordinal) ? jaColumn
                    : issue.Path.StartsWith("en", StringComparison.Ordinal) ? enColumn : idColumn;
                Fail(column, issue.Message.StartsWith("Is required", StringComparison.Ordinal) ? Codes.Required : Codes.InvalidFormat);
                return null;
            }

            return block;
        }

        private string? CheckLength(string column, string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            Fail(column, Codes.TooLong);
            return null;
        }
    }
}
