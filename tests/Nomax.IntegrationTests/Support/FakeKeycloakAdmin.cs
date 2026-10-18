using System.Collections.Concurrent;
using Nomax.Modules.Identity.Infrastructure.Keycloak;

namespace Nomax.IntegrationTests.Support;

/// <summary>
/// In-memory stand-in for the Keycloak Admin API. Failures are injected by username prefix so tests running in
/// parallel never affect each other.
/// </summary>
internal sealed class FakeKeycloakAdmin : IKeycloakAdmin
{
    /// <summary>Creating a user whose name starts with this fails as if Keycloak were down.</summary>
    public const string UnavailablePrefix = "kc-down";

    /// <summary>Role mapping for such a user fails after the user was created (tests the compensation).</summary>
    public const string RoleFailurePrefix = "kc-role-fail";

    public ConcurrentDictionary<Guid, FakeKeycloakUser> Users { get; } = new();

    public ConcurrentQueue<(string Operation, Guid UserId)> Calls { get; } = new();

    public Task<Guid> CreateUserAsync(NewKeycloakUser user, CancellationToken cancellationToken)
    {
        if (user.Username.StartsWith(UnavailablePrefix, StringComparison.Ordinal))
        {
            throw new KeycloakAdminException(KeycloakFailure.Unavailable, "Keycloak is down (fake).");
        }

        if (Users.Values.Any(existing => existing.Username == user.Username))
        {
            throw new KeycloakAdminException(KeycloakFailure.Conflict, "User exists (fake).");
        }

        var id = Guid.NewGuid();
        Users[id] = new FakeKeycloakUser(user.Username) { Email = user.Email, DisplayName = user.DisplayName };
        Calls.Enqueue(("create", id));
        return Task.FromResult(id);
    }

    public Task SetTemporaryPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var user = Get(userId);
        user.TemporaryPassword = password;
        Calls.Enqueue(("set-temporary-password", userId));
        return Task.CompletedTask;
    }

    public Task AssignRealmRoleAsync(Guid userId, string realmRole, CancellationToken cancellationToken)
    {
        var user = Get(userId);
        if (user.Username.StartsWith(RoleFailurePrefix, StringComparison.Ordinal))
        {
            throw new KeycloakAdminException(KeycloakFailure.Unavailable, "Role mapping failed (fake).");
        }

        user.Roles.Add(realmRole);
        Calls.Enqueue(("assign-role", userId));
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(Guid userId, KeycloakUserChanges changes, CancellationToken cancellationToken)
    {
        var user = Get(userId);
        if (changes.Email is not null)
        {
            user.Email = changes.Email.Length == 0 ? null : changes.Email;
        }

        user.DisplayName = changes.DisplayName ?? user.DisplayName;
        user.Enabled = changes.Enabled ?? user.Enabled;
        Calls.Enqueue(("update", userId));
        return Task.CompletedTask;
    }

    public Task LogoutAsync(Guid userId, CancellationToken cancellationToken)
    {
        Calls.Enqueue(("logout", userId));
        return Task.CompletedTask;
    }

    public Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        Users.TryRemove(userId, out _);
        Calls.Enqueue(("delete", userId));
        return Task.CompletedTask;
    }

    public bool Called(string operation, Guid userId) => Calls.Contains((operation, userId));

    // Seeded development users exist in the real realm but were never created through this fake.
    private FakeKeycloakUser Get(Guid userId) => Users.GetOrAdd(userId, _ => new FakeKeycloakUser("seeded"));
}

internal sealed class FakeKeycloakUser(string username)
{
    public string Username { get; } = username;

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public bool Enabled { get; set; } = true;

    public string? TemporaryPassword { get; set; }

    public ConcurrentBag<string> Roles { get; } = [];
}
