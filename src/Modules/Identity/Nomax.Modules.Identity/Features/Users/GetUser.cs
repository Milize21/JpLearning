using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Users;

/// <summary>GET /api/identity/users/{id} — admin only.</summary>
internal sealed class GetUserHandler(IdentityDbContext db)
{
    public async Task<Result<UserProfileDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        return profile is null ? AppError.NotFound() : UserProfileDto.From(profile);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/users/{id:guid}", async (Guid id, GetUserHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("GetUser")
            .WithSummary("One account (admin)")
            .Produces<UserProfileDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
