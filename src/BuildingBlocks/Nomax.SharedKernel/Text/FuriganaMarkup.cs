using System.Text;

namespace Nomax.SharedKernel.Text;

/// <summary>A problem found while parsing furigana markup; <see cref="Index"/> is the character position in the markup.</summary>
public sealed record MarkupIssue(string Code, int Index, string? Text = null);

public sealed record MarkupParseResult(
    IReadOnlyList<JaSegment> Segments,
    IReadOnlyList<MarkupIssue> Errors,
    IReadOnlyList<MarkupIssue> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// The one-line furigana markup of docs/06 §3, with the same rules as the frontend parser (web/src/i18n/furigana.ts):
/// <c>{漢字|かんじ}</c>, <c>{は||wa}</c>, <c>{日本|にほん|nihon}</c>, plain text, a space as word boundary, and the escapes
/// <c>\{ \} \| \\</c>. UI label placeholders (<c>{{n}}</c>) do not exist in content and are rejected.
/// </summary>
public static class FuriganaMarkup
{
    public const string UnclosedBrace = "unclosed_brace";
    public const string UnexpectedClosingBrace = "unexpected_closing_brace";
    public const string EmptyBase = "empty_base";
    public const string TooManySeparators = "too_many_separators";
    public const string InvalidReading = "invalid_reading";
    public const string KanjiWithoutReading = "kanji_without_reading";
    public const string ParticleWithoutRomaji = "particle_without_romaji";

    private const string Escapable = "{}|\\";

    public static MarkupParseResult Parse(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        var segments = new List<JaSegment>();
        var errors = new List<MarkupIssue>();
        var warnings = new List<MarkupIssue>();
        var plain = new StringBuilder();
        var plainStart = 0;

        var i = 0;
        while (i < markup.Length)
        {
            var c = markup[i];
            var next = i + 1 < markup.Length ? markup[i + 1] : '\0';

            if (c == '\\' && next != '\0' && Escapable.Contains(next, StringComparison.Ordinal))
            {
                if (plain.Length == 0)
                {
                    plainStart = i;
                }

                plain.Append(next);
                i += 2;
                continue;
            }

            if (c == '{')
            {
                var parts = new List<string>();
                var current = new StringBuilder();
                var j = i + 1;
                var closed = false;
                while (j < markup.Length)
                {
                    var d = markup[j];
                    var after = j + 1 < markup.Length ? markup[j + 1] : '\0';
                    if (d == '\\' && after != '\0' && Escapable.Contains(after, StringComparison.Ordinal))
                    {
                        current.Append(after);
                        j += 2;
                        continue;
                    }

                    if (d == '}')
                    {
                        closed = true;
                        break;
                    }

                    if (d == '{')
                    {
                        break;
                    }

                    if (d == '|')
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(d);
                    }

                    j++;
                }

                parts.Add(current.ToString());
                if (!closed)
                {
                    errors.Add(new MarkupIssue(UnclosedBrace, i));
                    break;
                }

                FlushPlain();
                var text = parts[0];
                var reading = parts.Count > 1 ? parts[1] : string.Empty;
                var romaji = parts.Count > 2 ? parts[2] : string.Empty;
                if (parts.Count > 3)
                {
                    errors.Add(new MarkupIssue(TooManySeparators, i, text));
                }

                if (text.Length == 0)
                {
                    errors.Add(new MarkupIssue(EmptyBase, i));
                }

                if (reading.Length > 0 && !JapaneseScript.IsReading(reading))
                {
                    errors.Add(new MarkupIssue(InvalidReading, i, reading));
                }

                if (reading.Length == 0 && JapaneseScript.ContainsKanji(text))
                {
                    errors.Add(new MarkupIssue(KanjiWithoutReading, i, text));
                }

                segments.Add(new JaSegment(text, reading.Length > 0 ? reading : null, romaji.Length > 0 ? romaji : null));
                i = j + 1;
                continue;
            }

            if (c == '}')
            {
                errors.Add(new MarkupIssue(UnexpectedClosingBrace, i));
                i++;
                continue;
            }

            if (c == ' ')
            {
                FlushPlain();
                if (segments.Count == 0 || segments[^1].Text != " ")
                {
                    segments.Add(new JaSegment(" "));
                }

                i++;
                continue;
            }

            if (plain.Length == 0)
            {
                plainStart = i;
            }

            plain.Append(c);
            i++;
        }

        FlushPlain();
        return new MarkupParseResult(segments, errors, warnings);

        void FlushPlain()
        {
            if (plain.Length == 0)
            {
                return;
            }

            var text = plain.ToString();
            segments.Add(new JaSegment(text));
            // Rule 3: kanji always need a reading, otherwise the kana and furigana modes cannot work.
            if (JapaneseScript.ContainsKanji(text))
            {
                errors.Add(new MarkupIssue(KanjiWithoutReading, plainStart, text));
            }

            if (text is "は" or "へ" or "を")
            {
                warnings.Add(new MarkupIssue(ParticleWithoutRomaji, plainStart, text));
            }

            plain.Clear();
        }
    }

    /// <summary>Japanese text as displayed: segments joined without the word-boundary spaces.</summary>
    public static string PlainText(IEnumerable<JaSegment> segments) =>
        string.Concat((segments ?? []).Where(segment => segment.Text != " ").Select(segment => segment.Text));
}

/// <summary>Character classes used by the text rules (docs/06 §3).</summary>
public static class JapaneseScript
{
    /// <summary>CJK ideographs (incl. extension A and compatibility) and 々 〆 ヶ.</summary>
    public static bool IsKanji(char c) =>
        c is (>= '\u3400' and <= '\u4DBF') or (>= '\u4E00' and <= '\u9FFF') or (>= '\uF900' and <= '\uFAFF')
            or '々' or '〆' or 'ヶ';

    public static bool ContainsKanji(string text) => (text ?? string.Empty).Any(IsKanji);

    /// <summary>Hiragana, katakana and ー only (rule 2).</summary>
    public static bool IsReading(string text) =>
        !string.IsNullOrEmpty(text) && text.All(c =>
            c is (>= '\u3041' and <= '\u3096') or '\u309D' or '\u309E' or (>= '\u30A1' and <= '\u30FA') or (>= '\u30FC' and <= '\u30FE'));

    public static bool IsKana(string text) => IsReading(text);
}
