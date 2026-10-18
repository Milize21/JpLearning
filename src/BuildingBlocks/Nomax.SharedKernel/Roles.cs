namespace Nomax.SharedKernel;

/// <summary>
/// Keycloak realm roles (docs/01 §1). One account has exactly one role.
/// </summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string Sensei = "sensei";
    public const string Murid = "murid";

    public static IReadOnlyList<string> All { get; } = [Admin, Sensei, Murid];

    public static bool IsKnown(string? role) => role is Admin or Sensei or Murid;
}
