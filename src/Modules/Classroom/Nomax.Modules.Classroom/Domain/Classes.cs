using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom.Contracts;

namespace Nomax.Modules.Classroom.Domain;

/// <summary>classroom.classes (docs/03 §6.1). Level and book live in Content and are validated through its Contracts.</summary>
public sealed class StudyClass : IHasTimestamps
{
    public const int NameMaxLength = 100;

    private StudyClass()
    {
    }

    public Guid Id { get; private set; }

    public Guid LevelId { get; private set; }

    /// <summary>A book of <see cref="LevelId"/>; only its chapters can be unlocked.</summary>
    public Guid BookId { get; private set; }

    /// <summary>Operational text, e.g. "N5 Sabtu Pagi".</summary>
    public string Name { get; private set; } = string.Empty;

    public ClassStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static StudyClass Create(Guid id, Guid levelId, Guid bookId, string name, ClassStatus status, Guid createdBy) =>
        new() { Id = id, LevelId = levelId, BookId = bookId, Name = name.Trim(), Status = status, CreatedBy = createdBy };

    public void Rename(string name) => Name = name.Trim();

    /// <summary>Draft ↔ active. Archiving is its own action and cannot be undone here.</summary>
    public bool TrySetStatus(ClassStatus status)
    {
        if (Status == ClassStatus.Archived || status == ClassStatus.Archived)
        {
            return status == Status;
        }

        Status = status;
        return true;
    }

    public void Archive() => Status = ClassStatus.Archived;
}

/// <summary>classroom.class_teachers (docs/03 §6.2): <c>Mengajar(S, K)</c>; one primary teacher per class.</summary>
public sealed class ClassTeacher : IHasTimestamps
{
    private ClassTeacher()
    {
    }

    public Guid ClassId { get; private set; }

    public Guid TeacherId { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ClassTeacher Create(Guid classId, Guid teacherId, bool isPrimary) =>
        new() { ClassId = classId, TeacherId = teacherId, IsPrimary = isPrimary };

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}

/// <summary>classroom.class_students (docs/03 §6.3): <c>Anggota(M, K)</c> when <see cref="Status"/> is active.</summary>
public sealed class ClassStudent : IHasTimestamps
{
    private ClassStudent()
    {
    }

    public Guid ClassId { get; private set; }

    public Guid StudentId { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public MemberStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ClassStudent Join(Guid classId, Guid studentId, DateTimeOffset now) =>
        new() { ClassId = classId, StudentId = studentId, JoinedAt = now, Status = MemberStatus.Active };

    /// <summary>A removed student added again becomes active with a new join time.</summary>
    public void Rejoin(DateTimeOffset now)
    {
        if (Status == MemberStatus.Removed)
        {
            Status = MemberStatus.Active;
            JoinedAt = now;
        }
    }

    public void Remove() => Status = MemberStatus.Removed;
}

/// <summary>classroom.class_unlocks (docs/03 §6.4). Locking a chapter deletes the row.</summary>
public sealed class ClassUnlock : IHasTimestamps
{
    private ClassUnlock()
    {
    }

    public Guid ClassId { get; private set; }

    public Guid ChapterId { get; private set; }

    public Guid UnlockedBy { get; private set; }

    public DateTimeOffset UnlockedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ClassUnlock Create(Guid classId, Guid chapterId, Guid unlockedBy, DateTimeOffset now) =>
        new() { ClassId = classId, ChapterId = chapterId, UnlockedBy = unlockedBy, UnlockedAt = now };
}
