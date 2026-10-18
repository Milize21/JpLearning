using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Identity.Public;

internal sealed class UserDirectory(IdentityDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserSummary>();
        }

        var ids = userIds.Distinct().ToList();
        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(profile => ids.Contains(profile.Id))
            .ToListAsync(cancellationToken);

        return profiles.ToDictionary(profile => profile.Id, ToSummary);
    }

    public async Task<IReadOnlyDictionary<string, UserSummary>> FindByUsernamesAsync(
        IReadOnlyCollection<string> usernames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usernames);
        var names = usernames.Select(name => name.Trim().ToLowerInvariant()).Where(name => name.Length > 0).Distinct().ToList();
        if (names.Count == 0)
        {
            return new Dictionary<string, UserSummary>();
        }

        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(profile => names.Contains(profile.Username))
            .ToListAsync(cancellationToken);

        return profiles.ToDictionary(profile => profile.Username, ToSummary, StringComparer.Ordinal);
    }

    private static UserSummary ToSummary(UserProfile profile) => new(
        profile.Id,
        profile.Username,
        profile.DisplayName,
        EnumSnakeCase.ToValue(profile.Role),
        profile.IsActive,
        profile.Timezone,
        EnumSnakeCase.ToValue(profile.UiLocale),
        profile.Email);
}
