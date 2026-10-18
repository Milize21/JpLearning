using Nomax.Modules.Identity.Domain;

namespace Nomax.Modules.Identity.Features.Common;

/// <summary>
/// A profile as returned by <c>/me</c> and the admin user endpoints. Email is included because only the owner and
/// admins can reach these endpoints (docs/01 §4).
/// </summary>
public sealed record UserProfileDto(
    Guid Id,
    string Username,
    string? Email,
    string DisplayName,
    UserRole Role,
    UiLocale UiLocale,
    ScriptPref ScriptPref,
    string Timezone,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    internal static UserProfileDto From(UserProfile profile) => new(
        profile.Id,
        profile.Username,
        profile.Email,
        profile.DisplayName,
        profile.Role,
        profile.UiLocale,
        profile.ScriptPref,
        profile.Timezone,
        profile.IsActive,
        profile.CreatedAt);
}
