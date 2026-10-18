using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Domain;

/// <summary>
/// content.materials (docs/03 §5.4). A material belongs to a chapter and is visible to every class that unlocked the
/// chapter (D-26). Exactly one of <see cref="Body"/>, <see cref="FileId"/>, <see cref="Url"/> is set, matching <see cref="Kind"/>.
/// </summary>
public sealed class Material : IHasTimestamps, IHasVersion
{
    public const int UrlMaxLength = 2048;

    private Material()
    {
    }

    public Guid Id { get; private set; }

    public Guid ChapterId { get; private set; }

    public MaterialKind Kind { get; private set; }

    public TextBlock Title { get; private set; } = null!;

    public RichText? Body { get; private set; }

    public Guid? FileId { get; private set; }

    public string? Url { get; private set; }

    /// <summary>Null = visible at once; a future time hides it from students until then (BR-KONTEN-01).</summary>
    public DateTimeOffset? PublishAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public int SortOrder { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Material CreateRichText(Guid id, Guid chapterId, TextBlock title, RichText body, DateTimeOffset? publishAt, Guid createdBy, int sortOrder) =>
        new()
        {
            Id = id,
            ChapterId = chapterId,
            Kind = MaterialKind.Richtext,
            Title = title,
            Body = body,
            PublishAt = publishAt,
            CreatedBy = createdBy,
            SortOrder = sortOrder,
        };

    public static Material CreateFile(Guid id, Guid chapterId, TextBlock title, Guid fileId, DateTimeOffset? publishAt, Guid createdBy, int sortOrder) =>
        new()
        {
            Id = id,
            ChapterId = chapterId,
            Kind = MaterialKind.File,
            Title = title,
            FileId = fileId,
            PublishAt = publishAt,
            CreatedBy = createdBy,
            SortOrder = sortOrder,
        };

    public static Material CreateLink(Guid id, Guid chapterId, TextBlock title, string url, DateTimeOffset? publishAt, Guid createdBy, int sortOrder) =>
        new()
        {
            Id = id,
            ChapterId = chapterId,
            Kind = MaterialKind.Link,
            Title = title,
            Url = url,
            PublishAt = publishAt,
            CreatedBy = createdBy,
            SortOrder = sortOrder,
        };

    public void Update(TextBlock? title, int? sortOrder)
    {
        Title = title ?? Title;
        SortOrder = sortOrder ?? SortOrder;
    }

    public void ChangePublishAt(DateTimeOffset? publishAt) => PublishAt = publishAt;

    /// <summary>Only the content field of the material's own kind can change.</summary>
    public bool TryChangeContent(RichText? body, Guid? fileId, string? url)
    {
        switch (Kind)
        {
            case MaterialKind.Richtext when fileId is null && url is null:
                Body = body ?? Body;
                return true;
            case MaterialKind.File when body is null && url is null:
                FileId = fileId ?? FileId;
                return true;
            case MaterialKind.Link when body is null && fileId is null:
                Url = url ?? Url;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>BR-KONTEN-01: a student sees a material when the chapter is accessible and it is published.</summary>
public static class MaterialVisibility
{
    public static bool IsPublished(DateTimeOffset? publishAt, DateTimeOffset now) => publishAt is null || publishAt <= now;
}
