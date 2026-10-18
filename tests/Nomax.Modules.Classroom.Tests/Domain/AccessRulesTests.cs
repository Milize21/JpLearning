using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Tests.Domain;

public sealed class AccessRulesTests
{
    private static readonly Guid K1 = Guid.Parse("c0000000-0000-4000-8000-000000000001");
    private static readonly Guid K2 = Guid.Parse("c0000000-0000-4000-8000-000000000002");
    private static readonly Guid C = Guid.Parse("b2000000-0000-4000-8000-000000000001");
    private static readonly Guid Other = Guid.Parse("b2000000-0000-4000-8000-000000000009");

    private static MembershipView Member(Guid classId, MemberStatus status, ClassStatus classStatus, params Guid[] unlocked) =>
        new(classId, status, classStatus, unlocked.ToHashSet());

    [Fact]
    public void BR_AKSES_01_case1_active_member_of_a_class_that_unlocked_the_chapter_may_open_it()
    {
        ChapterAccess.IsAccessible([Member(K1, MemberStatus.Active, ClassStatus.Active, C)], C).ShouldBeTrue();
    }

    [Fact]
    public void BR_AKSES_01_case2_unlock_in_a_class_the_student_is_not_in_does_not_count()
    {
        // M is only in K1, which did not unlock C; K2 unlocked C but M is not a member of K2.
        ChapterAccess.IsAccessible([Member(K1, MemberStatus.Active, ClassStatus.Active, Other)], C).ShouldBeFalse();
    }

    [Fact]
    public void BR_AKSES_01_case3_access_is_the_union_over_all_classes()
    {
        ChapterAccess.IsAccessible(
            [Member(K1, MemberStatus.Active, ClassStatus.Active, Other), Member(K2, MemberStatus.Active, ClassStatus.Active, C)],
            C).ShouldBeTrue();
    }

    [Fact]
    public void BR_AKSES_01_case4_removed_member_has_no_access()
    {
        ChapterAccess.IsAccessible([Member(K2, MemberStatus.Removed, ClassStatus.Active, C)], C).ShouldBeFalse();
    }

    [Fact]
    public void BR_AKSES_01_case5_archived_class_still_gives_read_access()
    {
        ChapterAccess.IsAccessible([Member(K2, MemberStatus.Active, ClassStatus.Archived, C)], C).ShouldBeTrue();
    }

    [Fact]
    public void BR_AKSES_01_case6_draft_class_gives_no_access()
    {
        ChapterAccess.IsAccessible([Member(K2, MemberStatus.Active, ClassStatus.Draft, C)], C).ShouldBeFalse();
    }

    [Fact]
    public void BR_AKSES_01_accessible_chapters_is_the_union_of_qualifying_memberships()
    {
        var chapters = ChapterAccess.AccessibleChapters(
        [
            Member(K1, MemberStatus.Active, ClassStatus.Active, C),
            Member(K2, MemberStatus.Active, ClassStatus.Draft, Other),
        ]);

        chapters.ShouldBe([C]);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void BR_AKSES_03_the_last_teacher_cannot_be_removed(int teacherCount, bool canRemove)
    {
        TeacherRules.CanRemove(teacherCount).ShouldBe(canRemove);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void BR_AKSES_03_primary_teacher_or_admin_manages_teachers(bool isAdmin, bool isPrimary, bool allowed)
    {
        TeacherRules.CanManageTeachers(isAdmin, isPrimary).ShouldBe(allowed);
    }

    [Fact]
    public void BR_AKSES_03_the_earliest_remaining_teacher_becomes_primary()
    {
        var early = Guid.Parse("a0000000-0000-4000-8000-000000000005");
        var late = Guid.Parse("a0000000-0000-4000-8000-000000000006");
        var t0 = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        TeacherRules.NextPrimary([(late, t0.AddDays(2)), (early, t0)]).ShouldBe(early);
        TeacherRules.NextPrimary([]).ShouldBeNull();
    }
}
