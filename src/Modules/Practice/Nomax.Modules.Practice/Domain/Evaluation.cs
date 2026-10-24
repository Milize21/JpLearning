using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Domain;

// Answer shapes of docs/03 §8.2.

public sealed record McqAnswer(IReadOnlyList<int> Selected);

public sealed record TapAnswer(IReadOnlyList<int> Order);

/// <param name="Raw">What was typed before the romaji→kana conversion of the client.</param>
public sealed record TypedAnswer(string Text, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Raw);

/// <param name="Pairs">Left index → right index.</param>
public sealed record MatchAnswer(IReadOnlyList<IReadOnlyList<int>> Pairs);

public sealed record FlashcardAnswer(bool Remembered);

public sealed record HandwriteAnswer(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>>? Strokes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Score,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? SelfCheck);

public sealed record LongTextAnswer(string Text);

/// <summary>Result of evaluating one answer. <see cref="CorrectAnswer"/> is the key, sent only after answering.</summary>
public sealed record Evaluation(bool IsCorrect, InputMode InputMode, JsonElement CorrectAnswer);

/// <summary>BR-DRILL-06 normalization of typed answers.</summary>
public static class AnswerNormalization
{
    private const string TrailingPunctuation = "。．.！!？?";

    public static string Normalize(string text, AnswerScript script, bool kanaInsensitive)
    {
        ArgumentNullException.ThrowIfNull(text);

        // 1. NFKC, trim, single spaces. NFKC also turns the full-width space into a plain one.
        var value = CollapseSpaces(text.Normalize(NormalizationForm.FormKC).Replace('　', ' ').Trim());

        // 2. Trailing punctuation goes.
        value = value.TrimEnd(TrailingPunctuation.ToCharArray()).TrimEnd();

        return script switch
        {
            // 4. Katakana → hiragana on both sides.
            AnswerScript.Kana when kanaInsensitive => KatakanaToHiragana(value),
            // 5. Lower case, macrons spelled out, apostrophes/hyphens/spaces removed.
            AnswerScript.Romaji => NormalizeRomaji(value),
            _ => value,
        };
    }

    public static string KatakanaToHiragana(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            // ァ (U+30A1) … ヶ (U+30F6) map to ぁ … ゖ; the long vowel mark ー has no hiragana form and stays.
            builder.Append(c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c);
        }

