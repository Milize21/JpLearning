using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Questions;

/// <summary>Loading questions with the BR-SOAL-03 visibility check, and the shared lookups of the question use cases.</summary>
internal sealed class QuestionStore(PracticeDbContext db, PracticeAccess access, IContentCatalog catalog, IReferenceDataReader reference, TimeProvider timeProvider)
{
    /// <summary>A question the caller may see; a private question of someone else is 404 (docs/01 §6).</summary>
    public async Task<Result<Question>> VisibleAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await db.Questions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return question is not null && QuestionRules.IsVisibleToStaff(question.Scope, question.OwnerId, access.UserId, access.IsAdmin)
            ? question
            : AppError.NotFound();
    }

    /// <summary>A question the caller may change: its owner, or an admin (builtin questions are admin-only).</summary>
    public async Task<Result<Question>> ManageableAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await VisibleAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found;
        }

        return QuestionRules.CanManage(found.Value.Scope, found.Value.OwnerId, access.UserId, access.IsAdmin)
            ? found
            : AppError.Forbidden("Only the owner or an admin can change this question.");
    }

    /// <summary>BR-SOAL-04: answered questions keep their content.</summary>
    public Task<bool> HasAnswersAsync(Guid questionId, CancellationToken cancellationToken) =>
        db.DrillAnswers.AnyAsync(answer => answer.QuestionId == questionId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> AnsweredAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken) =>
        (await db.DrillAnswers.AsNoTracking()
            .Where(answer => questionIds.Contains(answer.QuestionId))
            .Select(answer => answer.QuestionId)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    /// <summary>"Internal NomaX" (license internal) for questions written by staff (BR-SOAL-01), created on first use.</summary>
    public async Task<Guid> InternalSourceIdAsync(CancellationToken cancellationToken) =>
        await SourceIdAsync(QuestionSource.InternalName, QuestionSource.InternalLicense, cancellationToken);

    /// <summary>A source with this exact name and license, created when missing.</summary>
    public async Task<Guid> SourceIdAsync(string name, string license, CancellationToken cancellationToken)
    {
        var existing = await db.QuestionSources.AsNoTracking()
            .Where(source => source.Name == name && source.License == license)
            .OrderBy(source => source.CreatedAt)
            .Select(source => (Guid?)source.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is { } id)
        {
            return id;
        }

        var tracked = db.QuestionSources.Local.FirstOrDefault(source => source.Name == name && source.License == license);
        if (tracked is not null)
        {
            return tracked.Id;
        }

        var created = QuestionSource.Create(Uuid7.New(timeProvider), name, null, license);
        db.QuestionSources.Add(created);
        return created.Id;
    }

    /// <summary>The chapter must exist and belong to the level (docs/03 §7.2).</summary>
    public async Task<AppError?> CheckChapterAsync(Guid levelId, Guid chapterId, CancellationToken cancellationToken)
    {
        var chapter = await catalog.GetChapterAsync(chapterId, cancellationToken);
        return chapter is null || chapter.LevelId != levelId
            ? AppError.Validation(new Dictionary<string, string[]> { ["chapterId"] = ["Must be a chapter of the chosen level."] })
            : null;
    }

    /// <summary>The referenced reference item must exist (docs/03 §12).</summary>
    public async Task<AppError?> CheckItemAsync(RefItemType? itemType, Guid? itemId, CancellationToken cancellationToken)
    {
        if (itemType is not { } type || itemId is not { } id)
        {
            return null;
        }

        var found = await reference.GetLabelsAsync([(ToReferenceKind(type), id)], cancellationToken);
        return found.Count == 0
            ? AppError.Validation(new Dictionary<string, string[]> { ["itemId"] = ["No such reference item."] })
            : null;
    }

    public static ReferenceItemKind ToReferenceKind(RefItemType type) => type switch
    {
        RefItemType.Kana => ReferenceItemKind.Kana,
        RefItemType.Kanji => ReferenceItemKind.Kanji,
        RefItemType.Vocab => ReferenceItemKind.Vocab,
        RefItemType.Grammar => ReferenceItemKind.Grammar,
        _ => ReferenceItemKind.Sentence,
    };
}

/// <summary>Rules shared by the create and update validators.</summary>
internal static class QuestionValidation
{
    public static void Tags<T>(IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule.Must(tags => tags is null || (tags.Count <= Question.MaxTags && tags.All(tag => tag?.Trim().Length is > 0 and <= Question.TagMaxLength)))
            .WithMessage($"At most {Question.MaxTags} tags of 1–{Question.TagMaxLength} characters.");

    public static IReadOnlyList<string> CleanTags(IReadOnlyList<string>? tags) =>
        [.. (tags ?? []).Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.Ordinal)];
}
