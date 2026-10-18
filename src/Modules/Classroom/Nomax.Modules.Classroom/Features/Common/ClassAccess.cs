using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Common;

/// <summary>
/// Layer 2 of the authorization for class resources (docs/01 §5): admins pass every ownership check, teachers need
/// <c>Mengajar(S, K)</c>, students an active membership. Anything else is answered as 404 so classes cannot be enumerated.
/// </summary>
internal sealed class ClassAccess(ClassroomDbContext db, ICurrentUser currentUser)
{
    public bool IsAdmin => currentUser.IsInRole(Roles.Admin);

    public bool IsStudent => currentUser.IsInRole(Roles.Murid);

    public Guid UserId => currentUser.Id;

    /// <summary>The class when the caller teaches it or is an admin.</summary>
    public async Task<Result<StudyClass>> ForTeacherAsync(Guid classId, CancellationToken cancellationToken)
    {
        var item = await db.Classes.FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (item is null || (!IsAdmin && !await IsTeacherAsync(classId, cancellationToken)))
        {
            return AppError.NotFound();
        }

        return item;
    }

    /// <summary>The class for its teachers, admins and active students. Students do not see draft classes.</summary>
    public async Task<Result<StudyClass>> ForMemberAsync(Guid classId, CancellationToken cancellationToken)
    {
        var item = await db.Classes.FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (item is null)
        {
            return AppError.NotFound();
        }

        if (IsAdmin || (!IsStudent && await IsTeacherAsync(classId, cancellationToken)))
        {
            return item;
        }

        var isMember = IsStudent && item.Status != ClassStatus.Draft && await db.ClassStudents.AnyAsync(
            member => member.ClassId == classId && member.StudentId == currentUser.Id && member.Status == MemberStatus.Active,
            cancellationToken);
        return isMember ? item : AppError.NotFound();
    }

    public Task<bool> IsTeacherAsync(Guid classId, CancellationToken cancellationToken) =>
        db.ClassTeachers.AnyAsync(teacher => teacher.ClassId == classId && teacher.TeacherId == currentUser.Id, cancellationToken);

    public Task<bool> IsPrimaryTeacherAsync(Guid classId, CancellationToken cancellationToken) =>
        db.ClassTeachers.AnyAsync(teacher => teacher.ClassId == classId && teacher.TeacherId == currentUser.Id && teacher.IsPrimary, cancellationToken);

    /// <summary>Archived classes are read only (BR-AKSES-01).</summary>
    public static AppError? EnsureWritable(StudyClass item) =>
        item.Status == ClassStatus.Archived ? AppError.InvalidState("The class is archived and read only.") : null;
}
