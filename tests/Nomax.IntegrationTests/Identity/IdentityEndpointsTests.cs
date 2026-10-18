using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nomax.IntegrationTests.Support;
using Nomax.Modules.Identity.Contracts;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Identity;

/// <summary>
/// Identity endpoints of docs/05 §4 against PostgreSQL and Valkey, with the Keycloak Admin API faked.
/// Each test uses its own usernames, so tests can run in parallel on the shared database.
/// </summary>
public sealed class IdentityEndpointsTests(NomaxApiFixture api)
{
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string AdminToken => TestUsers.TokenFor(Roles.Admin);

    [Fact]
    public async Task Me_returns_the_seeded_profile_with_defaults()
    {
        using var client = api.CreateClient();

        var me = await SendJsonAsync(client, HttpMethod.Get, "/api/identity/me", TestUsers.TokenFor(Roles.Sensei));

        me.GetProperty("id").GetGuid().ShouldBe(TestUsers.Sensei);
        me.GetProperty("role").GetString().ShouldBe("sensei");
        me.GetProperty("uiLocale").GetString().ShouldBe("id");
        me.GetProperty("timezone").GetString().ShouldBe("Asia/Jakarta");
    }

    [Fact]
    public async Task BR_AKUN_07_user_changes_own_preferences_including_script_off()
    {
        using var client = api.CreateClient();
        var (id, token) = await CreateAccountAsync(client, "pref");

        var updated = await SendJsonAsync(client, HttpMethod.Patch, "/api/identity/me/preferences", token,
            new { uiLocale = "en", scriptPref = "off", timezone = "Asia/Makassar" });
        var reread = await SendJsonAsync(client, HttpMethod.Get, "/api/identity/me", token);

        updated.GetProperty("scriptPref").GetString().ShouldBe("off");
        reread.GetProperty("id").GetGuid().ShouldBe(id);
        reread.GetProperty("uiLocale").GetString().ShouldBe("en");
        reread.GetProperty("scriptPref").GetString().ShouldBe("off");
        reread.GetProperty("timezone").GetString().ShouldBe("Asia/Makassar");
    }

