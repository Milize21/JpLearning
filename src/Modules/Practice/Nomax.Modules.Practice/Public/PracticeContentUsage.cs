using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Practice.Infrastructure;

namespace Nomax.Modules.Practice.Public;

/// <summary>
/// Content references kept by Practice (BR-KONTEN-03, BR-FILE-05): questions (level, chapter, reference item) and
/// imported question files. Submissions (files) follow in phase 3.
/// </summary>
internal sealed class PracticeContentUsage(PracticeDbContext db) : IContentUsageProbe
{
    public async Task<bool> IsUsedAsync(ContentUsageTarget target, Guid id, CancellationToken cancellationToken) => target switch
    {
        ContentUsageTarget.Level => await db.Questions.AnyAsync(question => question.LevelId == id, cancellationToken),
        ContentUsageTarget.Chapter => await db.Questions.AnyAsync(question => question.ChapterId == id, cancellationToken)
            || await db.Quizzes.AnyAsync(quiz => quiz.ChapterId == id, cancellationToken)
            || await db.Assignments.AnyAsync(item => item.ChapterId == id, cancellationToken),
        ContentUsageTarget.ReferenceItem => await db.Questions.AnyAsync(question => question.ItemId == id, cancellationToken),
        ContentUsageTarget.File => (await GetUsedFileIdsAsync([id], cancellationToken)).Count > 0,
        _ => false,
    };

    public async Task<IReadOnlySet<Guid>> GetUsedFileIdsAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        var ids = fileIds.ToList();
        var used = (await db.QuestionSources.AsNoTracking()
                .Where(source => source.ImportedFileId != null && ids.Contains(source.ImportedFileId.Value))
                .Select(source => source.ImportedFileId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        // Submissions keep their files (BR-FILE-05); arrays are matched in SQL with the overlap operator.
        var submitted = await db.Submissions.AsNoTracking()
            .Where(submission => submission.FileIds.Any(fileId => ids.Contains(fileId)))
            .Select(submission => submission.FileIds)
            .ToListAsync(cancellationToken);
        used.UnionWith(submitted.SelectMany(list => list).Where(ids.Contains));
        return used;
    }
}
