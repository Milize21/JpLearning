using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Common;

/// <summary>Mastery rows as DTOs with item labels (BR-MASTERY-02 status, BR-MASTERY-03 weaknesses).</summary>
internal sealed class MasteryViews(PracticeDbContext db, IReferenceDataReader reference, IOptions<MasteryOptions> options)
{
    public MasterySettings Settings => options.Value.ToSettings();

    /// <summary>The student's rows for these items (items never answered are absent).</summary>
    public async Task<IReadOnlyList<MasteryItemDto>> ItemsAsync(Guid studentId, IReadOnlyCollection<(MasteryItemType Type, Guid Id)> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        var ids = keys.Select(key => key.Id).Distinct().ToList();
        var wanted = keys.ToHashSet();
        var rows = (await db.ItemMasteries.AsNoTracking()
                .Where(mastery => mastery.StudentId == studentId && ids.Contains(mastery.ItemId))
                .ToListAsync(cancellationToken))
            .Where(mastery => wanted.Contains((mastery.ItemType, mastery.ItemId)))
            .ToList();
        return await ToDtosAsync(rows, cancellationToken);
    }

    /// <summary>All rows of a student, optionally narrowed to a level, chapter or category.</summary>
    public async Task<IReadOnlyList<ItemMastery>> RowsAsync(Guid studentId, Guid? levelId, Guid? chapterId, Category? category, CancellationToken cancellationToken)
    {
        var rows = db.ItemMasteries.AsNoTracking().Where(mastery => mastery.StudentId == studentId);
        if (levelId is { } level)
        {
            rows = rows.Where(mastery => mastery.LevelId == level);
        }

        if (chapterId is { } chapter)
        {
            rows = rows.Where(mastery => mastery.ChapterId == chapter);
        }

        if (category is { } value)
        {
            rows = rows.Where(mastery => mastery.Category == value);
        }

        return await rows.OrderBy(mastery => mastery.Mastery).ThenBy(mastery => mastery.ItemId).Take(2000).ToListAsync(cancellationToken);
    }

    public async Task<WeaknessesDto> WeaknessesAsync(IReadOnlyList<ItemMastery> rows, CancellationToken cancellationToken)
    {
        var report = WeaknessAggregation.Aggregate(rows.Select(ToRow), Settings);
        var weakestKeys = report.WeakestItems.Select(item => (item.ItemType, item.ItemId)).ToHashSet();
        var weakest = await ToDtosAsync([.. rows.Where(row => weakestKeys.Contains((row.ItemType, row.ItemId)))], cancellationToken);
        return new WeaknessesDto(
            [.. report.Groups.Select(group => new WeaknessGroupDto(group.Kind, group.Key, group.ItemCount, group.AverageMastery, group.Accuracy, group.WeakCount))],
            [.. weakest.OrderBy(item => item.Mastery).ThenByDescending(item => item.Attempts)]);
    }

    public async Task<IReadOnlyList<MasteryItemDto>> ToDtosAsync(IReadOnlyList<ItemMastery> rows, CancellationToken cancellationToken)
    {
        var labels = await LabelsAsync(rows, cancellationToken);
        var settings = Settings;
        return
        [
            .. rows.Select(row => new MasteryItemDto(
                row.ItemType, row.ItemId, labels.GetValueOrDefault((row.ItemType, row.ItemId)), row.Attempts, row.Correct, row.Streak, row.Mastery,
                MasteryRules.StatusOf(row.Attempts, row.Mastery, row.Streak, settings), row.ChapterId, row.Category,
                [.. row.Tags.Where(tag => !QuestionRules.IsInternalTag(tag))], row.LastSeenAt)),
        ];
    }

    public static MasteryRow ToRow(ItemMastery row) =>
        new(row.ItemType, row.ItemId, row.Attempts, row.Correct, row.Streak, row.Mastery, row.ChapterId, row.Category, row.Tags, row.LastSeenAt);

    private async Task<Dictionary<(MasteryItemType, Guid), TextBlock>> LabelsAsync(IReadOnlyList<ItemMastery> rows, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(MasteryItemType, Guid), TextBlock>();
        var referenceKeys = rows
            .Where(row => row.ItemType != MasteryItemType.Question)
            .Select(row => (ToReferenceKind(row.ItemType), row.ItemId))
            .Distinct()
            .ToList();
        foreach (var label in await reference.GetLabelsAsync(referenceKeys, cancellationToken))
        {
            result[(FromReferenceKind(label.Kind), label.Id)] = label.Label;
        }

        var questionIds = rows.Where(row => row.ItemType == MasteryItemType.Question).Select(row => row.ItemId).Distinct().ToList();
        if (questionIds.Count > 0)
        {
            foreach (var question in await db.Questions.AsNoTracking()
                         .Where(question => questionIds.Contains(question.Id))
                         .Select(question => new { question.Id, question.Prompt })
                         .ToListAsync(cancellationToken))
            {
                result[(MasteryItemType.Question, question.Id)] = question.Prompt;
            }
        }

        return result;
    }

    private static ReferenceItemKind ToReferenceKind(MasteryItemType type) => type switch
    {
        MasteryItemType.Kana => ReferenceItemKind.Kana,
        MasteryItemType.Kanji => ReferenceItemKind.Kanji,
        MasteryItemType.Vocab => ReferenceItemKind.Vocab,
        MasteryItemType.Grammar => ReferenceItemKind.Grammar,
        _ => ReferenceItemKind.Sentence,
    };

    private static MasteryItemType FromReferenceKind(ReferenceItemKind kind) => kind switch
    {
        ReferenceItemKind.Kana => MasteryItemType.Kana,
        ReferenceItemKind.Kanji => MasteryItemType.Kanji,
        ReferenceItemKind.Vocab => MasteryItemType.Vocab,
        ReferenceItemKind.Grammar => MasteryItemType.Grammar,
        _ => MasteryItemType.Sentence,
    };
}
