using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.MyProfile;

/// <summary>Only the fields that are present change.</summary>
public sealed record UpdatePreferencesRequest(UiLocale? UiLocale = null, ScriptPref? ScriptPref = null, string? Timezone = null);

internal sealed class UpdatePreferencesValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesValidator()
    {
        RuleFor(request => request.UiLocale).IsInEnum();
        RuleFor(request => request.ScriptPref).IsInEnum();
        RuleFor(request => request.Timezone)
            .Must(AccountRules.IsValidTimezone)
            .When(request => request.Timezone is not null)
            .WithMessage("Must be an IANA time zone such as Asia/Jakarta.");
    }
}

/// <summary>PATCH /api/identity/me/preferences — BR-AKUN-07. Every role.</summary>
internal sealed class UpdatePreferencesHandler(IdentityDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<UserProfileDto>> HandleAsync(UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(profile => profile.Id == currentUser.Id, cancellationToken);
        if (profile is null)
        {
            return AppError.NotFound();
        }

        profile.UpdatePreferences(request.UiLocale, request.ScriptPref, request.Timezone);
        await db.SaveChangesAsync(cancellationToken);
        return UserProfileDto.From(profile);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/me/preferences", async (
                UpdatePreferencesRequest request,
                UpdatePreferencesHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToOkResult())
            .WithValidation<UpdatePreferencesRequest>()
            .WithName("UpdateMyPreferences")
            .WithSummary("Change own UI language, Japanese display mode or time zone")
            .Produces<UserProfileDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
