using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Content.Infrastructure;
using Nomax.Modules.Content.Infrastructure.Files;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Content.Public;

internal sealed class StoredFileService(ContentDbContext db, SignedUrls signedUrls) : IStoredFileService
{
    public async Task<IReadOnlyDictionary<Guid, StoredFileInfo>> GetFilesAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        if (fileIds.Count == 0)
        {
            return new Dictionary<Guid, StoredFileInfo>();
        }

        var ids = fileIds.Distinct().ToList();
        var files = await db.StoredFiles.AsNoTracking().Where(file => ids.Contains(file.Id)).ToListAsync(cancellationToken);
        return files.ToDictionary(
            file => file.Id,
            file => new StoredFileInfo(
                file.Id,
                file.OriginalName,
                file.Mime,
                file.SizeBytes,
                file.UploadedBy,
                EnumSnakeCase.ToValue(file.Purpose),
                EnumSnakeCase.ToValue(file.Status)));
    }

    public string CreateDownloadUrl(Guid fileId) => signedUrls.Create(fileId);
}
