using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.Modules.Identity.Domain;

namespace Nomax.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// identity.user_profiles exactly as docs/03 §4.1. Enum columns get their CHECK constraints from the module conventions.
/// The unique index on lower(email) is an expression index and lives in the migration as SQL.
/// </summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");
        builder.HasKey(profile => profile.Id);

        // The id is the Keycloak sub, never generated here.
        builder.Property(profile => profile.Id).ValueGeneratedNever();
        builder.Property(profile => profile.Username).HasMaxLength(AccountRules.UsernameMaxLength);
        builder.Property(profile => profile.Email).HasMaxLength(AccountRules.EmailMaxLength);
        builder.Property(profile => profile.DisplayName).HasMaxLength(AccountRules.DisplayNameMaxLength);
        builder.Property(profile => profile.Timezone).HasMaxLength(AccountRules.TimezoneMaxLength);

        // Database defaults from the schema contract. ValueGeneratedNever keeps EF sending the value even when it
        // equals the CLR default (for example ScriptPref.Off), instead of silently falling back to the column default.
        builder.Property(profile => profile.UiLocale).HasDefaultValueSql("'id'").ValueGeneratedNever();
        builder.Property(profile => profile.ScriptPref).HasDefaultValueSql("'kanji_furigana'").ValueGeneratedNever();
        builder.Property(profile => profile.Timezone).HasDefaultValueSql("'Asia/Jakarta'").ValueGeneratedNever();
        builder.Property(profile => profile.IsActive).HasDefaultValueSql("true").ValueGeneratedNever();

        builder.HasIndex(profile => profile.Username).IsUnique();
        builder.HasIndex(profile => new { profile.Role, profile.IsActive });
    }
}
