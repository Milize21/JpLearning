using Nomax.BuildingBlocks.Auth;
using Nomax.Modules.Classroom.Contracts;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Common;

/// <summary>
/// Layer 2 of the authorization for students (docs/01 §5): chapter access through <see cref="IClassAccessReader"/>
/// (BR-AKSES-01). Staff see every chapter.
/// </summary>
internal sealed class ChapterGate(ICurrentUser currentUser, IClassAccessReader classAccess)
{
    private IReadOnlySet<Guid>? _accessible;

    public bool IsStudent => currentUser.IsInRole(Roles.Murid);

    /// <summary>403 <c>chapter_locked</c> when a student has no class that unlocked the chapter.</summary>
    public async Task<AppError?> CheckAsync(Guid chapterId, CancellationToken cancellationToken)
    {
        if (!IsStudent)
        {
            return null;
        }

        return await classAccess.IsChapterAccessibleAsync(currentUser.Id, chapterId, cancellationToken)
            ? null
            : ContentErrors.ChapterLocked();
    }

    /// <summary>Chapters a student may open; null for staff (everything).</summary>
    public async Task<IReadOnlySet<Guid>?> AccessibleChaptersAsync(CancellationToken cancellationToken)
    {
        if (!IsStudent)
        {
            return null;
        }

        return _accessible ??= await classAccess.GetAccessibleChapterIdsAsync(currentUser.Id, cancellationToken);
    }
}

internal static class ContentErrors
{
    public static AppError ChapterLocked() =>
        new(ErrorCodes.ChapterLocked, "Chapter is not unlocked for any of your classes.");

    public static AppError HasDependents(string what) =>
        new(ErrorCodes.HasDependents, $"The {what} is still used by other data and cannot be deleted.");

    /// <summary>A referenced id in the request body does not exist: a validation error on that field.</summary>
    public static AppError UnknownReference(string field) => AppError.Validation(new Dictionary<string, string[]>
    {
        [field] = ["Does not exist."],
    });
}
