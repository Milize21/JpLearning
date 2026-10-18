using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.MyProfile;

/// <summary>GET /api/identity/me — own profile and preferences (docs/05 §4). Every role.</summary>
internal sealed class GetMeHandler(IdentityDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<UserProfileDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == currentUser.Id, cancellationToken);
        return profile is null ? AppError.NotFound() : UserProfileDto.From(profile);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/me", async (GetMeHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken)).ToOkResult())
            .WithName("GetMe")
            .WithSummary("Own profile and preferences")
            .Produces<UserProfileDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
