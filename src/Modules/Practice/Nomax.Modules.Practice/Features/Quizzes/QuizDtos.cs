using System.Text.Json;
using Nomax.Modules.Practice.Domain;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Quizzes;

/// <summary>A quiz for its teachers (and admins).</summary>
public sealed record QuizDto(
    Guid Id,
    Guid ClassId,
    Guid? ChapterId,
    QuizKind Kind,
    TextBlock Title,
    int? TimeLimitMin,
    int MaxAttempts,
    DateTimeOffset? OpenAt,
    DateTimeOffset? CloseAt,
    bool Shuffle,
    bool ShowReview,
    PublishStatus Status,
    int QuestionCount,
    decimal TotalPoints,
    bool HasAttempts,
    uint Version)
{
    public static QuizDto From(Quiz quiz, int questionCount, decimal totalPoints, bool hasAttempts) =>
        new(quiz.Id, quiz.ClassId, quiz.ChapterId, quiz.Kind, quiz.Title, quiz.TimeLimitMin, quiz.MaxAttempts, quiz.OpenAt,
            quiz.CloseAt, quiz.Shuffle, quiz.ShowReview, quiz.Status, questionCount, totalPoints, hasAttempts, quiz.Version);
}

/// <summary>A quiz for a student: information only, never the questions (docs/05 §7).</summary>
/// <param name="InProgressAttemptId">The running attempt to resume, if any.</param>
public sealed record StudentQuizDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    QuizKind Kind,
    TextBlock Title,
    int? TimeLimitMin,
    int MaxAttempts,
    DateTimeOffset? OpenAt,
    DateTimeOffset? CloseAt,
    int QuestionCount,
    int AttemptsUsed,
    Guid? InProgressAttemptId,
    decimal? BestScorePct,
    bool IsOpen,
    IReadOnlyList<AttemptSummaryDto> Attempts);

/// <param name="PendingManual">Long answers still waiting for a teacher (BR-KUIS-08).</param>
public sealed record AttemptSummaryDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    int AttemptNo,
    AttemptStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? SubmittedAt,
    decimal? ScorePct,
    int PendingManual);

/// <summary>One question inside a quiz for its teachers.</summary>
public sealed record QuizQuestionDto(Guid QuestionId, decimal Points, int SortOrder, QuestionKind Kind, Category Category, TextBlock Prompt, QuestionStatus Status);

/// <summary>
/// One question of an attempt. Students get the student version (docs/03 §8.3); <see cref="CorrectAnswer"/> and
/// <see cref="Explanation"/> only appear when BR-KUIS-09 allows it (or for staff).
/// </summary>
public sealed record AttemptQuestionDto(
    SafeQuestion Question,
    decimal Points,
    JsonElement? Answer,
    bool? IsCorrect,
    decimal? Earned,
    JsonElement? CorrectAnswer,
    RichText? Explanation);

/// <param name="ServerNow">Lets the client run its countdown against the server clock (BR-KUIS-03).</param>
public sealed record AttemptDto(
    Guid Id,
    Guid QuizId,
    TextBlock QuizTitle,
    QuizKind QuizKind,
    Guid StudentId,
    int AttemptNo,
    AttemptStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? SubmittedAt,
    decimal? ScorePct,
    bool ReviewVisible,
    DateTimeOffset ServerNow,
    IReadOnlyList<AttemptQuestionDto> Questions);
