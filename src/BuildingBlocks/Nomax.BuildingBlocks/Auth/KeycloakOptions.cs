using System.ComponentModel.DataAnnotations;

namespace Nomax.BuildingBlocks.Auth;

/// <summary>
/// Configuration section <c>Keycloak</c> (docs/02 §14). The Keycloak Admin API settings are added with the Identity module.
/// </summary>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>Realm URL, e.g. <c>http://localhost:8081/realms/nomax</c>. Must match the token issuer exactly (D-25).</summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = "nomax-api";

    public bool RequireHttpsMetadata { get; set; } = true;
}
