using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Auth;
using Nomax.Modules.Content.Domain;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Serialization;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

namespace Nomax.Modules.Content.Infrastructure.Files;

/// <summary>tus file ids are the UUIDv7 primary keys of content.stored_files (docs/05 §13).</summary>
internal sealed class Uuid7FileIdProvider(TimeProvider timeProvider) : ITusFileIdProvider
{
    public Task<string> CreateId(string uploadMetadata) => Task.FromResult(Guid.CreateVersion7(timeProvider.GetUtcNow()).ToString());

    public Task<bool> ValidateId(string fileId) => Task.FromResult(Guid.TryParse(fileId, out _));
}

/// <summary>
/// tus 1.0.0 endpoint <c>/api/content/uploads</c> (docs/05 §13, D-05): creation, termination and expiration on a local disk
/// store. One store instance for the process; the events run per request with scoped services.
/// </summary>
internal sealed class TusUploads
{
    public const string RoutePattern = "/api/content/uploads";

    public TusUploads(IOptions<StorageOptions> storage, IOptions<UploadOptions> upload, IHostEnvironment environment, TimeProvider timeProvider)
    {
        UploadDirectory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, storage.Value.UploadPath));
        Directory.CreateDirectory(UploadDirectory);
        Store = new TusDiskStore(UploadDirectory, deletePartialFilesOnConcat: true, TusDiskBufferSize.Default, new Uuid7FileIdProvider(timeProvider));
        Options = upload.Value;
    }

    public string UploadDirectory { get; }

    public TusDiskStore Store { get; }

    public UploadOptions Options { get; }

    public DefaultTusConfiguration CreateConfiguration() => new()
    {
        Store = Store,
        MaxAllowedUploadSizeInBytesLong = Options.MaxFileBytes,
        Expiration = new AbsoluteExpiration(TimeSpan.FromHours(Options.IncompleteExpiryHours)),
        Events = new Events
        {
            OnAuthorizeAsync = context => Handler(context.HttpContext).AuthorizeAsync(context),
            OnBeforeCreateAsync = context => Handler(context.HttpContext).BeforeCreateAsync(context),
            OnCreateCompleteAsync = context => Handler(context.HttpContext).CreateCompleteAsync(context),
            OnFileCompleteAsync = context => Handler(context.HttpContext).FileCompleteAsync(context, this),
        },
    };

    private static UploadEvents Handler(HttpContext httpContext) => httpContext.RequestServices.GetRequiredService<UploadEvents>();
}

/// <summary>The rules of BR-FILE-01, 03, 06 around one tus request.</summary>
internal sealed partial class UploadEvents(
    ContentDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    IOptions<UploadOptions> options,
    TimeProvider timeProvider,
    ILogger<UploadEvents> logger)
{
    public async Task AuthorizeAsync(AuthorizeContext context)
    {
        switch (context.Intent)
        {
            case IntentType.CreateFile:
            case IntentType.GetOptions:
                return;
            case IntentType.ConcatenateFiles:
                context.FailRequest(HttpStatusCode.Forbidden, "Concatenation is not supported.");
                return;
            default:
                // Only the uploader may continue, inspect or cancel an upload; anyone else sees nothing (404).
                var fileId = Guid.TryParse(context.FileId, out var id) ? id : Guid.Empty;
                var owned = await db.StoredFiles.AnyAsync(
                    file => file.Id == fileId && file.UploadedBy == currentUser.Id && file.Status == FileStatus.Uploading,
                    context.CancellationToken);
                if (!owned)
                {
                    context.FailRequest(HttpStatusCode.NotFound);
                }

                return;
        }
    }

    public async Task BeforeCreateAsync(BeforeCreateContext context)
    {
        if (context.UploadLengthIsDeferred)
        {
            context.FailRequest(HttpStatusCode.BadRequest, "Upload-Length is required.");
            return;
        }

        // BR-FILE-01: refused before any byte is stored.
        if (context.UploadLength > options.Value.MaxFileBytes)
        {
            context.FailRequest(HttpStatusCode.RequestEntityTooLarge, "file_too_large");
            return;
        }

        if (Text(context.Metadata, "purpose") is not { } purposeText || !EnumSnakeCase.TryParse<FilePurpose>(purposeText, out var purpose))
        {
            context.FailRequest(HttpStatusCode.BadRequest, "Metadata 'purpose' must be material, book_cover, submission or question_import.");
            return;
        }

        if (!FileRules.CanUpload(CurrentRole(), purpose))
        {
            context.FailRequest(HttpStatusCode.Forbidden, "forbidden");
            return;
        }

        if (string.IsNullOrWhiteSpace(Text(context.Metadata, "filename")))
        {
            context.FailRequest(HttpStatusCode.BadRequest, "Metadata 'filename' is required.");
            return;
        }

        var since = timeProvider.GetUtcNow().AddHours(-1);
        var recent = await db.StoredFiles.CountAsync(file => file.UploadedBy == currentUser.Id && file.CreatedAt > since, context.CancellationToken);
        if (recent >= options.Value.MaxUploadsPerHour)
        {
            context.FailRequest(HttpStatusCode.TooManyRequests, "rate_limited");
        }
    }

    public async Task CreateCompleteAsync(CreateCompleteContext context)
    {
        var id = Guid.Parse(context.FileId);
        var purpose = EnumSnakeCase.Parse<FilePurpose>(Text(context.Metadata, "purpose")!);
        var storageKey = $"{timeProvider.GetUtcNow():yyyy/MM}/{id:N}";
        db.StoredFiles.Add(StoredFile.StartUpload(
            id,
            storageKey,
            Text(context.Metadata, "filename") ?? "file",
            Text(context.Metadata, "filetype") ?? string.Empty,
            context.UploadLength,
            currentUser.Id,
            purpose));
        await db.SaveChangesAsync(context.CancellationToken);
    }

    /// <summary>Upload complete: move the bytes into the storage, record the SHA-256, mark the file ready.</summary>
    public async Task FileCompleteAsync(FileCompleteContext context, TusUploads uploads)
    {
        var id = Guid.Parse(context.FileId);
        var file = await db.StoredFiles.FirstAsync(file => file.Id == id, context.CancellationToken);
        try
        {
            var source = Path.Combine(uploads.UploadDirectory, context.FileId);
            string sha256;
            await using (var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, context.CancellationToken));
            }

            await storage.ImportAsync(source, file.StorageKey, context.CancellationToken);
            await ((ITusTerminationStore)uploads.Store).DeleteFileAsync(context.FileId, context.CancellationToken);
            file.MarkReady(sha256);
        }
        catch (IOException exception)
        {
            LogStoreFailed(logger, id, exception);
            file.MarkFailed();
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }

    private string CurrentRole() =>
        Roles.All.FirstOrDefault(currentUser.IsInRole) ?? string.Empty;

    private static string? Text(Dictionary<string, Metadata> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && !value.HasEmptyValue ? value.GetString(Encoding.UTF8) : null;

    [LoggerMessage(Level = LogLevel.Error, Message = "Finished upload {FileId} could not be moved into the storage")]
    private static partial void LogStoreFailed(ILogger logger, Guid fileId, Exception exception);
}
