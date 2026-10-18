namespace Nomax.Modules.Identity.Domain;

/// <summary>Enum <c>role</c> (docs/03 §3.3); stored and sent as admin/sensei/murid.</summary>
public enum UserRole
{
    Admin,
    Sensei,
    Murid,
}

/// <summary>Enum <c>ui_locale</c> (docs/03 §3.3).</summary>
public enum UiLocale
{
    Id,
    En,
}

/// <summary>Enum <c>script_pref</c> (docs/03 §3.3, docs/06 §4).</summary>
public enum ScriptPref
{
    Off,
    KanjiFurigana,
    Kana,
    Romaji,
    KanjiFuriganaRomaji,
}

public static class UserRoleExtensions
{
    /// <summary>The Keycloak realm role name (docs/01 §1).</summary>
    public static string ToRealmRole(this UserRole role) => role switch
    {
        UserRole.Admin => SharedKernel.Roles.Admin,
        UserRole.Sensei => SharedKernel.Roles.Sensei,
        UserRole.Murid => SharedKernel.Roles.Murid,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
