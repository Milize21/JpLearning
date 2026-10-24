using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Domain;

/// <summary>practice.assignments (docs/03 §7.10).</summary>
public sealed class Assignment : IHasTimestamps, IHasVersion
{
    private Assignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClassId { get; private set; }

    public Guid? ChapterId { get; private set; }

    public TextBlock Title { get; private set; } = null!;

    public RichText Instructions { get; private set; } = null!;

    public DateTimeOffset DueAt { get; private set; }

    public bool AllowResubmit { get; private set; }

    public PublishStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Assignment Create(Guid id, Guid classId, Guid? chapterId, TextBlock title, RichText instructions, DateTimeOffset dueAt, bool allowResubmit, Guid createdBy) =>
        new()
        {
            Id = id,
            ClassId = classId,
            ChapterId = chapterId,
            Title = title,
            Instructions = instructions,
            DueAt = dueAt,
            AllowResubmit = allowResubmit,
            Status = PublishStatus.Draft,
            CreatedBy = createdBy,
        };

    public void Update(TextBlock title, Guid? chapterId, RichText instructions, DateTimeOffset dueAt, bool allowResubmit)
    {
        Title = title;
        ChapterId = chapterId;
        Instructions = instructions;
        DueAt = dueAt;
        AllowResubmit = allowResubmit;
    }

    public void ChangeStatus(PublishStatus status) => Status = status;
}

/// <summary>practice.submissions (docs/03 §7.11): one per student and assignment; a resend overwrites it.</summary>
public sealed class Submission : IHasTimestamps
{
    public const int TextMaxLength = 20_000;
    public const int FeedbackMaxLength = 5_000;
    public const int MaxFiles = 10;

    private Submission()
    {
    }

    public Guid Id { get; private set; }

    public Guid AssignmentId { get; private set; }

    public Guid StudentId { get; private set; }

    public string? Text { get; private set; }

    public List<Guid> FileIds { get; private set; } = [];

    public DateTimeOffset SubmittedAt { get; private set; }

    public decimal? GradePct { get; private set; }

    public string? Feedback { get; private set; }

    public Guid? GradedBy { get; private set; }

    public DateTimeOffset? GradedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Submission Create(Guid id, Guid assignmentId, Guid studentId, string? text, IEnumerable<Guid> fileIds, DateTimeOffset at) =>
        new() { Id = id, AssignmentId = assignmentId, StudentId = studentId, Text = text, FileIds = [.. fileIds], SubmittedAt = at };

    /// <summary>BR-TUGAS-02: the content is replaced and the time renewed.</summary>
    public void Resubmit(string? text, IEnumerable<Guid> fileIds, DateTimeOffset at)
    {
        Text = text;
        FileIds = [.. fileIds];
        SubmittedAt = at;
    }

    /// <summary>BR-TUGAS-04; grades may change later.</summary>
    public void Grade(decimal gradePct, string? feedback, Guid gradedBy, DateTimeOffset at)
    {
        GradePct = gradePct;
        Feedback = feedback;
        GradedBy = gradedBy;
        GradedAt = at;
    }
}

/// <summary>BR-TUGAS-01…03 as pure functions.</summary>
public static class AssignmentRules
{
    /// <summary>BR-TUGAS-02: a resend needs allow_resubmit and no grade yet.</summary>
    public static bool CanResubmit(bool allowResubmit, decimal? gradePct) => allowResubmit && gradePct is null;

    /// <summary>BR-TUGAS-03 (D-20): late submissions are accepted and marked.</summary>
    public static bool IsLate(DateTimeOffset submittedAt, DateTimeOffset dueAt) => submittedAt > dueAt;

    /// <summary>BR-TUGAS-01: text or at least one file.</summary>
    public static bool HasContent(string? text, IReadOnlyCollection<Guid> fileIds) =>
        !string.IsNullOrWhiteSpace(text) || fileIds.Count > 0;
}
