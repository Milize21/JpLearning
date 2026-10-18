using System.ComponentModel.DataAnnotations;

namespace Nomax.Modules.Content.Infrastructure.Files;

/// <summary>Configuration section <c>Storage</c> (docs/02 §14). Relative paths are under the API content root.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Only <c>disk</c> exists; an S3-compatible store can be added behind <see cref="IFileStorage"/> (D-05).</summary>
    public string Provider { get; set; } = "disk";

    [Required]
    public string RootPath { get; set; } = "storage/files";

    /// <summary>Unfinished tus uploads.</summary>
    [Required]
    public string UploadPath { get; set; } = "storage/uploads";

    /// <summary>
    /// Base64 key (≥ 32 bytes) for the HMAC of signed download URLs. Required outside development; in development a
    /// random key per process is used when it is missing (old links then simply expire).
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>BR-FILE-04: 5 minutes.</summary>
    [Range(10, 86_400)]
    public int SignedUrlTtlSeconds { get; set; } = 300;
}

/// <summary>Configuration section <c>Upload</c> with the defaults of docs/04 §12.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    /// <summary>BR-FILE-01: 1 GiB.</summary>
    [Range(1, 1_073_741_824)]
    public long MaxFileBytes { get; set; } = 1_073_741_824;

    /// <summary>BR-FILE-03.</summary>
    [Range(1, 720)]
    public int IncompleteExpiryHours { get; set; } = 24;

    /// <summary>BR-FILE-05.</summary>
    [Range(1, 365)]
    public int OrphanDays { get; set; } = 7;

    /// <summary>BR-FILE-06.</summary>
    [Range(1, 10_000)]
    public int MaxUploadsPerHour { get; set; } = 20;
}
