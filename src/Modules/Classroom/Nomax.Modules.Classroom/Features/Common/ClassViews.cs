using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Identity.Contracts;

namespace Nomax.Modules.Classroom.Features.Common;

/// <summary>Builds <see cref="ClassDto"/>s with teacher names and student counts in a few set-based queries.</summary>
internal sealed class ClassViews(ClassroomDbContext db, IUserDirectory users)
{
    public async Task<IReadOnlyList<ClassDto>> BuildAsync(IReadOnlyList<StudyClass> classes, Guid callerId, CancellationToken cancellationToken)
    {
        if (classes.Count == 0)
        {
            return [];
        }

        var ids = classes.Select(item => item.Id).ToList();
        var teachers = await db.ClassTeachers.AsNoTracking()
            .Where(teacher => ids.Contains(teacher.ClassId))
            .OrderByDescending(teacher => teacher.IsPrimary)
            .ThenBy(teacher => teacher.CreatedAt)
            .ToListAsync(cancellationToken);
        var studentCounts = await db.ClassStudents.AsNoTracking()
            .Where(member => ids.Contains(member.ClassId) && member.Status == MemberStatus.Active)
            .GroupBy(member => member.ClassId)
            .Select(group => new { ClassId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.ClassId, entry => entry.Count, cancellationToken);
        var myMemberships = await db.ClassStudents.AsNoTracking()
            .Where(member => ids.Contains(member.ClassId) && member.StudentId == callerId && member.Status == MemberStatus.Active)
            .Select(member => member.ClassId)
            .ToListAsync(cancellationToken);
        var names = await users.GetUsersAsync([.. teachers.Select(teacher => teacher.TeacherId)], cancellationToken);

        var teachersByClass = teachers.ToLookup(teacher => teacher.ClassId);
        return
        [
            .. classes.Select(item =>
            {
                var classTeachers = teachersByClass[item.Id].ToList();
                var myRole = classTeachers.Any(teacher => teacher.TeacherId == callerId) ? "teacher"
                    : myMemberships.Contains(item.Id) ? "student" : null;
                return new ClassDto(
                    item.Id,
                    item.Name,
                    item.LevelId,
                    item.BookId,
                    item.Status,
                    item.CreatedBy,
                    item.CreatedAt,
                    studentCounts.GetValueOrDefault(item.Id),
                    [
                        .. classTeachers.Select(teacher => new TeacherDto(
                            teacher.TeacherId,
                            names.TryGetValue(teacher.TeacherId, out var user) ? user.DisplayName : string.Empty,
                            teacher.IsPrimary)),
                    ],
                    myRole);
            }),
        ];
    }

    public async Task<ClassDto> BuildAsync(StudyClass item, Guid callerId, CancellationToken cancellationToken) =>
        (await BuildAsync([item], callerId, cancellationToken))[0];
}
