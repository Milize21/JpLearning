using System.Text.Json.Serialization;

namespace Nomax.SharedKernel.Text;

/// <summary>
/// One segment of Japanese text (docs/03 §3.1, docs/06 §10). JSON keys are kept short because they are stored in jsonb.
/// A segment whose <see cref="Text"/> is a single ASCII space marks a word boundary for romaji and is not rendered.
/// </summary>
/// <param name="Text">Japanese text as written (kanji, kana, punctuation).</param>
/// <param name="Reading">Furigana in hiragana/katakana; only when <paramref name="Text"/> contains kanji.</param>
/// <param name="Romaji">Romaji override, mainly for particles (は → "wa").</param>
/// <param name="VocabId">Optional content.vocab id for "tap to see meaning".</param>
public sealed record JaSegment(
    [property: JsonPropertyName("t")] string Text,
    [property: JsonPropertyName("r"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reading = null,
    [property: JsonPropertyName("ro"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Romaji = null,
    [property: JsonPropertyName("v"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? VocabId = null);
