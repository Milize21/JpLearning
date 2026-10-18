using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Materials;

/// <summary>A file material needs a finished <c>material</c> upload of the caller (admins may use any).</summary>
internal static class MaterialFiles
{
    public static async Task<Result<StoredFile>> CheckAsync(ContentDbContext db, Guid fileId, Guid callerId, bool isAdmin, CancellationToken cancellationToken)
    {
        var file = await db.StoredFiles.FirstOrDefaultAsync(file => file.Id == fileId, cancellationToken);
        if (file is null || file.Purpose != FilePurpose.Material || (!isAdmin && file.UploadedBy != callerId))
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["fileId"] = ["Must be one of your material uploads."] });
        }

        if (file.Status != FileStatus.Ready)
        {
            return new AppError(ErrorCodes.FileNotReady, "The upload is not finished.");
        }

        return file;
    }
}
