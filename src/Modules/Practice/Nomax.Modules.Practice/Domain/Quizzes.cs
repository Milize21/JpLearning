using System.Text.Json;
using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Domain;

public enum QuizKind
{
    Quiz,
    Exam,
}

/// <summary><c>publish_status</c> of quizzes and assignments (docs/03 §3.3).</summary>
public enum PublishStatus
{
    Draft,
    Published,
    Archived,
}

public enum AttemptStatus
{
    InProgress,
    Submitted,
    Graded,
}

/// <summary>practice.quizzes (docs/03 §7.6).</summary>
public sealed class Quiz : IHasTimestamps, IHasVersion
{
    private Quiz()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClassId { get; private set; }

    /// <summary>Null = across chapters (usually exams).</summary>
    public Guid? ChapterId { get; private set; }

    public QuizKind Kind { get; private set; }

    public TextBlock Title { get; private set; } = null!;

    public int? TimeLimitMin { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTimeOffset? OpenAt { get; private set; }

    public DateTimeOffset? CloseAt { get; private set; }

    public bool Shuffle { get; private set; }

    public bool ShowReview { get; private set; }

    public PublishStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Quiz Create(
        Guid id, Guid classId, Guid? chapterId, QuizKind kind, TextBlock title, int? timeLimitMin, int maxAttempts,
        DateTimeOffset? openAt, DateTimeOffset? closeAt, bool shuffle, bool showReview, Guid createdBy) =>
        new()
        {
            Id = id,
            ClassId = classId,
            ChapterId = chapterId,
            Kind = kind,
            Title = title,
            TimeLimitMin = timeLimitMin,
            MaxAttempts = maxAttempts,
            OpenAt = openAt,
            CloseAt = closeAt,
            Shuffle = shuffle,
            ShowReview = showReview,
            Status = PublishStatus.Draft,
            CreatedBy = createdBy,
        };

    public void Update(
        TextBlock title, Guid? chapterId, QuizKind kind, int? timeLimitMin, int maxAttempts, DateTimeOffset? openAt,
        DateTimeOffset? closeAt, bool shuffle, bool showReview)
    {
        Title = title;
        ChapterId = chapterId;
        Kind = kind;
        TimeLimitMin = timeLimitMin;
        MaxAttempts = maxAttempts;
        OpenAt = openAt;
        CloseAt = closeAt;
        Shuffle = shuffle;
        ShowReview = showReview;
    }

    public void ChangeStatus(PublishStatus status) => Status = status;
}

/// <summary>practice.quiz_questions (docs/03 §7.7).</summary>
public sealed class QuizQuestion : IHasTimestamps
{
    private QuizQuestion()
    {
    }

    public Guid QuizId { get; private set; }

    public Guid QuestionId { get; private set; }

    public decimal Points { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static QuizQuestion Create(Guid quizId, Guid questionId, decimal points, int sortOrder) =>
        new() { QuizId = quizId, QuestionId = questionId, Points = points, SortOrder = sortOrder };
}

/// <summary>practice.quiz_attempts (docs/03 §7.8).</summary>
public sealed class QuizAttempt : IHasTimestamps
{
    private QuizAttempt()
    {
    }

    public Guid Id { get; private set; }

    public Guid QuizId { get; private set; }

    public Guid StudentId { get; private set; }

