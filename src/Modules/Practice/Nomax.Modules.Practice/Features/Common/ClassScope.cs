using Nomax.Modules.Classroom.Contracts;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Common;

/// <summary>
/// Class checks for quizzes, assignments and grades (docs/01 §4): teachers of the class and admins manage them, active
/// students of a non-draft class see what is published. Anyone else gets 404.
/// </summary>
internal sealed class ClassScope(PracticeAccess access, IClassAccessReader classes)
{
    public async Task<Result<ClassInfo>> ForTeacherAsync(Guid classId, CancellationToken cancellationToken)
    {
        var info = await classes.GetClassInfoAsync(classId, cancellationToken);
        return info is not null && await access.CanTeachClassAsync(classId, cancellationToken) ? info : AppError.NotFound();
    }

    public async Task<Result<ClassInfo>> ForMemberAsync(Guid classId, CancellationToken cancellationToken)
    {
        var info = await classes.GetClassInfoAsync(classId, cancellationToken);
        if (info is null)
        {
            return AppError.NotFound();
        }

        if (access.IsStudent)
        {
            return info.Status != ClassStatus.Draft && await classes.IsStudentOfAsync(access.UserId, classId, cancellationToken)
                ? info
                : AppError.NotFound();
        }

        return await access.CanTeachClassAsync(classId, cancellationToken) ? info : AppError.NotFound();
    }

    /// <summary>Archived classes are read only (BR-AKSES-01).</summary>
    public static AppError? EnsureWritable(ClassInfo info) =>
        info.Status == ClassStatus.Archived ? AppError.InvalidState("The class is archived and read only.") : null;
}
