using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Nomax.SharedKernel;

namespace Nomax.BuildingBlocks.Auth;

/// <summary>
/// Layer 1 of the two-layer authorization (docs/01 §5): role policies on endpoints.
/// Layer 2, resource ownership, is checked in each handler.
/// </summary>
public static class Policies
{
    /// <summary>Any signed-in account with an application role. The active-profile check runs in <c>ActiveProfileMiddleware</c>.</summary>
    public const string Authenticated = nameof(Authenticated);

    public const string MuridOnly = nameof(MuridOnly);

    public const string StaffOnly = nameof(StaffOnly);

    public const string AdminOnly = nameof(AdminOnly);

    public static IServiceCollection AddNomaxAuthorization(this IServiceCollection services)
    {
        var authenticated = RequireAnyRole(Roles.All);

        services.AddAuthorizationBuilder()
            .AddPolicy(Authenticated, authenticated)
            .AddPolicy(MuridOnly, RequireAnyRole([Roles.Murid]))
            .AddPolicy(StaffOnly, RequireAnyRole([Roles.Admin, Roles.Sensei]))
            .AddPolicy(AdminOnly, RequireAnyRole([Roles.Admin]))
            .SetDefaultPolicy(authenticated)
            // Secure by default: an endpoint that forgets RequireAuthorization still needs a signed-in user.
            .SetFallbackPolicy(authenticated);

        return services;
    }

    private static AuthorizationPolicy RequireAnyRole(IEnumerable<string> roles) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole(roles)
            .Build();
}
