using Nomax.BuildingBlocks.Auth;
using Nomax.Modules.Classroom.Contracts;
using Nomax.SharedKernel;

namespace Nomax.Modules.Practice.Features.Common;

/// <summary>
/// Layer 2 of the authorization for practice data (docs/01 §4–5): students see their own data, teachers the students of
/// classes they teach (<c>SenseiDari</c>), admins everything. Anything else is answered as 404.
/// </summary>
internal sealed class PracticeAccess(ICurrentUser currentUser, IClassAccessReader classes)
{
    public Guid UserId => currentUser.Id;

    public bool IsAdmin => currentUser.IsInRole(Roles.Admin);

    public bool IsStudent => currentUser.IsInRole(Roles.Murid);

    public bool IsStaff => currentUser.IsInRole(Roles.Admin) || currentUser.IsInRole(Roles.Sensei);

    /// <summary>The caller may see this student's practice data.</summary>
    public async Task<bool> CanSeeStudentAsync(Guid studentId, CancellationToken cancellationToken) =>
        IsAdmin
        || (IsStudent && studentId == UserId)
        || (!IsStudent && await classes.CanTeacherSeeStudentAsync(UserId, studentId, cancellationToken));

    /// <summary>The caller teaches the class or is an admin.</summary>
    public async Task<bool> CanTeachClassAsync(Guid classId, CancellationToken cancellationToken) =>
        IsAdmin || (!IsStudent && await classes.IsTeacherOfAsync(UserId, classId, cancellationToken));
}
