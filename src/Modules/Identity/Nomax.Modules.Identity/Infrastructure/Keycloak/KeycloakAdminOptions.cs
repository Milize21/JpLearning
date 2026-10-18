using System.ComponentModel.DataAnnotations;

namespace Nomax.Modules.Identity.Infrastructure.Keycloak;

/// <summary>
/// Admin API settings from the <c>Keycloak</c> section (docs/02 §14). The secret comes from user-secrets or the environment.
/// </summary>
internal sealed class KeycloakAdminOptions
{
    /// <summary>Realm URL, e.g. <c>http://localhost:8081/realms/nomax</c>.</summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string AdminClientId { get; set; } = "nomax-api";

    [Required]
    public string AdminClientSecret { get; set; } = string.Empty;

    public Uri TokenEndpoint => new($"{Authority.TrimEnd('/')}/protocol/openid-connect/token");

    /// <summary><c>…/realms/nomax</c> → <c>…/admin/realms/nomax/</c>.</summary>
    public Uri AdminBaseAddress
    {
        get
        {
            var authority = Authority.TrimEnd('/');
            var realmIndex = authority.LastIndexOf("/realms/", StringComparison.Ordinal);
            if (realmIndex < 0)
            {
                throw new InvalidOperationException("Keycloak:Authority must end with /realms/{realm}.");
            }

            return new Uri($"{authority[..realmIndex]}/admin{authority[realmIndex..]}/");
        }
    }
}
