using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.SharedKernel.Paging;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Users;

/// <summary>Query string of GET /users. <see cref="Role"/> is admin/sensei/murid; <see cref="Q"/> searches username, display name and email.</summary>
public sealed record ListUsersQuery(
    [property: FromQuery(Name = "role")] string? Role,
    [property: FromQuery(Name = "q")] string? Q,
    [property: FromQuery(Name = "isActive")] bool? IsActive,
    [property: FromQuery(Name = "page")] int? Page,
    [property: FromQuery(Name = "pageSize")] int? PageSize);

/// <summary>GET /api/identity/users — admin only, filtered and paged in SQL (docs/05 §4).</summary>
internal sealed class ListUsersHandler(IdentityDbContext db)
{
    public async Task<Result<PagedResult<UserProfileDto>>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var roleFilter = QueryEnums.Parse<UserRole>(query.Role, "role");
        if (!roleFilter.IsSuccess)
        {
            return roleFilter.Error;
        }

        var page = PageRequest.Create(query.Page, query.PageSize);
        var profiles = db.UserProfiles.AsNoTracking();

        if (roleFilter.Value is { } role)
        {
            profiles = profiles.Where(profile => profile.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            profiles = profiles.Where(profile => profile.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = SqlLike.Contains(query.Q.Trim());
            profiles = profiles.Where(profile =>
                EF.Functions.ILike(profile.Username, pattern, SqlLike.EscapeCharacter)
                || EF.Functions.ILike(profile.DisplayName, pattern, SqlLike.EscapeCharacter)
                || (profile.Email != null && EF.Functions.ILike(profile.Email, pattern, SqlLike.EscapeCharacter)));
        }

        var total = await profiles.CountAsync(cancellationToken);
        var items = await profiles
            .OrderBy(profile => profile.DisplayName)
            .ThenBy(profile => profile.Username)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserProfileDto>([.. items.Select(UserProfileDto.From)], page.Page, page.PageSize, total);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/users", async ([AsParameters] ListUsersQuery query, ListUsersHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("ListUsers")
            .WithSummary("List accounts (admin)")
            .Produces<PagedResult<UserProfileDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
