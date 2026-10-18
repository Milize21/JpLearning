using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.Modules.Identity.Infrastructure.Keycloak;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Users;

/// <summary>Shown once, never stored.</summary>
public sealed record ResetPasswordResponse(string TemporaryPassword);

/// <summary>
/// POST /api/identity/users/{id}/reset-password — BR-AKUN-04: new temporary password, UPDATE_PASSWORD at next login,
/// all sessions ended. Admin only.
/// </summary>
internal sealed class ResetPasswordHandler(IdentityDbContext db, IKeycloakAdmin keycloak)
{
    public async Task<Result<ResetPasswordResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.UserProfiles.AnyAsync(profile => profile.Id == id, cancellationToken))
        {
            return AppError.NotFound();
        }

        var password = TemporaryPassword.Generate();
        try
        {
            await keycloak.SetTemporaryPasswordAsync(id, password, cancellationToken);
            await keycloak.LogoutAsync(id, cancellationToken);
        }
        catch (KeycloakAdminException exception)
        {
            return KeycloakErrors.ToAppError(exception);
        }

        return new ResetPasswordResponse(password);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/users/{id:guid}/reset-password", async (Guid id, ResetPasswordHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("ResetUserPassword")
            .WithSummary("Issue a new one-time temporary password and end all sessions (admin)")
            .Produces<ResetPasswordResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
