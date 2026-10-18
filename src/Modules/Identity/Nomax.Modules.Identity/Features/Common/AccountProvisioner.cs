using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nomax.BuildingBlocks.Messaging;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.Modules.Identity.Infrastructure.Keycloak;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Common;

internal sealed record NewAccount(
    string Username,
    string? Email,
    string DisplayName,
    UserRole Role,
    string Timezone,
    UiLocale UiLocale);

internal sealed record ProvisionedAccount(UserProfile Profile, string TemporaryPassword);

/// <summary>
/// BR-AKUN-02, shared by single creation and CSV import: Keycloak user + temporary password + UPDATE_PASSWORD + realm role,
/// then the profile with the Keycloak id and the UserAccountCreated event (step 6) in one transaction.
/// If anything after the Keycloak user fails, that user is deleted again.
/// The temporary password is returned once and never stored.
/// </summary>
internal sealed partial class AccountProvisioner(
    IdentityDbContext db,
    IKeycloakAdmin keycloak,
    TimeProvider timeProvider,
    ILogger<AccountProvisioner> logger)
{
    public async Task<AppError?> CheckAvailableAsync(string username, string? email, CancellationToken cancellationToken)
    {
        if (await db.UserProfiles.AnyAsync(profile => profile.Username == username, cancellationToken))
        {
            return AppError.Conflict("The username is already taken.");
        }

        if (email is not null && await IsEmailTakenAsync(email, exceptUserId: null, cancellationToken))
        {
            return AppError.Conflict("The email is already used by another account.");
        }

        return null;
    }

    /// <summary>Emails are unique case-insensitively (unique index on lower(email), docs/03 §4.1).</summary>
    public Task<bool> IsEmailTakenAsync(string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        var pattern = SqlLike.Escape(email);
        return db.UserProfiles.AnyAsync(
            profile => profile.Email != null && EF.Functions.ILike(profile.Email, pattern, SqlLike.EscapeCharacter) && profile.Id != exceptUserId,
            cancellationToken);
    }

    public async Task<Result<ProvisionedAccount>> CreateAsync(NewAccount account, Guid createdBy, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = await keycloak.CreateUserAsync(new NewKeycloakUser(account.Username, account.Email, account.DisplayName), cancellationToken);
        }
        catch (KeycloakAdminException exception)
        {
            return KeycloakErrors.ToAppError(exception);
        }

        var password = TemporaryPassword.Generate();
        try
        {
            await keycloak.SetTemporaryPasswordAsync(userId, password, cancellationToken);
            await keycloak.AssignRealmRoleAsync(userId, account.Role.ToRealmRole(), cancellationToken);

            var profile = UserProfile.Create(
                userId, account.Username, account.Email, account.DisplayName, account.Role, account.Timezone, account.UiLocale, createdBy);
            db.UserProfiles.Add(profile);
            db.AddIntegrationEvent(
                new UserAccountCreated(profile.Id, account.Role.ToRealmRole(), profile.DisplayName, createdBy),
                timeProvider);
            await db.SaveChangesAsync(cancellationToken);
            return new ProvisionedAccount(profile, password);
        }
        catch (Exception exception) when (exception is KeycloakAdminException or DbUpdateException)
        {
            LogCompensating(logger, exception);
            db.ChangeTracker.Clear();
            await keycloak.DeleteUserAsync(userId, CancellationToken.None);
            return exception is KeycloakAdminException keycloakError
                ? KeycloakErrors.ToAppError(keycloakError)
                : AppError.Conflict("The username or email is already taken.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account creation failed after the Keycloak user was created; deleting it again")]
    private static partial void LogCompensating(ILogger logger, Exception exception);
}

internal static class KeycloakErrors
{
    public static AppError ToAppError(KeycloakAdminException exception) => exception.Failure switch
    {
        KeycloakFailure.Conflict => AppError.Conflict("The username or email already exists in Keycloak."),
        KeycloakFailure.NotFound => AppError.NotFound("The account does not exist in Keycloak."),
        KeycloakFailure.Rejected => AppError.Validation(new Dictionary<string, string[]>
        {
            ["keycloak"] = ["Keycloak rejected the data."],
        }),
        _ => new AppError(ErrorCodes.KeycloakUnavailable, "Keycloak is unavailable. Try again later."),
    };
}
