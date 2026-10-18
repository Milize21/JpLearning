using Nomax.Modules.Identity.Domain;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Tests.Domain;

public sealed class AccountRulesTests
{
    private static readonly Guid Actor = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    [Theory]
    [InlineData("abc", true)]
    [InlineData("murid1.dev", true)]
    [InlineData("sensei_tanaka-2", true)]
    [InlineData("ab", false)]
    [InlineData("Murid", false)]
    [InlineData("murid satu", false)]
    [InlineData("murid@dev", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void BR_AKUN_02_username_is_3_to_64_of_a_z_0_9_dot_underscore_hyphen(string? username, bool valid)
    {
        AccountRules.IsValidUsername(username).ShouldBe(valid);
    }

    [Fact]
    public void BR_AKUN_02_username_longer_than_64_is_rejected()
    {
        AccountRules.IsValidUsername(new string('a', 64)).ShouldBeTrue();
        AccountRules.IsValidUsername(new string('a', 65)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("murid@nomax.test", true)]
    [InlineData("nama.lengkap+kelas@contoh.co.id", true)]
    [InlineData("tanpa-at", false)]
    [InlineData("a@b", false)]
    [InlineData(" spasi@nomax.test", false)]
    [InlineData("Nama <murid@nomax.test>", false)]
    public void BR_AKUN_02_email_must_be_a_plain_address(string email, bool valid)
    {
        AccountRules.IsValidEmail(email).ShouldBe(valid);
    }

    [Theory]
    [InlineData("Budi", true)]
    [InlineData("  Budi  ", true)]
    [InlineData("   ", false)]
    [InlineData("", false)]
    public void BR_AKUN_06_display_name_is_trimmed_and_not_empty(string displayName, bool valid)
    {
        AccountRules.IsValidDisplayName(displayName).ShouldBe(valid);
    }

    [Fact]
    public void BR_AKUN_06_display_name_is_at_most_80_characters_after_trimming()
    {
        AccountRules.IsValidDisplayName(" " + new string('a', 80) + " ").ShouldBeTrue();
        AccountRules.IsValidDisplayName(new string('a', 81)).ShouldBeFalse();
        AccountRules.NormalizeDisplayName("  Murid Satu  ").ShouldBe("Murid Satu");
    }

    [Theory]
    [InlineData("Asia/Jakarta", true)]
    [InlineData("Asia/Makassar", true)]
    [InlineData("Asia/Tokyo", true)]
    [InlineData("UTC", true)]
    [InlineData("SE Asia Standard Time", false)]
    [InlineData("Asia/Atlantis", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void BR_AKUN_07_timezone_must_be_an_iana_zone(string? timezone, bool valid)
    {
        AccountRules.IsValidTimezone(timezone).ShouldBe(valid);
    }

    [Fact]
    public void BR_AKUN_05_admin_cannot_deactivate_self()
    {
        var self = Profile(Actor, UserRole.Admin);

        AccountRules.CheckDeactivation(Actor, self, otherActiveAdmins: 3)!.Code.ShouldBe(ErrorCodes.InvalidState);
    }

    [Fact]
    public void BR_AKUN_05_last_active_admin_cannot_be_deactivated()
    {
        var lastAdmin = Profile(Guid.NewGuid(), UserRole.Admin);

        AccountRules.CheckDeactivation(Actor, lastAdmin, otherActiveAdmins: 0)!.Code.ShouldBe(ErrorCodes.InvalidState);
        AccountRules.CheckDeactivation(Actor, lastAdmin, otherActiveAdmins: 1).ShouldBeNull();
    }

    [Fact]
    public void BR_AKUN_05_students_and_teachers_can_always_be_deactivated()
    {
        AccountRules.CheckDeactivation(Actor, Profile(Guid.NewGuid(), UserRole.Murid), otherActiveAdmins: 0).ShouldBeNull();
        AccountRules.CheckDeactivation(Actor, Profile(Guid.NewGuid(), UserRole.Sensei), otherActiveAdmins: 0).ShouldBeNull();
    }

    [Fact]
    public void BR_AKUN_02_temporary_password_has_12_unambiguous_characters()
    {
        var passwords = Enumerable.Range(0, 200).Select(_ => TemporaryPassword.Generate()).ToList();

        passwords.ShouldAllBe(password => password.Length == 12);
        passwords.ShouldAllBe(password => password.All(c => TemporaryPassword.Alphabet.Contains(c)));
        string.Concat(passwords).IndexOfAny(['0', 'O', '1', 'l', 'I']).ShouldBe(-1);
        passwords.Distinct().Count().ShouldBe(passwords.Count);
    }

    [Fact]
    public void BR_AKUN_07_preferences_change_only_given_fields()
    {
        var profile = Profile(Guid.NewGuid(), UserRole.Murid);

        profile.UpdatePreferences(UiLocale.En, null, null);

        profile.UiLocale.ShouldBe(UiLocale.En);
        profile.ScriptPref.ShouldBe(ScriptPref.KanjiFurigana);
        profile.Timezone.ShouldBe("Asia/Jakarta");
    }

    private static UserProfile Profile(Guid id, UserRole role) =>
        UserProfile.Create(id, "user", null, "User", role, "Asia/Jakarta", UiLocale.Id, createdBy: null);
}
