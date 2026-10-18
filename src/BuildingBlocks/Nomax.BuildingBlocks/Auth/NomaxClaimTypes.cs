namespace Nomax.BuildingBlocks.Auth;

/// <summary>
/// Claim names as issued by Keycloak. Inbound claim mapping is disabled, so they keep their JWT names.
/// </summary>
public static class NomaxClaimTypes
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string RealmAccess = "realm_access";
    public const string PreferredUsername = "preferred_username";
}
