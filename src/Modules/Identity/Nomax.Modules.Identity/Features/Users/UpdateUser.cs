using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Messaging;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.Modules.Identity.Infrastructure.Keycloak;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Users;

/// <summary>Only the fields that are present change. <see cref="Email"/> = "" removes the email.</summary>
public sealed record UpdateUserRequest(string? DisplayName = null, string? Email = null, string? Timezone = null);

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(request => request.DisplayName)
            .Must(AccountRules.IsValidDisplayName)
            .When(request => request.DisplayName is not null)
            .WithMessage("Must be 1–80 characters.");
        RuleFor(request => request.Email)
            .Must(AccountRules.IsValidEmail)
            .When(request => !string.IsNullOrEmpty(request.Email))
            .WithMessage("Must be a valid email address.");
        RuleFor(request => request.Timezone)
            .Must(AccountRules.IsValidTimezone)
            .When(request => request.Timezone is not null)
            .WithMessage("Must be an IANA time zone such as Asia/Jakarta.");
    }
}

/// <summary>
/// PATCH /api/identity/users/{id} — display name (BR-AKUN-06, D-21), email and time zone, admin only.
/// Keycloak gets the same email and display name before the profile change is committed.
/// </summary>
internal sealed class UpdateUserHandler(
    IdentityDbContext db,
    AccountProvisioner provisioner,
    IKeycloakAdmin keycloak,
    TimeProvider timeProvider)
{
    public async Task<Result<UserProfileDto>> HandleAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        if (profile is null)
        {
            return AppError.NotFound();
        }

        if (!string.IsNullOrEmpty(request.Email) && await provisioner.IsEmailTakenAsync(request.Email, id, cancellationToken))
        {
            return AppError.Conflict("The email is already used by another account.");
        }

        var emailChanged = request.Email is not null && request.Email != (profile.Email ?? string.Empty);
        var nameChanged = request.DisplayName is not null && AccountRules.NormalizeDisplayName(request.DisplayName) != profile.DisplayName;

        if (request.DisplayName is not null)
        {
            profile.ChangeDisplayName(request.DisplayName);
        }

        if (request.Email is not null)
        {
            profile.ChangeEmail(request.Email);
        }

        if (request.Timezone is not null)
        {
            profile.ChangeTimezone(request.Timezone);
        }

        if (nameChanged)
        {
            db.AddIntegrationEvent(new UserDisplayNameChanged(profile.Id, profile.DisplayName), timeProvider);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (emailChanged || nameChanged)
        {
            try
            {
                await keycloak.UpdateUserAsync(
                    id,
                    new KeycloakUserChanges(
                        Email: emailChanged ? profile.Email ?? string.Empty : null,
                        DisplayName: nameChanged ? profile.DisplayName : null),
                    cancellationToken);
            }
            catch (KeycloakAdminException exception)
            {
                return KeycloakErrors.ToAppError(exception);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return UserProfileDto.From(profile);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/users/{id:guid}", async (
                Guid id,
                UpdateUserRequest request,
                UpdateUserHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<UpdateUserRequest>()
            .WithName("UpdateUser")
            .WithSummary("Change display name, email or time zone of an account (admin)")
            .Produces<UserProfileDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
