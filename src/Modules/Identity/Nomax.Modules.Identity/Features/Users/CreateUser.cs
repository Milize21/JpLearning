using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Features.Common;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Users;

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    UserRole Role,
    string? Email = null,
    string? Timezone = null,
    UiLocale? UiLocale = null);

/// <summary>The temporary password appears here once and is never stored (BR-AKUN-02 step 5).</summary>
public sealed record CreateUserResponse(UserProfileDto User, string TemporaryPassword);

internal sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(request => request.Username)
            .Must(AccountRules.IsValidUsername)
            .WithMessage("Must be 3–64 characters of a-z, 0-9, dot, underscore or hyphen.");
        RuleFor(request => request.Email)
            .Must(AccountRules.IsValidEmail)
            .When(request => !string.IsNullOrEmpty(request.Email))
            .WithMessage("Must be a valid email address.");
        RuleFor(request => request.DisplayName)
            .Must(AccountRules.IsValidDisplayName)
            .WithMessage("Must be 1–80 characters.");
        RuleFor(request => request.Role).IsInEnum();
        RuleFor(request => request.Timezone)
            .Must(AccountRules.IsValidTimezone)
            .When(request => request.Timezone is not null)
            .WithMessage("Must be an IANA time zone such as Asia/Jakarta.");
        RuleFor(request => request.UiLocale).IsInEnum();
    }
}

/// <summary>POST /api/identity/users — BR-AKUN-01/02, admin only.</summary>
internal sealed class CreateUserHandler(AccountProvisioner provisioner, ICurrentUser currentUser)
{
    public async Task<Result<CreateUserResponse>> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = string.IsNullOrEmpty(request.Email) ? null : request.Email;
        if (await provisioner.CheckAvailableAsync(request.Username, email, cancellationToken) is { } taken)
        {
            return taken;
        }

        var created = await provisioner.CreateAsync(
            new NewAccount(
                request.Username,
                email,
                request.DisplayName,
                request.Role,
                request.Timezone ?? AccountRules.DefaultTimezone,
                request.UiLocale ?? UiLocale.Id),
            currentUser.Id,
            cancellationToken);

        return created.IsSuccess
            ? new CreateUserResponse(UserProfileDto.From(created.Value.Profile), created.Value.TemporaryPassword)
            : created.Error;
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/users", async (CreateUserRequest request, CreateUserHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken))
                    .ToHttpResult(response => TypedResults.Created($"/api/identity/users/{response.User.Id}", response)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<CreateUserRequest>()
            .WithName("CreateUser")
            .WithSummary("Create an account with a one-time temporary password (admin)")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
