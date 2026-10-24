using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Infrastructure;

namespace Nomax.Modules.Practice.Features.Drill;

/// <summary>What a student may practise: accessible chapters (BR-AKSES-01) and the teachers of their active classes.</summary>
internal sealed record PoolScope(IReadOnlySet<Guid> AccessibleChapters, IReadOnlyList<Guid> TeacherIds);

/// <summary>A pool question with its mastery key (docs/03 §7.5: the reference item, or the question itself).</summary>
internal sealed record PoolEntry(Guid QuestionId, MasteryItemType ItemType, Guid ItemId);

/// <summary>BR-DRILL-02: the question pool of a student and session, filtered in SQL.</summary>
internal sealed class DrillPool(PracticeDbContext db, IClassAccessReader classes)
{
    private static readonly QuestionKind[] AllKinds = Enum.GetValues<QuestionKind>();

    public async Task<PoolScope> ScopeForAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var chapters = await classes.GetAccessibleChapterIdsAsync(studentId, cancellationToken);
        var teacherIds = new HashSet<Guid>();
        foreach (var classId in await classes.GetStudentClassIdsAsync(studentId, cancellationToken))
        {
            if (await classes.GetClassInfoAsync(classId, cancellationToken) is { Status: ClassStatus.Active } info)
            {
                teacherIds.UnionWith(info.TeacherIds);
            }
        }

        return new PoolScope(chapters, [.. teacherIds]);
    }

    /// <summary>
    /// Published questions of the category in the chosen (default: all) accessible chapters, of kinds that fit the input
    /// mode, either builtin or private questions of a teacher of one of the student's active classes.
    /// </summary>
    public IQueryable<Question> Query(PoolScope scope, Category category, DrillConfig config)
    {
        var chapters = (config.ChapterIds.Count == 0 ? scope.AccessibleChapters : config.ChapterIds.Where(scope.AccessibleChapters.Contains)).ToList();
        var kinds = AllKinds.Where(kind => QuestionRules.IsDrillable(kind, config.InputMode)).ToList();
        var teachers = scope.TeacherIds.ToList();
        return db.Questions.AsNoTracking().Where(question =>
            question.Status == QuestionStatus.Published
            && question.Category == category
            && chapters.Contains(question.ChapterId)
            && kinds.Contains(question.Kind)
            && (question.Scope == QuestionScope.Builtin || teachers.Contains(question.OwnerId)));
    }

    public static async Task<IReadOnlyList<PoolEntry>> EntriesAsync(IQueryable<Question> pool, CancellationToken cancellationToken) =>
        [
            .. (await pool.Select(question => new { question.Id, question.ItemType, question.ItemId }).ToListAsync(cancellationToken))
                .Select(question => new PoolEntry(question.Id, MasteryKey.TypeOf(question.ItemType), question.ItemId ?? question.Id)),
        ];
}

/// <summary>Which mastery row an answer updates (BR-MASTERY-01).</summary>
internal static class MasteryKey
{
    public static MasteryItemType TypeOf(RefItemType? itemType) => itemType switch
    {
        RefItemType.Kana => MasteryItemType.Kana,
        RefItemType.Kanji => MasteryItemType.Kanji,
        RefItemType.Vocab => MasteryItemType.Vocab,
        RefItemType.Grammar => MasteryItemType.Grammar,
        RefItemType.Sentence => MasteryItemType.Sentence,
        _ => MasteryItemType.Question,
    };

    public static (MasteryItemType Type, Guid Id) Of(Question question) => (TypeOf(question.ItemType), question.ItemId ?? question.Id);
}
