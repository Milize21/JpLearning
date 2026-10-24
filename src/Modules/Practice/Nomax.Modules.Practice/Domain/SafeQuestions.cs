using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Domain;

public sealed record ChoiceOption(int ChoiceId, TextBlock Text);

public sealed record TokenOption(int TokenId, TextBlock Text);

public sealed record MatchLeftOption(int LeftId, TextBlock Text);

public sealed record MatchRightOption(int RightId, TextBlock Text);

/// <summary>
/// The student version of a question (docs/03 §8.3): no <c>correct</c>, <c>answers</c>, <c>accepted</c> or <c>rubric</c>;
/// choices, tokens and the right column of a match are shuffled and carry their original index as id. Only the fields
/// of the question's kind are set.
/// </summary>
public sealed record SafeQuestion(
    Guid Id,
    QuestionKind Kind,
    Category Category,
    TextBlock Prompt,
    JaDisplay JaDisplay,
    TextBlock? Stimulus,
    bool? Multi,
    IReadOnlyList<ChoiceOption>? Choices,
    IReadOnlyList<TokenOption>? Tokens,
    AnswerScript? AnswerScript,
    IReadOnlyList<MatchLeftOption>? Left,
    IReadOnlyList<MatchRightOption>? Right,
    TextBlock? Front,
    TextBlock? Back,
    int? MaxChars);

public static class SafeQuestions
{
    /// <param name="random">Shuffles the options; seed it per session and question so a reload shows the same order.</param>
    public static SafeQuestion For(Question question, Random random)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(random);
        var payload = Payloads.Parse(question.Kind, question.Payload);
        var safe = new SafeQuestion(
            question.Id, question.Kind, question.Category, question.Prompt, Payloads.JaDisplayOf(payload),
            null, null, null, null, null, null, null, null, null, null);

        return payload switch
        {
            McqPayload mcq => safe with
            {
                Stimulus = mcq.Stimulus,
                Multi = mcq.Multi,
                Choices = Shuffle(mcq.Choices.Select((text, index) => new ChoiceOption(index, text)), random),
            },
            TapSentencePayload tap => safe with
            {
                Stimulus = tap.Stimulus,
                Tokens = Shuffle(tap.Tokens.Select((text, index) => new TokenOption(index, text)), random),
            },
            TypeAnswerPayload type => safe with { Stimulus = type.Stimulus, AnswerScript = type.AnswerScript },
            MatchPayload match => safe with
            {
                Left = [.. match.Pairs.Select((pair, index) => new MatchLeftOption(index, pair.Left))],
                Right = Shuffle(match.Pairs.Select((pair, index) => new MatchRightOption(index, pair.Right)), random),
            },
            // The back is needed when the card is flipped; a self-check answer earns no points (BR-POIN-07).
            FlashcardPayload card => safe with { Front = card.Front, Back = card.Back },
            LongAnswerPayload longAnswer => safe with { MaxChars = longAnswer.MaxChars ?? Payloads.LongAnswerDefaultMaxChars },
            // Handwriting targets are the answer; phase 5 decides what the student sees.
            _ => safe,
        };
    }

    /// <summary>A shuffle stable per session and question.</summary>
    public static Random SeededRandom(Guid sessionId, Guid questionId) => new(StableSeed(sessionId, questionId));

    public static int StableSeed(Guid first, Guid second)
    {
        var a = first.ToByteArray();
        var b = second.ToByteArray();
        unchecked
        {
            var hash = (int)2166136261;
            for (var i = 0; i < 16; i++)
            {
                hash = (hash ^ (a[i] ^ b[15 - i])) * 16777619;
            }

            return hash;
        }
    }

    private static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
