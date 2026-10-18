using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Persistence;
using Nomax.IntegrationTests.Support;
using Nomax.Modules.Identity.Infrastructure;
using Npgsql;

namespace Nomax.IntegrationTests.Identity;

/// <summary>
/// The migrated database matches the data contract of docs/03 §4.1 (AGENTS.md §5), checked in the catalog itself.
/// </summary>
public sealed class IdentitySchemaTests(NomaxApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task User_profiles_columns_match_the_contract()
    {
        var columns = await QueryAsync("""
            select column_name, data_type, coalesce(character_maximum_length::text, ''), is_nullable, coalesce(column_default, '')
            from information_schema.columns
            where table_schema = 'identity' and table_name = 'user_profiles'
            order by ordinal_position
            """);

        columns.ShouldBe(
        [
            "id|uuid||NO|",
            "username|character varying|64|NO|",
            "email|character varying|254|YES|",
            "display_name|character varying|80|NO|",
            "role|text||NO|",
            "ui_locale|text||NO|'id'::text",
            "script_pref|text||NO|'kanji_furigana'::text",
            "timezone|character varying|64|NO|'Asia/Jakarta'::character varying",
            "is_active|boolean||NO|true",
            "created_by|uuid||YES|",
            "created_at|timestamp with time zone||NO|now()",
            "updated_at|timestamp with time zone||NO|now()",
        ]);
    }

    [Fact]
    public async Task User_profiles_constraints_and_indexes_match_the_contract()
    {
        var checks = await QueryAsync("""
            select conname, pg_get_constraintdef(oid) from pg_constraint
            where conrelid = 'identity.user_profiles'::regclass and contype = 'c' order by conname
            """);
        var indexes = await QueryAsync("""
            select indexname, indexdef from pg_indexes where schemaname = 'identity' and tablename = 'user_profiles' order by indexname
            """);

        checks.ShouldBe(
        [
            "ck_user_profiles_role|CHECK ((role = ANY (ARRAY['admin'::text, 'sensei'::text, 'murid'::text])))",
            "ck_user_profiles_script_pref|CHECK ((script_pref = ANY (ARRAY['off'::text, 'kanji_furigana'::text, 'kana'::text, 'romaji'::text, 'kanji_furigana_romaji'::text])))",
            "ck_user_profiles_ui_locale|CHECK ((ui_locale = ANY (ARRAY['id'::text, 'en'::text])))",
        ]);
        indexes.Select(index => index.Split('|')[0]).ShouldBe(
            ["ix_user_profiles_lower_email", "ix_user_profiles_role_is_active", "ix_user_profiles_username", "pk_user_profiles"]);
        indexes[0].ShouldEndWith("(lower((email)::text)) WHERE (email IS NOT NULL)");
        indexes[2].ShouldContain("CREATE UNIQUE INDEX");
    }

    [Fact]
    public async Task Migration_history_lives_in_the_identity_schema()
    {
        var history = await QueryAsync("select migration_id from identity.__ef_migrations_history order by migration_id");

        history.Count.ShouldBe(2);
        history[0].ShouldEndWith("_CreateUserProfiles");
        history[1].ShouldEndWith("_AddOutboxInbox");
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("content")]
    [InlineData("classroom")]
    public async Task Every_module_schema_has_its_outbox_and_inbox(string schema)
    {
        var tables = await QueryAsync(
            $"select table_name from information_schema.tables where table_schema = '{schema}' and table_name like '%box_messages' order by table_name");

        tables.ShouldBe(["inbox_messages", "outbox_messages"]);
    }

    [Fact]
    public async Task Development_seed_is_idempotent()
    {
        await using var scope = api.Services.CreateAsyncScope();
        foreach (var seeder in scope.ServiceProvider.GetServices<IDevelopmentSeeder>())
        {
            await seeder.SeedAsync(Cancel);
            await seeder.SeedAsync(Cancel);
        }

        var seeded = await QueryAsync(
            "select username from identity.user_profiles where id::text like 'a0000000-0000-4000-8000-00000000000%' order by username");
        seeded.ShouldBe(["admin.dev", "murid1.dev", "murid2.dev", "sensei.dev"]);
        scope.ServiceProvider.GetRequiredService<IdentityDbContext>().ShouldNotBeNull();
    }

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(api.PostgresConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Cancel);
        var rows = new List<string>();
        while (await reader.ReadAsync(Cancel))
        {
            rows.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i)?.ToString())));
        }

        return rows;
    }
}
