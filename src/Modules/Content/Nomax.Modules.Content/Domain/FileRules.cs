using Nomax.SharedKernel;

namespace Nomax.Modules.Content.Domain;

/// <summary>BR-FILE-02, 06: who may upload which purpose, and when an image may be shown inline.</summary>
public static class FileRules
{
    /// <summary>BR-FILE-02 exception: a book cover is shown as an image only up to 5 MB.</summary>
    public const long MaxInlineCoverBytes = 5 * 1024 * 1024;

    /// <summary>BR-FILE-06: staff upload materials, admins covers and question imports, students only submissions.</summary>
    public static bool CanUpload(string role, FilePurpose purpose) => purpose switch
    {
        FilePurpose.Material => role is Roles.Admin or Roles.Sensei,
        FilePurpose.BookCover or FilePurpose.QuestionImport => role == Roles.Admin,
        FilePurpose.Submission => role == Roles.Murid,
        _ => false,
    };

    /// <summary>Recognized from the first bytes (magic numbers), never from the client's MIME type.</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        // RIFF....WEBP
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
