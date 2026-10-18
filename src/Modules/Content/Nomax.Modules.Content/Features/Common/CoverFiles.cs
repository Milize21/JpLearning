using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Common;

/// <summary>A book cover must be a finished <c>book_cover</c> upload (BR-FILE-02, BR-FILE-06).</summary>
internal sealed class CoverFiles(ContentDbContext db)
{
    public async Task<AppError?> CheckAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(file => file.Id == fileId, cancellationToken);
        if (file is null || file.Purpose != FilePurpose.BookCover)
        {
            return ContentErrors.UnknownReference("coverFileId");
        }

        return file.Status == FileStatus.Ready ? null : new AppError(ErrorCodes.FileNotReady, "The cover upload is not finished.");
    }
}
