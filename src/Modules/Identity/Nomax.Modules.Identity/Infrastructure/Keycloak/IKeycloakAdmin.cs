namespace Nomax.Modules.Identity.Infrastructure.Keycloak;

/// <summary>
/// The Keycloak Admin API operations the Identity module needs (docs/04 §1). Behind an interface so integration tests
/// never call Keycloak (docs/09 §1).
/// </summary>
internal interface IKeycloakAdmin
{
    /// <summary>Creates an enabled user with the UPDATE_PASSWORD required action and returns its id (the future <c>sub</c>).</summary>
    Task<Guid> CreateUserAsync(NewKeycloakUser user, CancellationToken cancellationToken);

    /// <summary>Sets a temporary password; Keycloak then forces UPDATE_PASSWORD at the next login.</summary>
    Task SetTemporaryPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);

    Task AssignRealmRoleAsync(Guid userId, string realmRole, CancellationToken cancellationToken);

    Task UpdateUserAsync(Guid userId, KeycloakUserChanges changes, CancellationToken cancellationToken);

    /// <summary>Ends every session of the user.</summary>
    Task LogoutAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Compensation when account creation fails half way (BR-AKUN-02 step 4). Missing users are ignored.</summary>
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed record NewKeycloakUser(string Username, string? Email, string DisplayName);

/// <summary>Only the non-null members are changed. <see cref="Email"/> = "" removes the email.</summary>
internal sealed record KeycloakUserChanges(string? Email = null, string? DisplayName = null, bool? Enabled = null);

internal enum KeycloakFailure
{
    /// <summary>Network error, timeout, 5xx, or the service account is not allowed (misconfiguration).</summary>
    Unavailable,

    /// <summary>Username or email already exists in Keycloak.</summary>
    Conflict,

    NotFound,

    /// <summary>Keycloak refused the data, e.g. its password policy or email validation.</summary>
    Rejected,
}

internal sealed class KeycloakAdminException : Exception
{
    public KeycloakAdminException()
        : this(KeycloakFailure.Unavailable, "Keycloak admin call failed.")
    {
    }

    public KeycloakAdminException(string message)
        : this(KeycloakFailure.Unavailable, message)
    {
    }

    public KeycloakAdminException(string message, Exception innerException)
        : this(KeycloakFailure.Unavailable, message, innerException)
    {
    }

    public KeycloakAdminException(KeycloakFailure failure, string message, Exception? innerException = null)
        : base(message, innerException) => Failure = failure;

    public KeycloakFailure Failure { get; }
}
