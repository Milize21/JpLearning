namespace Nomax.Modules.Content.Contracts;

/// <param name="Purpose"><c>material</c>, <c>book_cover</c>, <c>submission</c> or <c>question_import</c>.</param>
/// <param name="Status"><c>uploading</c>, <c>ready</c>, <c>failed</c> or <c>deleted</c>.</param>
public sealed record StoredFileInfo(Guid Id, string OriginalName, string Mime, long SizeBytes, Guid UploadedBy, string Purpose, string Status);

/// <summary>
/// Files for Practice (assignment submissions, docs/02 §6.2): validating uploads by status, uploader and purpose
/// (BR-TUGAS-01) and creating signed download links once the caller checked access (BR-FILE-04).
/// </summary>
public interface IStoredFileService
{
    /// <summary>Unknown ids are absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, StoredFileInfo>> GetFilesAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken);

    /// <summary>A short-lived signed link (relative URL) to download the file as an attachment.</summary>
    string CreateDownloadUrl(Guid fileId);
}
