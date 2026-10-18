using Nomax.BuildingBlocks.Persistence;

namespace Nomax.Modules.Content.Domain;

/// <summary>
/// content.stored_files (docs/03 §5.5). The id is also the tus upload id. The original name and MIME type come from the
/// client and are never trusted for rendering (BR-FILE-02).
/// </summary>
public sealed class StoredFile : IHasTimestamps
{
    public const int StorageKeyMaxLength = 512;
    public const int OriginalNameMaxLength = 255;
    public const int MimeMaxLength = 255;

    /// <summary>BR-FILE-01: 1 GiB, also the CHECK constraint of size_bytes.</summary>
    public const long MaxSizeBytes = 1_073_741_824;

    private StoredFile()
    {
    }

    public Guid Id { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string OriginalName { get; private set; } = string.Empty;

    public string Mime { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Hex SHA-256, filled when the upload completes.</summary>
    public string? Sha256 { get; private set; }

    public Guid UploadedBy { get; private set; }

    public FileStatus Status { get; private set; }

    public FilePurpose Purpose { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static StoredFile StartUpload(Guid id, string storageKey, string originalName, string mime, long sizeBytes, Guid uploadedBy, FilePurpose purpose) =>
        new()
        {
            Id = id,
            StorageKey = storageKey,
            OriginalName = FileNames.Sanitize(originalName),
            Mime = string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime.Trim()[..Math.Min(mime.Trim().Length, MimeMaxLength)],
            SizeBytes = sizeBytes,
            UploadedBy = uploadedBy,
            Status = FileStatus.Uploading,
            Purpose = purpose,
        };

    public void MarkReady(string sha256)
    {
        Sha256 = sha256;
        Status = FileStatus.Ready;
    }

    public void MarkFailed() => Status = FileStatus.Failed;

    public void MarkDeleted() => Status = FileStatus.Deleted;
}

/// <summary>Display and download names: no path parts or control characters, at most 255 characters.</summary>
public static class FileNames
{
    public static string Sanitize(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        // Browsers send "C:\fakepath\x" or nested paths in some cases; keep only the last part.
        var lastSeparator = trimmed.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            trimmed = trimmed[(lastSeparator + 1)..];
        }

        var cleaned = new string([.. trimmed.Where(c => !char.IsControl(c) && c is not ('"' or '<' or '>' or '|' or ':' or '*' or '?'))]).Trim();
        if (cleaned.Length == 0 || cleaned is "." or "..")
        {
            cleaned = "file";
        }

        return cleaned.Length <= StoredFile.OriginalNameMaxLength ? cleaned : cleaned[..StoredFile.OriginalNameMaxLength];
    }
}
