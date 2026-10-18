using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Identity.Domain;

namespace Nomax.Modules.Identity.Infrastructure;

/// <summary>
/// Profiles for the development users of infra/keycloak/realm-nomax.json, with the same ids (docs/03 §13). Idempotent.
/// </summary>
internal sealed class IdentityDevelopmentSeeder(IdentityDbContext db) : IDevelopmentSeeder
{
    internal static readonly IReadOnlyList<(Guid Id, string Username, string DisplayName, UserRole Role)> DevUsers =
    [
        (Guid.Parse("a0000000-0000-4000-8000-000000000001"), "admin.dev", "Admin Dev", UserRole.Admin),
        (Guid.Parse("a0000000-0000-4000-8000-000000000002"), "sensei.dev", "Sensei Dev", UserRole.Sensei),
        (Guid.Parse("a0000000-0000-4000-8000-000000000003"), "murid1.dev", "Murid Satu", UserRole.Murid),
        (Guid.Parse("a0000000-0000-4000-8000-000000000004"), "murid2.dev", "Murid Dua", UserRole.Murid),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var ids = DevUsers.Select(user => user.Id).ToList();
        var existing = await db.UserProfiles.Where(profile => ids.Contains(profile.Id)).Select(profile => profile.Id).ToListAsync(cancellationToken);

        foreach (var user in DevUsers.Where(user => !existing.Contains(user.Id)))
        {
            db.UserProfiles.Add(UserProfile.Create(
                user.Id,
                user.Username,
                $"{user.Username}@nomax.test",
                user.DisplayName,
                user.Role,
                AccountRules.DefaultTimezone,
                UiLocale.Id,
                createdBy: null));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
