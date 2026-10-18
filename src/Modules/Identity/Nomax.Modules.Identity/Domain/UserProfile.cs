using Nomax.BuildingBlocks.Persistence;

namespace Nomax.Modules.Identity.Domain;

/// <summary>
/// identity.user_profiles (docs/03 §4.1). <see cref="Id"/> equals the Keycloak <c>sub</c>; the password lives only in Keycloak.
/// </summary>
public sealed class UserProfile : IHasTimestamps
{
    private UserProfile()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Copy of the Keycloak username.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>Copy of the Keycloak email; optional, used for reminders.</summary>
    public string? Email { get; private set; }

    /// <summary>Shown on the leaderboard; changed by admins only (D-21).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Copy of the realm role; the token stays the source of authorization (docs/01 §9).</summary>
    public UserRole Role { get; private set; }

    public UiLocale UiLocale { get; private set; }

    public ScriptPref ScriptPref { get; private set; }

    public string Timezone { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    /// <summary>Admin who created the account; null for seeded accounts.</summary>
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserProfile Create(
        Guid id,
        string username,
        string? email,
        string displayName,
        UserRole role,
        string timezone,
        UiLocale uiLocale,
        Guid? createdBy) => new()
        {
            Id = id,
            Username = username,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            DisplayName = AccountRules.NormalizeDisplayName(displayName),
            Role = role,
            UiLocale = uiLocale,
            ScriptPref = ScriptPref.KanjiFurigana,
            Timezone = timezone,
            IsActive = true,
            CreatedBy = createdBy,
        };

    /// <summary>BR-AKUN-07: every user changes their own preferences at any time.</summary>
    public void UpdatePreferences(UiLocale? uiLocale, ScriptPref? scriptPref, string? timezone)
    {
        UiLocale = uiLocale ?? UiLocale;
        ScriptPref = scriptPref ?? ScriptPref;
        Timezone = timezone ?? Timezone;
    }

    public void ChangeDisplayName(string displayName) => DisplayName = AccountRules.NormalizeDisplayName(displayName);

    public void ChangeEmail(string? email) => Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    public void ChangeTimezone(string timezone) => Timezone = timezone;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
