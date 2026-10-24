using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Domain;

// questions.payload per kind (docs/03 §8.1). Indexes are the original positions in the payload, never display positions.

public sealed record McqPayload(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TextBlock? Stimulus,
    IReadOnlyList<TextBlock> Choices,
    IReadOnlyList<int> Correct,
    bool Multi,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JaDisplay? JaDisplay);

/// <param name="Answers">Every accepted order of token indexes; tokens in no order are distractors.</param>
public sealed record TapSentencePayload(
    TextBlock Stimulus,
    IReadOnlyList<TextBlock> Tokens,
    IReadOnlyList<IReadOnlyList<int>> Answers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JaDisplay? JaDisplay);

public sealed record TypeAnswerPayload(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TextBlock? Stimulus,
    IReadOnlyList<string> Accepted,
    AnswerScript AnswerScript,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? KanaInsensitive,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JaDisplay? JaDisplay);

public sealed record MatchPair(TextBlock Left, TextBlock Right);

public sealed record MatchPayload(
    IReadOnlyList<MatchPair> Pairs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JaDisplay? JaDisplay);

public sealed record FlashcardPayload(
    TextBlock Front,
    TextBlock Back,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JaDisplay? JaDisplay);

public sealed record HandwriteKanaPayload(string Target, string KanjivgCode);

public sealed record HandwriteKanjiPayload(string Target, string KanjivgCode, bool SelfCheck);

public sealed record LongAnswerPayload(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RichText? Rubric,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxChars);

/// <summary>A payload problem; <see cref="Path"/> is relative to <c>payload</c>, e.g. <c>choices[2].id</c>.</summary>
public sealed record PayloadIssue(string Path, string Message);

/// <summary>Parsing and validation of question payloads (docs/03 §8.1).</summary>
public static partial class Payloads
{
    public const int LongAnswerDefaultMaxChars = 2000;

    public static object Parse(QuestionKind kind, JsonElement payload) => kind switch
    {
        QuestionKind.Mcq => Deserialize<McqPayload>(payload),
        QuestionKind.TapSentence => Deserialize<TapSentencePayload>(payload),
        QuestionKind.TypeAnswer => Deserialize<TypeAnswerPayload>(payload),
        QuestionKind.Match => Deserialize<MatchPayload>(payload),
        QuestionKind.Flashcard => Deserialize<FlashcardPayload>(payload),
        QuestionKind.HandwriteKana => Deserialize<HandwriteKanaPayload>(payload),
        QuestionKind.HandwriteKanji => Deserialize<HandwriteKanjiPayload>(payload),
        QuestionKind.LongAnswer => Deserialize<LongAnswerPayload>(payload),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown question kind."),
    };

    public static JsonElement ToJson(object payload) => JsonSerializer.SerializeToElement(payload, payload.GetType(), NomaxJson.Options);

    /// <summary>The <c>jaDisplay</c> of the payload, <c>user_pref</c> when absent.</summary>
    public static JaDisplay JaDisplayOf(object payload) => payload switch
    {
        McqPayload mcq => mcq.JaDisplay ?? JaDisplay.UserPref,
        TapSentencePayload tap => tap.JaDisplay ?? JaDisplay.UserPref,
        TypeAnswerPayload type => type.JaDisplay ?? JaDisplay.UserPref,
        MatchPayload match => match.JaDisplay ?? JaDisplay.UserPref,
        FlashcardPayload card => card.JaDisplay ?? JaDisplay.UserPref,
        _ => JaDisplay.Plain,
    };

    /// <summary>All problems of a payload for <paramref name="kind"/>; empty when valid.</summary>
    public static IReadOnlyList<PayloadIssue> Validate(QuestionKind kind, JsonElement payload)
    {
        var issues = new List<PayloadIssue>();
        if (payload.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new PayloadIssue(string.Empty, "Must be an object."));
            return issues;
        }

        object parsed;
        try
        {
            parsed = Parse(kind, payload);
        }
        catch (JsonException exception)
        {
            issues.Add(new PayloadIssue(exception.Path?.TrimStart('$', '.') ?? string.Empty, "Has the wrong shape for this question kind."));
            return issues;
        }

        switch (parsed)
        {
            case McqPayload mcq:
                ValidateMcq(mcq, issues);
                break;
            case TapSentencePayload tap:
                ValidateTap(tap, issues);
                break;
            case TypeAnswerPayload type:
                ValidateType(type, issues);
                break;
            case MatchPayload match:
                ValidateMatch(match, issues);
                break;
            case FlashcardPayload card:
                Text(card.Front, "front", issues);
                Text(card.Back, "back", issues);
                break;
            case HandwriteKanaPayload kana:
                if (string.IsNullOrEmpty(kana.Target) || kana.Target.Length > 4 || !JapaneseScript.IsKana(kana.Target))
                {
                    issues.Add(new PayloadIssue("target", "Must be 1–4 kana."));
                }

                KanjivgCode(kana.KanjivgCode, issues);
                break;
            case HandwriteKanjiPayload kanji:
                if (string.IsNullOrEmpty(kanji.Target) || kanji.Target.Length > 4 || !JapaneseScript.ContainsKanji(kanji.Target))
                {
                    issues.Add(new PayloadIssue("target", "Must be 1–4 characters with a kanji."));
                }

                KanjivgCode(kanji.KanjivgCode, issues);
                break;
            case LongAnswerPayload longAnswer:
                if (longAnswer.Rubric is not null)
                {
                    issues.AddRange(TextRules.Validate(longAnswer.Rubric).Select(issue => new PayloadIssue(Join("rubric", issue.Path), issue.Message)));
                }

                if (longAnswer.MaxChars is < 1 or > LongAnswerDefaultMaxChars)
                {
                    issues.Add(new PayloadIssue("maxChars", $"Must be 1–{LongAnswerDefaultMaxChars}."));
                }

                break;
        }

