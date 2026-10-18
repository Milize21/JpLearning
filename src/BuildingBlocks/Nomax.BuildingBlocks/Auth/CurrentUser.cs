using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Nomax.BuildingBlocks.Auth;

/// <summary>
/// The caller of the current request.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The Keycloak <c>sub</c>, which is also identity.user_profiles.id. Throws for anonymous requests.</summary>
    Guid Id { get; }

    bool IsInRole(string role);
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? FindUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return Guid.TryParse(principal.FindFirst(NomaxClaimTypes.Subject)?.Value, out var id) ? id : null;
    }
}

internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true && User.FindUserId() is not null;

    public Guid Id => User.FindUserId()
        ?? throw new InvalidOperationException("The current request has no authenticated user.");

    public bool IsInRole(string role) => User.IsInRole(role);
}
