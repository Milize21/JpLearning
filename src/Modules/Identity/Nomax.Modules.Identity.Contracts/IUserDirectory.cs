namespace Nomax.Modules.Identity.Contracts;

/// <summary>
/// Short profiles of users by id, for Classroom, Practice and Insight (docs/02 §6.2).
/// </summary>
public interface IUserDirectory
{
    /// <summary>Profiles of the given users; unknown ids are absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>
    /// Profiles by exact (lower-case) username; unknown usernames are absent. Lets a teacher add known students to a class
    /// without a user search that would expose other students (docs/01 §4).
    /// </summary>
    Task<IReadOnlyDictionary<string, UserSummary>> FindByUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken cancellationToken);
}

/// <param name="Role">One of <c>Nomax.SharedKernel.Roles</c>.</param>
/// <param name="UiLocale"><c>id</c> or <c>en</c>.</param>
/// <param name="Email">Only for notifications; never show it to other users (docs/01 §4).</param>
public sealed record UserSummary(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    bool IsActive,
    string Timezone,
    string UiLocale,
    string? Email);
