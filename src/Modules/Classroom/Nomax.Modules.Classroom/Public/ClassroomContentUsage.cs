using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Content.Contracts;

namespace Nomax.Modules.Classroom.Public;

/// <summary>Content references kept by Classroom: classes (level, book), unlocks and sessions (chapter). BR-KONTEN-03.</summary>
internal sealed class ClassroomContentUsage(ClassroomDbContext db) : IContentUsageProbe
{
    public async Task<bool> IsUsedAsync(ContentUsageTarget target, Guid id, CancellationToken cancellationToken) => target switch
    {
        ContentUsageTarget.Level => await db.Classes.AnyAsync(item => item.LevelId == id, cancellationToken),
        ContentUsageTarget.Book => await db.Classes.AnyAsync(item => item.BookId == id, cancellationToken),
        ContentUsageTarget.Chapter => await db.ClassUnlocks.AnyAsync(unlock => unlock.ChapterId == id, cancellationToken)
            || await db.ClassSessions.AnyAsync(session => session.ChapterId == id, cancellationToken),
        _ => false,
    };

    public Task<IReadOnlySet<Guid>> GetUsedFileIdsAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}
