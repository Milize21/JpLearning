using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Domain;

/// <summary>
/// Account rules from docs/04 §1 (BR-AKUN-02, -05, -06, -07) as pure functions.
/// </summary>
public static partial class AccountRules
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 64;
    public const int DisplayNameMaxLength = 80;
    public const int EmailMaxLength = 254;
    public const int TimezoneMaxLength = 64;
    public const string DefaultTimezone = "Asia/Jakarta";

    /// <summary>BR-AKUN-02: 3–64 characters from <c>[a-z0-9._-]</c>.</summary>
    public static bool IsValidUsername(string? username) => username is not null && UsernamePattern().IsMatch(username);

    /// <summary>BR-AKUN-02/06: trimmed, 1–80 characters; does not need to be unique.</summary>
    public static bool IsValidDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= DisplayNameMaxLength;
    }

    public static string NormalizeDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        return displayName.Trim();
    }

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailMaxLength || email.Trim() != email)
        {
            return false;
        }

        return MailAddress.TryCreate(email, out var address)
            && address.Address == email
            && address.Host.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>BR-AKUN-07: an IANA zone name such as Asia/Jakarta; Windows zone ids are rejected.</summary>
    public static bool IsValidTimezone(string? timezone) =>
        !string.IsNullOrWhiteSpace(timezone)
        && timezone.Length <= TimezoneMaxLength
        && TimeZoneInfo.TryConvertIanaIdToWindowsId(timezone, out _)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _);

    /// <summary>
    /// BR-AKUN-05: admins cannot deactivate themselves, and at least one active admin must remain.
    /// </summary>
    public static AppError? CheckDeactivation(Guid actorId, UserProfile target, int otherActiveAdmins)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Id == actorId)
        {
            return AppError.InvalidState("Admins cannot deactivate their own account.");
        }

        if (target is { Role: UserRole.Admin, IsActive: true } && otherActiveAdmins == 0)
        {
            return AppError.InvalidState("At least one active admin must remain.");
        }

        return null;
    }

    [GeneratedRegex("^[a-z0-9._-]{3,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}

/// <summary>
/// BR-AKUN-02/04: twelve random characters without look-alikes (0/O, 1/l/I), because users copy them by hand.
/// </summary>
public static class TemporaryPassword
{
    public const int Length = 12;
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
