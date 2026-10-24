using System.Text.Json;
using Nomax.Modules.Practice.Domain;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Common;

/// <summary>A question for staff, including its payload and answer key (docs/01 §4).</summary>
/// <param name="HasAnswers">BR-SOAL-04: kind, payload, category and chapter are locked.</param>
public sealed record QuestionDto(
    Guid Id,
    QuestionKind Kind,
    Guid LevelId,
    Guid ChapterId,
    Category Category,
    IReadOnlyList<string> Tags,
    TextBlock Prompt,
    JsonElement Payload,
    RichText? Explanation,
    RefItemType? ItemType,
    Guid? ItemId,
    QuestionStatus Status,
    QuestionScope Scope,
    Guid SourceId,
    Guid OwnerId,
    bool HasAnswers,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static QuestionDto From(Question question, bool hasAnswers) =>
        new(question.Id, question.Kind, question.LevelId, question.ChapterId, question.Category, question.Tags, question.Prompt,
            question.Payload, question.Explanation, question.ItemType, question.ItemId, question.Status, question.Scope,
            question.SourceId, question.OwnerId, hasAnswers, question.Version, question.CreatedAt, question.UpdatedAt);
}

public sealed record QuestionSourceDto(Guid Id, string Name, string? Url, string License, Guid? ImportedFileId, Guid? ReviewedBy, DateTimeOffset? ReviewedAt)
{
    public static QuestionSourceDto From(QuestionSource source) =>
        new(source.Id, source.Name, source.Url, source.License, source.ImportedFileId, source.ReviewedBy, source.ReviewedAt);
}

/// <summary>Mastery of one item before and after an answer (docs/05 §7).</summary>
public sealed record MasteryChangeDto(decimal Before, decimal After, MasteryStatus Status);

/// <summary>Result of an answer: the key and explanation are only sent now, after answering (docs/03 §8.3).</summary>
public sealed record AnswerResultDto(bool IsCorrect, JsonElement CorrectAnswer, RichText? Explanation, MasteryChangeDto? Mastery);

public sealed record MasteryItemDto(
    MasteryItemType ItemType,
    Guid ItemId,
    TextBlock? Label,
    int Attempts,
    int Correct,
    int Streak,
    decimal Mastery,
    MasteryStatus Status,
    Guid ChapterId,
    Category Category,
    IReadOnlyList<string> Tags,
    DateTimeOffset LastSeenAt);

public sealed record WeaknessGroupDto(WeaknessGroupKind Kind, string Key, int ItemCount, decimal AverageMastery, decimal Accuracy, int WeakCount);

public sealed record WeaknessesDto(IReadOnlyList<WeaknessGroupDto> Groups, IReadOnlyList<MasteryItemDto> WeakestItems);

public sealed record DrillSessionSummaryDto(
    Guid Id,
    Category Category,
    DrillMode Mode,
    Guid? ClassId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int AnswerCount,
    int CorrectCount);

public sealed record CommentDto(Guid Id, Guid StudentId, Guid AuthorId, string AuthorName, CommentTarget TargetType, Guid TargetId, string Text, DateTimeOffset CreatedAt);
