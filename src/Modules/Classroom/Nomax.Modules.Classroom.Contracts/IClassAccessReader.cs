namespace Nomax.Modules.Classroom.Contracts;

/// <summary>Lifecycle of a class (docs/03 §3.3 <c>class_status</c>).</summary>
public enum ClassStatus
{
    Draft,
    Active,
    Archived,
}

/// <param name="TeacherIds">All teachers; the primary teacher first.</param>
public sealed record ClassInfo(Guid Id, string Name, Guid LevelId, Guid BookId, ClassStatus Status, IReadOnlyList<Guid> TeacherIds);

/// <summary>
/// Ownership relations of docs/01 §2 for Content, Practice and Insight (docs/02 §6.2). "Student" always means an
/// <c>active</c> member; teachers include admins that were added as teachers, but not admins in general.
/// </summary>
public interface IClassAccessReader
{
    /// <summary><c>Mengajar(S, K)</c>.</summary>
    Task<bool> IsTeacherOfAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken);

    /// <summary><c>Anggota(M, K)</c>: active membership.</summary>
    Task<bool> IsStudentOfAsync(Guid studentId, Guid classId, CancellationToken cancellationToken);

    /// <summary><c>SenseiDari(S, M)</c>: S teaches a class where M is an active member.</summary>
    Task<bool> CanTeacherSeeStudentAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>BR-AKSES-01: union over the student's active memberships in active or archived classes.</summary>
    Task<bool> IsChapterAccessibleAsync(Guid studentId, Guid chapterId, CancellationToken cancellationToken);

    /// <summary>BR-AKSES-01 for every chapter at once.</summary>
    Task<IReadOnlySet<Guid>> GetAccessibleChapterIdsAsync(Guid studentId, CancellationToken cancellationToken);

    /// <summary>Classes with an active membership (any class status).</summary>
    Task<IReadOnlyList<Guid>> GetStudentClassIdsAsync(Guid studentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetTeacherClassIdsAsync(Guid teacherId, CancellationToken cancellationToken);

    Task<ClassInfo?> GetClassInfoAsync(Guid classId, CancellationToken cancellationToken);

    /// <summary>Active members of the class.</summary>
    Task<IReadOnlyList<Guid>> GetClassStudentIdsAsync(Guid classId, CancellationToken cancellationToken);
}
