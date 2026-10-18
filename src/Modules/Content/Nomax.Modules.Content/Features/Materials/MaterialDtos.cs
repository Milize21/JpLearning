using Nomax.Modules.Content.Domain;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Materials;

/// <summary>Name and size of a file material; the bytes come through <c>GET /materials/{id}/download</c>.</summary>
public sealed record MaterialFileDto(Guid Id, string OriginalName, long SizeBytes, FileStatus Status);

public sealed record MaterialDto(
    Guid Id,
    Guid ChapterId,
    MaterialKind Kind,
    TextBlock Title,
    RichText? Body,
    string? Url,
    MaterialFileDto? File,
    DateTimeOffset? PublishAt,
    Guid CreatedBy,
    int SortOrder,
    uint Version,
    DateTimeOffset CreatedAt)
{
    public static MaterialDto From(Material material, StoredFile? file) => new(
        material.Id,
        material.ChapterId,
        material.Kind,
        material.Title,
        material.Body,
        material.Url,
        file is null ? null : new MaterialFileDto(file.Id, file.OriginalName, file.SizeBytes, file.Status),
        material.PublishAt,
        material.CreatedBy,
        material.SortOrder,
        material.Version,
        material.CreatedAt);
}
