using Nomax.Modules.Classroom.Contracts;

namespace Nomax.Modules.Classroom.Domain;

/// <summary>A student's membership in one class, with that class's status and unlocked chapters.</summary>
public sealed record MembershipView(Guid ClassId, MemberStatus MemberStatus, ClassStatus ClassStatus, IReadOnlySet<Guid> UnlockedChapterIds);

/// <summary>
/// BR-AKSES-01 ✅: a student may open chapter C when some class K has the student as an <c>active</c> member, K is
/// <c>active</c> or <c>archived</c> (archived = read only), and K unlocked C. Access is the union over all classes;
/// draft classes give no access.
/// </summary>
public static class ChapterAccess
{
    public static bool GrantsAccess(MemberStatus memberStatus, ClassStatus classStatus) =>
        memberStatus == MemberStatus.Active && classStatus is ClassStatus.Active or ClassStatus.Archived;

    public static bool IsAccessible(IEnumerable<MembershipView> memberships, Guid chapterId) =>
        (memberships ?? []).Any(membership =>
            GrantsAccess(membership.MemberStatus, membership.ClassStatus) && membership.UnlockedChapterIds.Contains(chapterId));

    public static IReadOnlySet<Guid> AccessibleChapters(IEnumerable<MembershipView> memberships) =>
        (memberships ?? [])
            .Where(membership => GrantsAccess(membership.MemberStatus, membership.ClassStatus))
            .SelectMany(membership => membership.UnlockedChapterIds)
            .ToHashSet();
}

/// <summary>BR-AKSES-03: teachers of a class.</summary>
public static class TeacherRules
{
    /// <summary>The last teacher of a class cannot be removed.</summary>
    public static bool CanRemove(int teacherCount) => teacherCount > 1;

    /// <summary>Adding/removing other teachers: the primary teacher or an admin.</summary>
    public static bool CanManageTeachers(bool isAdmin, bool isPrimaryTeacher) => isAdmin || isPrimaryTeacher;

    /// <summary>When the primary teacher leaves, the teacher who joined first takes over.</summary>
    public static Guid? NextPrimary(IEnumerable<(Guid TeacherId, DateTimeOffset AddedAt)> remaining) =>
        (remaining ?? []).OrderBy(teacher => teacher.AddedAt).ThenBy(teacher => teacher.TeacherId).Select(teacher => (Guid?)teacher.TeacherId).FirstOrDefault();
}
