using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Messaging;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.Modules.Identity.Infrastructure.Keycloak;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Users;

/// <summary>
/// POST /api/identity/users/{id}/deactivate and /activate — BR-AKUN-05, admin only.
/// Deactivation: Keycloak disabled + all sessions ended + is_active = false; the status cache is cleared so the API
/// answers 403 account_inactive at once. Keycloak is updated inside the database transaction, so a Keycloak failure
/// leaves both sides unchanged.
/// </summary>
internal sealed class SetAccountActiveHandler(
    IdentityDbContext db,
    IKeycloakAdmin keycloak,
    AccountStatusCache statusCache,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        if (profile is null)
        {
            return AppError.NotFound();
        }

        var otherActiveAdmins = await db.UserProfiles.CountAsync(
            other => other.Role == UserRole.Admin && other.IsActive && other.Id != id,
            cancellationToken);
        if (AccountRules.CheckDeactivation(currentUser.Id, profile, otherActiveAdmins) is { } refused)
        {
            return refused;
        }

        profile.Deactivate();
        return await ApplyAsync(profile, enabled: false, cancellationToken);
    }

    public async Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        if (profile is null)
        {
            return AppError.NotFound();
        }

        profile.Activate();
        return await ApplyAsync(profile, enabled: true, cancellationToken);
    }

    private async Task<Result> ApplyAsync(UserProfile profile, bool enabled, CancellationToken cancellationToken)
    {
        if (enabled)
        {
            db.AddIntegrationEvent(new UserReactivated(profile.Id), timeProvider);
        }
        else
        {
            db.AddIntegrationEvent(new UserDeactivated(profile.Id), timeProvider);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await keycloak.UpdateUserAsync(profile.Id, new KeycloakUserChanges(Enabled: enabled), cancellationToken);
            if (!enabled)
            {
                await keycloak.LogoutAsync(profile.Id, cancellationToken);
            }
        }
        catch (KeycloakAdminException exception)
        {
            return KeycloakErrors.ToAppError(exception);
        }

        await transaction.CommitAsync(cancellationToken);
        await statusCache.InvalidateAsync(profile.Id);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/users/{id:guid}/deactivate", async (Guid id, SetAccountActiveHandler handler, CancellationToken cancellationToken) =>
                (await handler.DeactivateAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("DeactivateUser")
            .WithSummary("Deactivate an account: no login, API answers 403 account_inactive (admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/users/{id:guid}/activate", async (Guid id, SetAccountActiveHandler handler, CancellationToken cancellationToken) =>
                (await handler.ActivateAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("ActivateUser")
            .WithSummary("Reactivate an account (admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