        return issues;
    }

    private static void ValidateMcq(McqPayload mcq, List<PayloadIssue> issues)
    {
        OptionalText(mcq.Stimulus, "stimulus", issues);
        var choices = mcq.Choices ?? [];
        if (choices.Count is < 2 or > 6)
        {
            issues.Add(new PayloadIssue("choices", "Must have 2–6 choices."));
        }

        for (var i = 0; i < choices.Count; i++)
        {
            Text(choices[i], $"choices[{i}]", issues);
        }

        var correct = mcq.Correct ?? [];
        if (correct.Count == 0 || correct.Distinct().Count() != correct.Count || correct.Any(index => index < 0 || index >= choices.Count))
        {
            issues.Add(new PayloadIssue("correct", "Must list distinct indexes of existing choices."));
        }
        else if (!mcq.Multi && correct.Count != 1)
        {
            issues.Add(new PayloadIssue("correct", "A single-answer question has exactly one correct choice."));
        }
    }

    private static void ValidateTap(TapSentencePayload tap, List<PayloadIssue> issues)
    {
        Text(tap.Stimulus, "stimulus", issues);
        var tokens = tap.Tokens ?? [];
        if (tokens.Count is < 2 or > 20)
        {
            issues.Add(new PayloadIssue("tokens", "Must have 2–20 tokens."));
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            Text(tokens[i], $"tokens[{i}]", issues);
        }

        var answers = tap.Answers ?? [];
        if (answers.Count is < 1 or > 10)
        {
            issues.Add(new PayloadIssue("answers", "Must have 1–10 accepted orders."));
        }

        for (var i = 0; i < answers.Count; i++)
        {
            var order = answers[i] ?? [];
            if (order.Count < 2 || order.Distinct().Count() != order.Count || order.Any(index => index < 0 || index >= tokens.Count))
            {
                issues.Add(new PayloadIssue($"answers[{i}]", "Must list at least 2 distinct token indexes."));
            }
        }
    }

    private static void ValidateType(TypeAnswerPayload type, List<PayloadIssue> issues)
    {
        OptionalText(type.Stimulus, "stimulus", issues);
        if (!Enum.IsDefined(type.AnswerScript))
        {
            issues.Add(new PayloadIssue("answerScript", "Must be kana, romaji or free."));
        }

        var accepted = type.Accepted ?? [];
        if (accepted.Count is < 1 or > 20)
        {
            issues.Add(new PayloadIssue("accepted", "Must have 1–20 accepted answers."));
        }

        for (var i = 0; i < accepted.Count; i++)
        {
            var normalized = AnswerNormalization.Normalize(accepted[i] ?? string.Empty, type.AnswerScript, kanaInsensitive: false);
            if (normalized.Length is 0 or > 100)
            {
                issues.Add(new PayloadIssue($"accepted[{i}]", "Must be 1–100 characters."));
            }
            else if (type.AnswerScript == AnswerScript.Kana && !JapaneseScript.IsKana(normalized))
            {
                issues.Add(new PayloadIssue($"accepted[{i}]", "Must be kana for a kana answer."));
            }
            else if (type.AnswerScript == AnswerScript.Romaji && !RomajiAnswer().IsMatch(normalized))
            {
                issues.Add(new PayloadIssue($"accepted[{i}]", "Must be romaji letters a–z."));
            }
        }
    }

    private static void ValidateMatch(MatchPayload match, List<PayloadIssue> issues)
    {
        var pairs = match.Pairs ?? [];
        if (pairs.Count is < 3 or > 8)
        {
            issues.Add(new PayloadIssue("pairs", "Must have 3–8 pairs."));
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            if (pairs[i] is null)
            {
                issues.Add(new PayloadIssue($"pairs[{i}]", "Is required."));
                continue;
            }

            Text(pairs[i].Left, $"pairs[{i}].left", issues);
            Text(pairs[i].Right, $"pairs[{i}].right", issues);
        }
    }

    private static void KanjivgCode(string? code, List<PayloadIssue> issues)
    {
        if (code is null || !KanjivgPattern().IsMatch(code))
        {
            issues.Add(new PayloadIssue("kanjivgCode", "Must be the 5-digit lowercase hex code of a KanjiVG file."));
        }
    }

    private static void Text(TextBlock? block, string path, List<PayloadIssue> issues) =>
        issues.AddRange(TextRules.Validate(block, TextBlockUsage.Stimulus).Select(issue => new PayloadIssue(Join(path, issue.Path), issue.Message)));

    private static void OptionalText(TextBlock? block, string path, List<PayloadIssue> issues)
    {
        if (block is not null)
        {
            Text(block, path, issues);
        }
    }

    private static string Join(string path, string inner) =>
        inner.Length == 0 ? path : inner.StartsWith('[') ? path + inner : $"{path}.{inner}";

    private static T Deserialize<T>(JsonElement payload) =>
        payload.Deserialize<T>(NomaxJson.Options) ?? throw new JsonException("The payload is empty.");

    [GeneratedRegex("^[a-z]+$")]
    private static partial Regex RomajiAnswer();

    [GeneratedRegex("^[0-9a-f]{5}$")]
    private static partial Regex KanjivgPattern();
}
