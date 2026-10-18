using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Infrastructure;

namespace Nomax.Modules.Classroom.Public;

/// <summary>
/// <see cref="IClassAccessReader"/> (docs/02 §6.2). The chapter queries are the SQL form of <see cref="ChapterAccess"/>
/// (BR-AKSES-01): active membership, class active or archived, and an unlock row.
/// </summary>
internal sealed class ClassAccessReader(ClassroomDbContext db) : IClassAccessReader
{
    public Task<bool> IsTeacherOfAsync(Guid teacherId, Guid classId, CancellationToken cancellationToken) =>
        db.ClassTeachers.AnyAsync(teacher => teacher.ClassId == classId && teacher.TeacherId == teacherId, cancellationToken);

    public Task<bool> IsStudentOfAsync(Guid studentId, Guid classId, CancellationToken cancellationToken) =>
        db.ClassStudents.AnyAsync(
            member => member.ClassId == classId && member.StudentId == studentId && member.Status == MemberStatus.Active,
            cancellationToken);

    public Task<bool> CanTeacherSeeStudentAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken) =>
        db.ClassTeachers
            .Where(teacher => teacher.TeacherId == teacherId)
            .Join(db.ClassStudents, teacher => teacher.ClassId, member => member.ClassId, (_, member) => member)
            .AnyAsync(member => member.StudentId == studentId && member.Status == MemberStatus.Active, cancellationToken);

    public Task<bool> IsChapterAccessibleAsync(Guid studentId, Guid chapterId, CancellationToken cancellationToken) =>
        AccessibleUnlocks(studentId).AnyAsync(unlock => unlock.ChapterId == chapterId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetAccessibleChapterIdsAsync(Guid studentId, CancellationToken cancellationToken) =>
        (await AccessibleUnlocks(studentId).Select(unlock => unlock.ChapterId).Distinct().ToListAsync(cancellationToken)).ToHashSet();

    public async Task<IReadOnlyList<Guid>> GetStudentClassIdsAsync(Guid studentId, CancellationToken cancellationToken) =>
        await db.ClassStudents.AsNoTracking()
            .Where(member => member.StudentId == studentId && member.Status == MemberStatus.Active)
            .Select(member => member.ClassId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetTeacherClassIdsAsync(Guid teacherId, CancellationToken cancellationToken) =>
        await db.ClassTeachers.AsNoTracking()
            .Where(teacher => teacher.TeacherId == teacherId)
            .Select(teacher => teacher.ClassId)
            .ToListAsync(cancellationToken);

    public async Task<ClassInfo?> GetClassInfoAsync(Guid classId, CancellationToken cancellationToken)
    {
        var item = await db.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var teachers = await db.ClassTeachers.AsNoTracking()
            .Where(teacher => teacher.ClassId == classId)
            .OrderByDescending(teacher => teacher.IsPrimary)
            .ThenBy(teacher => teacher.CreatedAt)
            .Select(teacher => teacher.TeacherId)
            .ToListAsync(cancellationToken);
        return new ClassInfo(item.Id, item.Name, item.LevelId, item.BookId, item.Status, teachers);
    }

    public async Task<IReadOnlyList<Guid>> GetClassStudentIdsAsync(Guid classId, CancellationToken cancellationToken) =>
        await db.ClassStudents.AsNoTracking()
            .Where(member => member.ClassId == classId && member.Status == MemberStatus.Active)
            .Select(member => member.StudentId)
            .ToListAsync(cancellationToken);

    private IQueryable<ClassUnlock> AccessibleUnlocks(Guid studentId) =>
        from member in db.ClassStudents.AsNoTracking()
        join item in db.Classes.AsNoTracking() on member.ClassId equals item.Id
        join unlock in db.ClassUnlocks.AsNoTracking() on item.Id equals unlock.ClassId
        where member.StudentId == studentId
            && member.Status == MemberStatus.Active
            && (item.Status == ClassStatus.Active || item.Status == ClassStatus.Archived)
        select unlock;
}
