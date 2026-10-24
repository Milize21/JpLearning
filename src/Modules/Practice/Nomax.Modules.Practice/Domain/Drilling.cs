using System.Text.Json;
using Nomax.BuildingBlocks.Persistence;

namespace Nomax.Modules.Practice.Domain;

/// <summary><c>drill_sessions.config</c> (docs/03 §7.3).</summary>
/// <param name="ChapterIds">Chapters chosen by the student; empty = every accessible chapter (BR-DRILL-02).</param>
public sealed record DrillConfig(IReadOnlyList<Guid> ChapterIds, DrillMode Mode, InputMode InputMode);

/// <summary>practice.drill_sessions (docs/03 §7.3).</summary>
public sealed class DrillSession : IHasTimestamps
{
    private DrillSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid? ClassId { get; private set; }

    public Category Category { get; private set; }

    public DrillConfig Config { get; private set; } = null!;

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Null while running (BR-DRILL-08).</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DrillSession Start(Guid id, Guid studentId, Guid? classId, Category category, DrillConfig config, DateTimeOffset now) =>
        new() { Id = id, StudentId = studentId, ClassId = classId, Category = category, Config = config, StartedAt = now };

    public void End(DateTimeOffset endedAt) => EndedAt ??= endedAt;
}

/// <summary>practice.drill_answers (docs/03 §7.4). The id comes from the client so a resend is idempotent (BR-DRILL-07).</summary>
public sealed class DrillAnswer : IHasTimestamps
{
    private DrillAnswer()
    {
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid QuestionId { get; private set; }

    public InputMode InputMode { get; private set; }

    public JsonElement Answer { get; private set; }

    public bool IsCorrect { get; private set; }

    public int? ResponseMs { get; private set; }

    /// <summary>Server time of the answer (docs/03 §7.4).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DrillAnswer Create(Guid id, Guid sessionId, Guid questionId, InputMode inputMode, JsonElement answer, bool isCorrect, int? responseMs) =>
        new()
        {
            Id = id,
            SessionId = sessionId,
            QuestionId = questionId,
            InputMode = inputMode,
            Answer = answer.Clone(),
            IsCorrect = isCorrect,
            ResponseMs = responseMs,
        };
}

/// <summary>practice.item_masteries (docs/03 §7.5): one row per student and item (or question without an item).</summary>
public sealed class ItemMastery : IHasTimestamps
{
    private ItemMastery()
    {
    }

    public Guid StudentId { get; private set; }

    public MasteryItemType ItemType { get; private set; }

    public Guid ItemId { get; private set; }

    public int Attempts { get; private set; }

    public int Correct { get; private set; }

    public int Streak { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public decimal Mastery { get; private set; }

    public Guid LevelId { get; private set; }

    public Guid ChapterId { get; private set; }

    public Category Category { get; private set; }

    public List<string> Tags { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public MasteryState State => new(Attempts, Correct, Streak, Mastery);

    public static ItemMastery Create(Guid studentId, MasteryItemType itemType, Guid itemId) =>
        new() { StudentId = studentId, ItemType = itemType, ItemId = itemId };

    /// <summary>BR-MASTERY-01: the new counters, plus the dimension columns copied from the answered question.</summary>
    public void Apply(MasteryState state, DateTimeOffset seenAt, Guid levelId, Guid chapterId, Category category, IEnumerable<string> tags)
    {
        Attempts = state.Attempts;
        Correct = state.Correct;
        Streak = state.Streak;
        Mastery = state.Mastery;
        LastSeenAt = seenAt;
        LevelId = levelId;
        ChapterId = chapterId;
        Category = category;
        Tags = [.. tags];
    }
}

/// <summary>practice.comments (docs/03 §7.14): feedback without a grade (BR-DRILL-01).</summary>
public sealed class Comment : IHasTimestamps
{
    public const int TextMaxLength = 2000;

    private Comment()
    {
    }

    public Guid Id { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid AuthorId { get; private set; }

    public CommentTarget TargetType { get; private set; }

    /// <summary>Drill session, attempt or submission id; the class id for <c>progress</c>.</summary>
    public Guid TargetId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Comment Create(Guid id, Guid studentId, Guid authorId, CommentTarget targetType, Guid targetId, string text) =>
        new() { Id = id, StudentId = studentId, AuthorId = authorId, TargetType = targetType, TargetId = targetId, Text = text.Trim() };
}
