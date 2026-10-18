using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;

namespace Nomax.Modules.Content.Infrastructure.Files;

/// <summary>
/// Hourly job (docs/02 §11): expired tus uploads become <c>failed</c> and their bytes are removed (BR-FILE-03); ready files
/// that nothing refers to for <see cref="UploadOptions.OrphanDays"/> become <c>deleted</c> (BR-FILE-05); the bytes of
/// deleted files are removed from the storage.
/// </summary>
internal sealed partial class UploadCleanup(
    IServiceScopeFactory scopeFactory,
    TusUploads uploads,
    TimeProvider timeProvider,
    ILogger<UploadCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), timeProvider);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await uploads.Store.RemoveExpiredFilesAsync(cancellationToken);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var usage = scope.ServiceProvider.GetRequiredService<ContentUsage>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<UploadOptions>>().Value;
        var now = timeProvider.GetUtcNow();

        var expiredBefore = now.AddHours(-options.IncompleteExpiryHours);
        var expired = await db.StoredFiles
            .Where(file => file.Status == FileStatus.Uploading && file.CreatedAt < expiredBefore)
            .ToListAsync(cancellationToken);
        expired.ForEach(file => file.MarkFailed());

        var orphanBefore = now.AddDays(-options.OrphanDays);
        var candidates = await db.StoredFiles
            .Where(file => file.Status == FileStatus.Ready && file.CreatedAt < orphanBefore)
            .Where(file => !db.Materials.Any(material => material.FileId == file.Id) && !db.Books.Any(book => book.CoverFileId == file.Id))
            .ToListAsync(cancellationToken);
        if (candidates.Count > 0)
        {
            var usedElsewhere = await usage.FilesUsedElsewhereAsync([.. candidates.Select(file => file.Id)], cancellationToken);
            foreach (var file in candidates.Where(file => !usedElsewhere.Contains(file.Id)))
            {
                file.MarkDeleted();
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Bytes of deleted files (orphans above, deleted materials) go; deleting a missing file is a no-op.
        var recentlyDeleted = await db.StoredFiles.AsNoTracking()
            .Where(file => file.Status == FileStatus.Deleted && file.UpdatedAt > now.AddDays(-7))
            .Select(file => file.StorageKey)
            .ToListAsync(cancellationToken);
        foreach (var key in recentlyDeleted)
        {
            await storage.DeleteAsync(key, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload cleanup failed; it runs again in an hour")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
