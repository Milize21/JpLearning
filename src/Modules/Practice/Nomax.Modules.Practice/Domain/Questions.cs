using System.Text.Json;
using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Domain;

/// <summary>practice.question_sources (docs/03 §7.1). Every question has one (BR-SOAL-01).</summary>
public sealed class QuestionSource : IHasTimestamps
{
    public const int NameMaxLength = 200;
    public const int UrlMaxLength = 2048;
    public const int LicenseMaxLength = 100;

    /// <summary>Source of questions written by staff (docs/03 §7.1, BR-SOAL-01).</summary>
    public const string InternalName = "Internal NomaX";
    public const string InternalLicense = "internal";

    private QuestionSource()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Url { get; private set; }

    public string License { get; private set; } = string.Empty;

    public Guid? ImportedFileId { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static QuestionSource Create(Guid id, string name, string? url, string license, Guid? importedFileId = null) =>
        new() { Id = id, Name = name.Trim(), Url = url, License = license.Trim(), ImportedFileId = importedFileId };

    public void Update(string? name, string? url, string? license)
    {
        Name = name?.Trim() ?? Name;
        Url = url ?? Url;
        License = license?.Trim() ?? License;
    }

    public void MarkReviewed(Guid reviewer, DateTimeOffset at)
    {
        ReviewedBy = reviewer;
        ReviewedAt = at;
    }
}

/// <summary>
/// practice.questions (docs/03 §7.2). <see cref="Payload"/> holds the content and the answer key per <see cref="Kind"/>
/// (§8.1) and is never sent to students as is (§8.3).
/// </summary>
public sealed class Question : IHasTimestamps, IHasVersion
{
    public const int MaxTags = 20;
    public const int TagMaxLength = 64;

    private Question()
    {
    }

    public Guid Id { get; private set; }

    public QuestionKind Kind { get; private set; }

    public Guid LevelId { get; private set; }

    public Guid ChapterId { get; private set; }

    public Category Category { get; private set; }

    public List<string> Tags { get; private set; } = [];

    public TextBlock Prompt { get; private set; } = null!;

    public JsonElement Payload { get; private set; }

    public RichText? Explanation { get; private set; }

    public RefItemType? ItemType { get; private set; }

    public Guid? ItemId { get; private set; }

    public QuestionStatus Status { get; private set; }

    public QuestionScope Scope { get; private set; }

    public Guid SourceId { get; private set; }

    public Guid OwnerId { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Question Create(
        Guid id, QuestionKind kind, Guid levelId, Guid chapterId, Category category, IEnumerable<string> tags,
        TextBlock prompt, JsonElement payload, RichText? explanation, RefItemType? itemType, Guid? itemId,
        QuestionStatus status, QuestionScope scope, Guid sourceId, Guid ownerId) =>
        new()
        {
            Id = id,
            Kind = kind,
            LevelId = levelId,
            ChapterId = chapterId,
            Category = category,
            Tags = [.. tags],
            Prompt = prompt,
            Payload = payload.Clone(),
            Explanation = explanation,
            ItemType = itemId is null ? null : itemType,
            ItemId = itemType is null ? null : itemId,
            Status = status,
            Scope = scope,
            SourceId = sourceId,
            OwnerId = ownerId,
        };

    /// <summary>BR-SOAL-04: a copy in <c>draft</c> that the caller owns; the original stays as it is.</summary>
    public Question Duplicate(Guid id, Guid ownerId, QuestionScope scope) =>
        Create(id, Kind, LevelId, ChapterId, Category, Tags, Prompt, Payload, Explanation, ItemType, ItemId,
            QuestionStatus.Draft, scope, SourceId, ownerId);

    /// <summary>Fields that may change even after students answered (BR-SOAL-04).</summary>
    public void UpdateTexts(TextBlock? prompt, RichText? explanation, bool clearExplanation, IEnumerable<string>? tags)
    {
        Prompt = prompt ?? Prompt;
        Explanation = clearExplanation ? null : explanation ?? Explanation;
        Tags = tags is null ? Tags : [.. tags];
    }

    /// <summary>Content fields, locked once the question has answers (BR-SOAL-04).</summary>
    public void UpdateContent(QuestionKind kind, JsonElement payload, Category category, Guid levelId, Guid chapterId)
    {
        Kind = kind;
        Payload = payload.Clone();
        Category = category;
        LevelId = levelId;
        ChapterId = chapterId;
    }

    public void ChangeStatus(QuestionStatus status) => Status = status;
}

/// <summary>BR-SOAL-02 status flow and BR-SOAL-03 visibility, as pure rules.</summary>
public static class QuestionRules
{
    /// <summary>Generated and imported questions always start in review (BR-SOAL-02).</summary>
    public static QuestionStatus InitialStatusForGenerated => QuestionStatus.Review;

    public static bool CanSubmitForReview(QuestionStatus status) => status == QuestionStatus.Draft;

    /// <summary>
    /// <c>private</c>: the owner (or an admin) publishes from draft or review. <c>builtin</c>: only admins publish.
    /// </summary>
    public static bool CanPublish(QuestionStatus status, QuestionScope scope, bool isAdmin) =>
        status is QuestionStatus.Draft or QuestionStatus.Review && (scope == QuestionScope.Private || isAdmin);

    public static bool CanArchive(QuestionStatus status) => status != QuestionStatus.Archived;

    /// <summary>Content of an archived question never changes; texts may still be corrected.</summary>
    public static bool CanEditContent(QuestionStatus status, bool hasAnswers) => !hasAnswers && status != QuestionStatus.Archived;

    /// <summary>BR-SOAL-03: builtin for all staff; private for its owner and admins.</summary>
    public static bool IsVisibleToStaff(QuestionScope scope, Guid ownerId, Guid callerId, bool isAdmin) =>
        isAdmin || scope == QuestionScope.Builtin || ownerId == callerId;

    /// <summary>Owners and admins change questions; builtin questions are managed by admins only.</summary>
    public static bool CanManage(QuestionScope scope, Guid ownerId, Guid callerId, bool isAdmin) =>
        isAdmin || (scope == QuestionScope.Private && ownerId == callerId);

    /// <summary>Categories a question kind may have in drilling; long answers only exist in quizzes (BR-DRILL-02).</summary>
    public static bool IsDrillable(QuestionKind kind, InputMode sessionInput) => sessionInput switch
    {
        InputMode.Handwrite => kind is QuestionKind.HandwriteKana or QuestionKind.HandwriteKanji,
        InputMode.Type => kind == QuestionKind.TypeAnswer,
        _ => kind is not (QuestionKind.LongAnswer or QuestionKind.HandwriteKana or QuestionKind.HandwriteKanji),
    };

    /// <summary>Generator tags (<c>gen:*</c>) mark the template for de-duplication; they are not weakness groups.</summary>
    public static bool IsInternalTag(string tag) => tag.StartsWith("gen:", StringComparison.Ordinal);
}