    [Fact]
    public async Task BR_AKUN_07_invalid_preferences_are_400_with_field_errors()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Patch, "/api/identity/me/preferences", AdminToken,
            new { timezone = "Jakarta" });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("errors").TryGetProperty("timezone", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task BR_AKUN_02_admin_creates_an_account_in_keycloak_and_a_profile_with_the_same_id()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Post, "/api/identity/users", AdminToken, new
        {
            username = $"budi.{_suffix}",
            email = $"Budi.{_suffix}@Contoh.id",
            displayName = "  Budi Santoso ",
            role = "murid",
            uiLocale = "en",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var user = body.GetProperty("user");
        var id = user.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/api/identity/users/{id}");
        user.GetProperty("displayName").GetString().ShouldBe("Budi Santoso");
        user.GetProperty("timezone").GetString().ShouldBe("Asia/Jakarta");
        user.GetProperty("uiLocale").GetString().ShouldBe("en");

        var password = body.GetProperty("temporaryPassword").GetString()!;
        password.Length.ShouldBe(12);
        var keycloakUser = api.Keycloak.Users[id];
        keycloakUser.TemporaryPassword.ShouldBe(password);
        keycloakUser.Roles.ShouldBe(["murid"]);
    }

    [Fact]
    public async Task BR_AKUN_02_username_and_email_must_be_unique()
    {
        using var client = api.CreateClient();
        await CreateAccountAsync(client, "uniq", email: $"uniq.{_suffix}@contoh.id");

        using var sameUsername = await SendAsync(client, HttpMethod.Post, "/api/identity/users", AdminToken,
            new { username = $"uniq.{_suffix}", displayName = "Lagi", role = "murid" });
        using var sameEmail = await SendAsync(client, HttpMethod.Post, "/api/identity/users", AdminToken,
            new { username = $"other.{_suffix}", email = $"UNIQ.{_suffix}@CONTOH.ID", displayName = "Lain", role = "murid" });

        await sameUsername.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
        await sameEmail.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task BR_AKUN_02_invalid_input_is_400_with_camel_case_fields()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Post, "/api/identity/users", AdminToken,
            new { username = "No Spaces", email = "bukan-email", displayName = "", role = "murid" });

        var errors = (await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed")).GetProperty("errors");
        errors.TryGetProperty("username", out _).ShouldBeTrue();
        errors.TryGetProperty("email", out _).ShouldBeTrue();
        errors.TryGetProperty("displayName", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task BR_AKUN_02_keycloak_outage_is_503_and_creates_nothing()
    {
        using var client = api.CreateClient();
        var username = $"{FakeKeycloakAdmin.UnavailablePrefix}.{_suffix}";

        using var response = await SendAsync(client, HttpMethod.Post, "/api/identity/users", AdminToken,
            new { username, displayName = "Down", role = "murid" });

        await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "keycloak_unavailable");
        (await ListAsync(client, $"q={username}")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task BR_AKUN_02_failure_after_keycloak_user_creation_deletes_that_user_again()
    {
        using var client = api.CreateClient();
        var username = $"{FakeKeycloakAdmin.RoleFailurePrefix}.{_suffix}";

        using var response = await SendAsync(client, HttpMethod.Post, "/api/identity/users", AdminToken,
            new { username, displayName = "Half", role = "sensei" });

        await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "keycloak_unavailable");
        api.Keycloak.Users.Values.ShouldNotContain(user => user.Username == username);
        api.Keycloak.Calls.Count(call => call.Operation == "delete").ShouldBeGreaterThan(0);
        (await ListAsync(client, $"q={username}")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task BR_AKUN_05_deactivated_account_is_refused_at_once_and_can_be_reactivated()
    {
        using var client = api.CreateClient();
        var (id, token) = await CreateAccountAsync(client, "nonaktif");
        (await SendAsync(client, HttpMethod.Get, "/api/identity/me", token)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var deactivate = await SendAsync(client, HttpMethod.Post, $"/api/identity/users/{id}/deactivate", AdminToken);
        using var refused = await SendAsync(client, HttpMethod.Get, "/api/identity/me", token);

        deactivate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "account_inactive");
        api.Keycloak.Users[id].Enabled.ShouldBeFalse();
        api.Keycloak.Called("logout", id).ShouldBeTrue();
        (await SendJsonAsync(client, HttpMethod.Get, $"/api/identity/users/{id}", AdminToken)).GetProperty("isActive").GetBoolean().ShouldBeFalse();

        using var activate = await SendAsync(client, HttpMethod.Post, $"/api/identity/users/{id}/activate", AdminToken);
        using var allowed = await SendAsync(client, HttpMethod.Get, "/api/identity/me", token);

        activate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        api.Keycloak.Users[id].Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task BR_AKUN_05_admin_cannot_deactivate_self()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Post, $"/api/identity/users/{TestUsers.Admin}/deactivate", AdminToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "invalid_state");
    }

    [Fact]
    public async Task BR_AKUN_04_reset_password_issues_a_new_temporary_password_and_ends_sessions()
    {
        using var client = api.CreateClient();
        var (id, _) = await CreateAccountAsync(client, "reset");
        var before = api.Keycloak.Users[id].TemporaryPassword;

        var reset = await SendJsonAsync(client, HttpMethod.Post, $"/api/identity/users/{id}/reset-password", AdminToken);

        var password = reset.GetProperty("temporaryPassword").GetString();
        password.ShouldNotBe(before);
        api.Keycloak.Users[id].TemporaryPassword.ShouldBe(password);
        api.Keycloak.Called("logout", id).ShouldBeTrue();
    }

    [Fact]
    public async Task BR_AKUN_06_admin_changes_display_name_email_and_timezone_in_both_systems()
    {
        using var client = api.CreateClient();
        var (id, _) = await CreateAccountAsync(client, "ubah", email: $"ubah.{_suffix}@contoh.id");

        var updated = await SendJsonAsync(client, HttpMethod.Patch, $"/api/identity/users/{id}", AdminToken,
            new { displayName = " Nama Baru ", email = "", timezone = "Asia/Jayapura" });

        updated.GetProperty("displayName").GetString().ShouldBe("Nama Baru");
        updated.GetProperty("email").ValueKind.ShouldBe(JsonValueKind.Null);
        updated.GetProperty("timezone").GetString().ShouldBe("Asia/Jayapura");
        api.Keycloak.Users[id].DisplayName.ShouldBe("Nama Baru");
        api.Keycloak.Users[id].Email.ShouldBeNull();
    }

    [Fact]
    public async Task Admin_cannot_give_an_account_an_email_that_is_already_used()
    {
        using var client = api.CreateClient();
        var (id, _) = await CreateAccountAsync(client, "emailA");

        using var response = await SendAsync(client, HttpMethod.Patch, $"/api/identity/users/{id}", AdminToken,
            new { email = "ADMIN.DEV@nomax.test" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task List_filters_searches_literally_and_pages()
    {
        using var client = api.CreateClient();
        await CreateAccountAsync(client, "list_a", role: "sensei");
        await CreateAccountAsync(client, "listxa");
        await CreateAccountAsync(client, "list_b");

        var underscore = await ListAsync(client, $"q=list_&pageSize=1");
        var all = await ListAsync(client, $"q={_suffix}");
        var senseis = await ListAsync(client, $"q={_suffix}&role=sensei&isActive=true");

        // "_" is not a wildcard: listxa does not match "list_".
        underscore.GetProperty("totalCount").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
        underscore.GetProperty("items").GetArrayLength().ShouldBe(1);
        all.GetProperty("totalCount").GetInt32().ShouldBe(3);
        senseis.GetProperty("items").EnumerateArray().Select(user => user.GetProperty("role").GetString()).ShouldBe(["sensei"]);
        (await ListAsync(client, $"q=listxa.{_suffix}")).GetProperty("totalCount").GetInt32().ShouldBe(1);
        (await ListAsync(client, $"q=list_.{_suffix}")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task List_rejects_an_unknown_role_filter()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, "/api/identity/users?role=superuser", AdminToken);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("errors").TryGetProperty("role", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task BR_AKUN_03_dry_run_reports_errors_and_creates_nothing()
    {
        using var client = api.CreateClient();
        var csv = $"username,display_name,role\nimp.ok.{_suffix},Ok,murid\nBAD,,admin\n";

        var result = await ImportAsync(client, csv, dryRun: true);

        result.GetProperty("errors").GetArrayLength().ShouldBe(3);
        result.GetProperty("created").GetArrayLength().ShouldBe(0);
        (await ListAsync(client, $"q=imp.ok.{_suffix}")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task BR_AKUN_03_import_creates_accounts_once_and_skips_them_on_reimport()
    {
        using var client = api.CreateClient();
        var csv = $"username;display_name;role;ui_locale\nimp.a.{_suffix};Impor A;murid;en\nimp.b.{_suffix};Impor B;sensei;\n";

        var dryRun = await ImportAsync(client, csv, dryRun: true);
        var imported = await ImportAsync(client, csv, dryRun: false);
        var again = await ImportAsync(client, csv, dryRun: false);

        dryRun.GetProperty("toCreate").GetArrayLength().ShouldBe(2);
        imported.GetProperty("created").GetArrayLength().ShouldBe(2);
        var passwords = imported.GetProperty("passwordsCsv").GetString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        passwords[0].ShouldBe("username,temporary_password");
        passwords[1].ShouldStartWith($"imp.a.{_suffix},");
        again.GetProperty("skipped").GetArrayLength().ShouldBe(2);
        again.GetProperty("created").GetArrayLength().ShouldBe(0);
        again.GetProperty("passwordsCsv").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task BR_AKUN_03_keycloak_failure_stops_the_import_and_reports_the_row()
    {
        using var client = api.CreateClient();
        var csv = $"username,display_name,role\nimp.first.{_suffix},First,murid\n{FakeKeycloakAdmin.UnavailablePrefix}.{_suffix},Second,murid\nimp.third.{_suffix},Third,murid\n";

        var result = await ImportAsync(client, csv, dryRun: false);

        result.GetProperty("created").EnumerateArray().Select(name => name.GetString()).ShouldBe([$"imp.first.{_suffix}"]);
        var failure = result.GetProperty("failure");
        failure.GetProperty("row").GetInt32().ShouldBe(3);
        failure.GetProperty("code").GetString().ShouldBe("keycloak_unavailable");
    }

    [Fact]
    public async Task Import_without_a_file_is_400()
    {
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/identity/users/import?dryRun=true")
        {
            Content = new MultipartFormDataContent(),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);

        using var response = await client.SendAsync(request, Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task User_directory_returns_short_profiles_for_other_modules()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

        var users = await directory.GetUsersAsync([TestUsers.Murid, TestUsers.Sensei, Guid.NewGuid()], Cancel);

        users.Count.ShouldBe(2);
        users[TestUsers.Murid].ShouldBe(new UserSummary(
            TestUsers.Murid, "murid1.dev", "Murid Satu", "murid", true, users[TestUsers.Murid].Timezone, users[TestUsers.Murid].UiLocale, "murid1.dev@nomax.test"));
        users[TestUsers.Sensei].Role.ShouldBe("sensei");
    }

    private async Task<(Guid Id, string Token)> CreateAccountAsync(HttpClient client, string name, string role = "murid", string? email = null)
    {
        var created = await SendJsonAsync(client, HttpMethod.Post, "/api/identity/users", AdminToken,
            new { username = $"{name.ToLowerInvariant()}.{_suffix}", email, displayName = $"Test {name}", role });
        var id = created.GetProperty("user").GetProperty("id").GetGuid();
        return (id, TestJwt.For(id, role));
    }

    private static async Task<JsonElement> ListAsync(HttpClient client, string query) =>
        await SendJsonAsync(client, HttpMethod.Get, $"/api/identity/users?{query}", AdminToken);

    private static async Task<JsonElement> ImportAsync(HttpClient client, string csv, bool dryRun)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "users.csv");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/identity/users/import?dryRun={(dryRun ? "true" : "false")}")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);
        using var response = await client.SendAsync(request, Cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, Cancel);
    }

    private static async Task<JsonElement> SendJsonAsync(HttpClient client, HttpMethod method, string path, string token, object? body = null)
    {
        using var response = await SendAsync(client, method, path, token, body);
        response.IsSuccessStatusCode.ShouldBeTrue($"{method} {path} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return await response.ReadJsonAsync();
    }
}
