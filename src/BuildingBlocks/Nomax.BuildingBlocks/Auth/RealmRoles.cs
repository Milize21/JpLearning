using System.Security.Claims;
using System.Text.Json;
using Nomax.SharedKernel;

namespace Nomax.BuildingBlocks.Auth;

/// <summary>
/// Maps Keycloak's <c>realm_access.roles</c> to role claims (docs/02 §8). Only application roles are mapped;
/// Keycloak's own roles such as <c>offline_access</c> are ignored.
/// </summary>
public static class RealmRoles
{
    public static IReadOnlyList<string> Parse(string? realmAccessJson)
    {
        if (string.IsNullOrWhiteSpace(realmAccessJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(realmAccessJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("roles", out var roles)
                || roles.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return
            [
                .. roles.EnumerateArray()
                    .Where(role => role.ValueKind == JsonValueKind.String)
                    .Select(role => role.GetString())
                    .OfType<string>()
                    .Where(Roles.IsKnown)
                    .Distinct(StringComparer.Ordinal),
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void AddRoleClaims(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        foreach (var role in Parse(identity.FindFirst(NomaxClaimTypes.RealmAccess)?.Value))
        {
            if (!identity.HasClaim(identity.RoleClaimType, role))
            {
                identity.AddClaim(new Claim(identity.RoleClaimType, role));
            }
        }
    }
}