        return builder.ToString();
    }

    private static string NormalizeRomaji(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        foreach (var c in value.ToLowerInvariant())
        {
            switch (c)
            {
                case 'ā':
                    builder.Append("aa");
                    break;
                case 'ī':
                    builder.Append("ii");
                    break;
                case 'ū':
                    builder.Append("uu");
                    break;
                case 'ē':
                    builder.Append("ee");
                    break;
                case 'ō':
                    builder.Append("ou");
                    break;
                case '\'' or '’' or '-' or ' ':
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string CollapseSpaces(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousSpace = false;
        foreach (var c in value)
        {
            var isSpace = char.IsWhiteSpace(c);
            if (!isSpace || !previousSpace)
            {
                builder.Append(isSpace ? ' ' : c);
            }

            previousSpace = isSpace;
        }

        return builder.ToString();
    }
}

/// <summary>BR-DRILL-05: is an answer correct? Shared by drilling and (phase 3) quizzes.</summary>
public static class AnswerEvaluator
{
    /// <summary>Handwriting pass score of docs/04 §12 (<c>Practice:Handwriting:PassScore</c>).</summary>
    public const double DefaultHandwritingPassScore = 0.75;

    /// <exception cref="JsonException">The answer does not have the shape of docs/03 §8.2 for this kind.</exception>
    public static Evaluation Evaluate(QuestionKind kind, JsonElement payload, JsonElement answer, double handwritingPassScore = DefaultHandwritingPassScore)
    {
        var parsed = Payloads.Parse(kind, payload);
        switch (parsed)
        {
            case McqPayload mcq:
                {
                    var selected = Read<McqAnswer>(answer).Selected ?? [];
                    var isCorrect = selected.Distinct().Count() == selected.Count && selected.ToHashSet().SetEquals(mcq.Correct);
                    return new Evaluation(isCorrect, InputMode.Choice, Key(new { correct = mcq.Correct }));
                }

            case TapSentencePayload tap:
                {
                    var order = Read<TapAnswer>(answer).Order ?? [];
                    var isCorrect = tap.Answers.Any(accepted => accepted.SequenceEqual(order));
                    return new Evaluation(isCorrect, InputMode.Tap, Key(new { order = tap.Answers[0] }));
                }

            case TypeAnswerPayload type:
                {
                    var typed = Read<TypedAnswer>(answer);
                    var insensitive = type.KanaInsensitive ?? type.AnswerScript == AnswerScript.Kana;
                    var value = AnswerNormalization.Normalize(typed.Text ?? string.Empty, type.AnswerScript, insensitive);
                    // 3. A kana answer must arrive as kana (the client converts romaji with WanaKana).
                    var isKana = type.AnswerScript != AnswerScript.Kana || JapaneseScript.IsKana(value);
                    var isCorrect = isKana && value.Length > 0 && type.Accepted.Any(accepted =>
                        string.Equals(AnswerNormalization.Normalize(accepted, type.AnswerScript, insensitive), value, StringComparison.Ordinal));
                    return new Evaluation(isCorrect, InputMode.Type, Key(new { accepted = type.Accepted }));
                }

            case MatchPayload match:
                {
                    var pairs = Read<MatchAnswer>(answer).Pairs ?? [];
                    var isCorrect = pairs.Count == match.Pairs.Count
                        && pairs.All(pair => pair is { Count: 2 } && pair[0] == pair[1])
                        && pairs.Select(pair => pair[0]).Distinct().Count() == match.Pairs.Count;
                    return new Evaluation(isCorrect, InputMode.Tap, Key(new { pairs = Enumerable.Range(0, match.Pairs.Count).Select(i => new[] { i, i }) }));
                }

            case FlashcardPayload:
                return new Evaluation(Read<FlashcardAnswer>(answer).Remembered, InputMode.SelfCheck, Key(new { remembered = true }));

            case HandwriteKanaPayload kana:
                {
                    var written = Read<HandwriteAnswer>(answer);
                    return written.Score is { } score
                        ? new Evaluation(score >= handwritingPassScore, InputMode.Handwrite, Key(new { target = kana.Target }))
                        : new Evaluation(written.SelfCheck == true, InputMode.SelfCheck, Key(new { target = kana.Target }));
                }

            case HandwriteKanjiPayload kanji:
                return new Evaluation(Read<HandwriteAnswer>(answer).SelfCheck == true, InputMode.SelfCheck, Key(new { target = kanji.Target }));

            default:
                // Long answers are graded by a teacher (phase 3) and never appear in drilling.
                throw new InvalidOperationException($"{kind} answers are not evaluated automatically.");
        }
    }

    /// <summary>The answer key of a question in the same shape the evaluation returns; null for long answers.</summary>
    public static JsonElement? KeyOf(QuestionKind kind, JsonElement payload) => Payloads.Parse(kind, payload) switch
    {
        McqPayload mcq => Key(new { correct = mcq.Correct }),
        TapSentencePayload tap => Key(new { order = tap.Answers[0] }),
        TypeAnswerPayload type => Key(new { accepted = type.Accepted }),
        MatchPayload match => Key(new { pairs = Enumerable.Range(0, match.Pairs.Count).Select(i => new[] { i, i }) }),
        FlashcardPayload => Key(new { remembered = true }),
        HandwriteKanaPayload kana => Key(new { target = kana.Target }),
        HandwriteKanjiPayload kanji => Key(new { target = kanji.Target }),
        _ => null,
    };

    private static T Read<T>(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The answer must be an object.");
        }

        return answer.Deserialize<T>(NomaxJson.Options) ?? throw new JsonException("The answer is empty.");
    }

    private static JsonElement Key(object value) => JsonSerializer.SerializeToElement(value, NomaxJson.Options);
}
