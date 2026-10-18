using System.Text.RegularExpressions;

namespace Nomax.SharedKernel.Text;

/// <summary>A rule violation inside a text value; <see cref="Path"/> is relative to the value, e.g. <c>ja[2].r</c>.</summary>
public sealed record TextIssue(string Path, string Message);

/// <summary>Which languages a <see cref="TextBlock"/> must contain where it is used (docs/03 §3.1 table).</summary>
public sealed record TextBlockUsage(bool RequireIndonesian, bool RequireEnglish, bool RequireJapanese, bool RequireIndonesianOrJapanese = false)
{
    /// <summary>Names and titles of content: Indonesian required.</summary>
    public static TextBlockUsage Title { get; } = new(true, false, false);

    /// <summary><c>questions.prompt</c>: Indonesian required.</summary>
    public static TextBlockUsage Prompt { get; } = new(true, false, false);

    /// <summary>Stimulus, options, tokens and cards of question payloads: Indonesian or Japanese.</summary>
    public static TextBlockUsage Stimulus { get; } = new(false, false, false, RequireIndonesianOrJapanese: true);

    /// <summary><c>sentences.text</c>: Indonesian and Japanese.</summary>
    public static TextBlockUsage Sentence { get; } = new(true, false, true);
}

/// <summary>
/// Validation of <see cref="TextBlock"/> (docs/03 §3.1) and <see cref="RichText"/> (§3.2), shared by every module so the
/// limits are the same everywhere. Japanese segments follow the furigana rules of docs/06 §3.
/// </summary>
public static partial class TextRules
{
    public const int MaxTextLength = 2000;
    public const int MaxSegments = 500;
    public const int MaxSegmentLength = 100;
    public const int MaxRichTextLength = 50_000;

    public static IReadOnlyList<TextIssue> Validate(TextBlock? block, TextBlockUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var issues = new List<TextIssue>();
        if (block is null)
        {
            issues.Add(new TextIssue(string.Empty, "Is required."));
            return issues;
        }

        CheckLanguage(block.Indonesian, "id", usage.RequireIndonesian, issues);
        CheckLanguage(block.English, "en", usage.RequireEnglish, issues);

        var japanese = block.Japanese ?? [];
        if (usage.RequireJapanese && japanese.All(segment => segment.Text == " "))
        {
            issues.Add(new TextIssue("ja", "Is required."));
        }

        if (usage.RequireIndonesianOrJapanese && string.IsNullOrWhiteSpace(block.Indonesian) && japanese.Count == 0)
        {
            issues.Add(new TextIssue("id", "Indonesian or Japanese text is required."));
        }

        if (japanese.Count > MaxSegments)
        {
            issues.Add(new TextIssue("ja", $"At most {MaxSegments} segments."));
        }

        for (var i = 0; i < japanese.Count && i < MaxSegments; i++)
        {
            CheckSegment(japanese[i], $"ja[{i}]", issues);
        }

        return issues;
    }

    public static IReadOnlyList<TextIssue> Validate(RichText? text)
    {
        var issues = new List<TextIssue>();
        if (text is null)
        {
            issues.Add(new TextIssue(string.Empty, "Is required."));
            return issues;
        }

        if (text.Format != RichText.MarkdownFormat)
        {
            issues.Add(new TextIssue("format", "Must be \"md\"."));
        }

        if (string.IsNullOrWhiteSpace(text.Indonesian))
        {
            issues.Add(new TextIssue("id", "Is required."));
        }
        else
        {
            CheckMarkdown(text.Indonesian, "id", issues);
        }

        if (text.English is not null)
        {
            CheckMarkdown(text.English, "en", issues);
        }

        return issues;
    }

    /// <summary>Only <c>https://</c> links (BR-KONTEN-05, BR-JADWAL-01).</summary>
    public static bool IsHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);

    private static void CheckLanguage(string? value, string path, bool required, List<TextIssue> issues)
    {
        if (required && string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new TextIssue(path, "Is required."));
        }
        else if (value is not null && value.Length > MaxTextLength)
        {
            issues.Add(new TextIssue(path, $"At most {MaxTextLength} characters."));
        }
    }

    private static void CheckSegment(JaSegment segment, string path, List<TextIssue> issues)
    {
        if (string.IsNullOrEmpty(segment.Text))
        {
            issues.Add(new TextIssue($"{path}.t", "Is required."));
            return;
        }

        if (segment.Text.Length > MaxSegmentLength)
        {
            issues.Add(new TextIssue($"{path}.t", $"At most {MaxSegmentLength} characters."));
        }

        var hasKanji = JapaneseScript.ContainsKanji(segment.Text);
        if (segment.Reading is { } reading)
        {
            if (reading.Length > MaxSegmentLength || !JapaneseScript.IsReading(reading))
            {
                issues.Add(new TextIssue($"{path}.r", "Must be hiragana or katakana (at most 100 characters)."));
            }
            else if (!hasKanji)
            {
                issues.Add(new TextIssue($"{path}.r", "Only segments with kanji have a reading."));
            }
        }
        else if (hasKanji)
        {
            issues.Add(new TextIssue($"{path}.r", "Segments with kanji need a reading."));
        }

        if (segment.Romaji is { } romaji && !RomajiPattern().IsMatch(romaji))
        {
            issues.Add(new TextIssue($"{path}.ro", "Only lowercase a–z, apostrophe, hyphen and space."));
        }
    }

    private static void CheckMarkdown(string markdown, string path, List<TextIssue> issues)
    {
        if (markdown.Length > MaxRichTextLength)
        {
            issues.Add(new TextIssue(path, $"At most {MaxRichTextLength} characters."));
        }

        if (RawHtml().IsMatch(markdown))
        {
            issues.Add(new TextIssue(path, "Raw HTML is not allowed."));
        }

        if (InlineImage().IsMatch(markdown))
        {
            issues.Add(new TextIssue(path, "Inline images are not allowed; upload the image as a file material."));
        }

        foreach (Match link in MarkdownLink().Matches(markdown))
        {
            if (!IsHttpsUrl(link.Groups["url"].Value))
            {
                issues.Add(new TextIssue(path, "Links must start with https://."));
                break;
            }
        }
    }

    [GeneratedRegex("^[a-z' -]+$")]
    private static partial Regex RomajiPattern();

    // A tag-like "<" (letter, "/", "!" or "?") is HTML; "<" before a space or digit stays plain text (e.g. "x < 3").
    [GeneratedRegex(@"<[A-Za-z/!?]")]
    private static partial Regex RawHtml();

    [GeneratedRegex(@"!\[[^\]]*\]\(")]
    private static partial Regex InlineImage();

    [GeneratedRegex(@"\[[^\]]*\]\((?<url>[^)\s]*)")]
    private static partial Regex MarkdownLink();
}