    public int AttemptNo { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>BR-KUIS-03; null = no time limit and no close time.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public decimal? ScorePct { get; private set; }

    public AttemptStatus Status { get; private set; }

    /// <summary>Question order of this attempt (BR-KUIS-04), stable across reloads.</summary>
    public List<Guid> QuestionOrder { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static QuizAttempt Start(Guid id, Guid quizId, Guid studentId, int attemptNo, DateTimeOffset startedAt, DateTimeOffset? expiresAt, IEnumerable<Guid> order) =>
        new()
        {
            Id = id,
            QuizId = quizId,
            StudentId = studentId,
            AttemptNo = attemptNo,
            StartedAt = startedAt,
            ExpiresAt = expiresAt,
            Status = AttemptStatus.InProgress,
            QuestionOrder = [.. order],
        };

    public void Submit(DateTimeOffset at) => (SubmittedAt, Status) = (at, AttemptStatus.Submitted);

    public void Grade(decimal scorePct) => (ScorePct, Status) = (scorePct, AttemptStatus.Graded);
}

/// <summary>practice.attempt_answers (docs/03 §7.9).</summary>
public sealed class AttemptAnswer : IHasTimestamps
{
    private AttemptAnswer()
    {
    }

    public Guid AttemptId { get; private set; }

    public Guid QuestionId { get; private set; }

    /// <summary>Null = not answered.</summary>
    public JsonElement? Answer { get; private set; }

    /// <summary>Null = waiting for a teacher (long answers).</summary>
    public bool? IsCorrect { get; private set; }

    public decimal? Points { get; private set; }

    public Guid? GradedBy { get; private set; }

    public DateTimeOffset? GradedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static AttemptAnswer Create(Guid attemptId, Guid questionId, JsonElement? answer) =>
        new() { AttemptId = attemptId, QuestionId = questionId, Answer = answer?.Clone() };

    public void Save(JsonElement answer) => Answer = answer.Clone();

    public void SetResult(bool? isCorrect, decimal? points) => (IsCorrect, Points) = (isCorrect, points);

    public void GradeManually(decimal points, decimal maxPoints, Guid gradedBy, DateTimeOffset at)
    {
        Points = points;
        IsCorrect = points >= maxPoints;
        GradedBy = gradedBy;
        GradedAt = at;
    }
}

/// <summary>Result of grading one quiz answer automatically (BR-KUIS-07); null points = manual grading.</summary>
public sealed record AnswerGrade(bool? IsCorrect, decimal? Points);

/// <summary>BR-KUIS-01…10 as pure functions.</summary>
public static class QuizRules
{
    /// <summary>Kinds allowed in quizzes and exams: no flashcards or other self-checks (BR-KUIS-07).</summary>
    public static bool IsAllowedInQuiz(QuestionKind kind) =>
        kind is QuestionKind.Mcq or QuestionKind.TapSentence or QuestionKind.TypeAnswer or QuestionKind.Match or QuestionKind.LongAnswer;

    /// <summary>BR-KUIS-01: inside the window (open_at ≤ now &lt; close_at).</summary>
    public static bool IsOpen(DateTimeOffset? openAt, DateTimeOffset? closeAt, DateTimeOffset now) =>
        (openAt is null || now >= openAt) && (closeAt is null || now < closeAt);

    /// <summary>BR-KUIS-03: <c>min(started_at + time_limit, close_at)</c>; null when both are absent.</summary>
    public static DateTimeOffset? ExpiresAt(DateTimeOffset startedAt, int? timeLimitMin, DateTimeOffset? closeAt)
    {
        DateTimeOffset? byLimit = timeLimitMin is { } minutes ? startedAt.AddMinutes(minutes) : null;
        return (byLimit, closeAt) switch
        {
            ({ } limit, { } close) => limit < close ? limit : close,
            ({ } limit, null) => limit,
            (null, { } close) => close,
            _ => null,
        };
    }

    /// <summary>BR-KUIS-03: answers after <c>expires_at</c> plus the grace period are refused.</summary>
    public static bool IsExpired(DateTimeOffset? expiresAt, DateTimeOffset now, TimeSpan grace) =>
        expiresAt is { } expires && now > expires + grace;

    /// <summary>
    /// BR-KUIS-09: with <c>show_review</c> the key and explanation appear after <c>close_at</c>; without a close time,
    /// once the student has no attempts left. Without <c>show_review</c>, only the score.
    /// </summary>
    public static bool CanSeeReview(bool showReview, DateTimeOffset? closeAt, DateTimeOffset now, int attemptsUsed, int maxAttempts) =>
        showReview && (closeAt is { } close ? now >= close : attemptsUsed >= maxAttempts);

    /// <summary>BR-KUIS-07: full points or 0; match gives points × correct pairs / pairs (2 decimals); long answers wait.</summary>
    public static AnswerGrade Grade(QuestionKind kind, JsonElement payload, JsonElement? answer, decimal points)
    {
        if (kind == QuestionKind.LongAnswer)
        {
            var text = answer is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("text", out var t) ? t.GetString() : null;
            return string.IsNullOrWhiteSpace(text) ? new AnswerGrade(false, 0m) : new AnswerGrade(null, null);
        }

        if (answer is not { ValueKind: JsonValueKind.Object } given)
        {
            return new AnswerGrade(false, 0m);
        }

        try
        {
            if (kind == QuestionKind.Match)
            {
                var pairs = Payloads.Parse(kind, payload) is MatchPayload match ? match.Pairs.Count : 0;
                var chosen = given.Deserialize<MatchAnswer>(Nomax.SharedKernel.Serialization.NomaxJson.Options)?.Pairs ?? [];
                var correct = chosen
                    .Where(pair => pair is { Count: 2 } && pair[0] == pair[1] && pair[0] >= 0 && pair[0] < pairs)
                    .Select(pair => pair[0])
                    .Distinct()
                    .Count();
                var earned = pairs == 0 ? 0m : Math.Round(points * correct / pairs, 2, MidpointRounding.AwayFromZero);
                return new AnswerGrade(correct == pairs && pairs > 0, earned);
            }

            var evaluation = AnswerEvaluator.Evaluate(kind, payload, given);
            return new AnswerGrade(evaluation.IsCorrect, evaluation.IsCorrect ? points : 0m);
        }
        catch (JsonException)
        {
            return new AnswerGrade(false, 0m);
        }
    }

    /// <summary>BR-KUIS-08: <c>round(100 × Σpoints / Σmax, 2)</c>.</summary>
    public static decimal ScorePct(decimal earned, decimal maximum) =>
        maximum <= 0 ? 0m : Math.Round(100m * earned / maximum, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// BR-KUIS-10: once attempted, only the title, a later close time, show_review and a higher max_attempts may change.
    /// Returns the name of the first locked field that changes, or null.
    /// </summary>
    public static string? LockedChange(Quiz quiz, QuizKind kind, Guid? chapterId, int? timeLimitMin, int maxAttempts, DateTimeOffset? openAt, DateTimeOffset? closeAt, bool shuffle)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        if (kind != quiz.Kind)
        {
            return "kind";
        }

        if (timeLimitMin != quiz.TimeLimitMin)
        {
            return "timeLimitMin";
        }

        if (chapterId != quiz.ChapterId)
        {
            return "chapterId";
        }

        if (openAt != quiz.OpenAt)
        {
            return "openAt";
        }

        if (shuffle != quiz.Shuffle)
        {
            return "shuffle";
        }

        if (maxAttempts < quiz.MaxAttempts)
        {
            return "maxAttempts";
        }

        // The close time may only move later (or be removed).
        return quiz.CloseAt is { } current && closeAt is { } next && next < current ? "closeAt" : null;
    }
}
