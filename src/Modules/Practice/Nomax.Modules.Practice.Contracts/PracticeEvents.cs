using Nomax.SharedKernel.Events;

namespace Nomax.Modules.Practice.Contracts;

// Integration events published by Practice (docs/05 §8). Staff preview never publishes any (BR-AKSES-05).

/// <summary>One drilling answer; quiz answers do not publish this (points for quizzes come from QuizGraded).</summary>
/// <param name="ItemType">Mastery item type (<c>kana</c>, …, or <c>question</c> when the question has no reference item).</param>
/// <param name="Category">Question category, e.g. <c>hiragana</c>.</param>
/// <param name="InputMode"><c>choice</c>, <c>tap</c>, <c>type</c>, <c>handwrite</c> or <c>self_check</c>.</param>
public sealed record AnswerSubmitted(
    Guid AnswerId,
    Guid SessionId,
    Guid StudentId,
    Guid LevelId,
    Guid ChapterId,
    Guid QuestionId,
    string ItemType,
    Guid ItemId,
    string Category,
    bool IsCorrect,
    string InputMode,
    DateTimeOffset AnsweredAt) : IIntegrationEvent
{
    public static string EventType => "AnswerSubmitted.v1";
}

/// <param name="QuizKind"><c>quiz</c> or <c>exam</c>.</param>
public sealed record QuizGraded(
    Guid AttemptId,
    Guid QuizId,
    string QuizKind,
    Guid ClassId,
    Guid LevelId,
    Guid StudentId,
    decimal ScorePct,
    int AttemptNo,
    DateTimeOffset GradedAt) : IIntegrationEvent
{
    public static string EventType => "QuizGraded.v1";
}

public sealed record SubmissionGraded(
    Guid SubmissionId,
    Guid AssignmentId,
    Guid ClassId,
    Guid StudentId,
    decimal GradePct,
    Guid GradedBy,
    DateTimeOffset GradedAt) : IIntegrationEvent
{
    public static string EventType => "SubmissionGraded.v1";
}

public sealed record DrillSessionEnded(
    Guid SessionId,
    Guid StudentId,
    int AnswerCount,
    int CorrectCount,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt) : IIntegrationEvent
{
    public static string EventType => "DrillSessionEnded.v1";
}
