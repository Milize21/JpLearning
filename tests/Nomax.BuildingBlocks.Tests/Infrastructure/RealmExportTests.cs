using System.Text.Json;
using Nomax.SharedKernel;

namespace Nomax.BuildingBlocks.Tests.Infrastructure;

/// <summary>
/// Keeps infra/keycloak/realm-nomax.json in line with docs/01 §8 and with the code.
/// </summary>
public sealed class RealmExportTests : IDisposable
{
    private readonly JsonDocument _realm = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(RepositoryRoot.Path, "infra", "keycloak", "realm-nomax.json")));

    private JsonElement Root => _realm.RootElement;

    [Fact]
    public void Realm_roles_are_the_application_roles()
    {
        var roles = Root.GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Select(role => role.GetProperty("name").GetString());

        roles.ShouldBe(Roles.All, ignoreOrder: true);
    }

    [Fact]
    public void Self_registration_is_off_and_brute_force_detection_is_on()
    {
        Root.GetProperty("registrationAllowed").GetBoolean().ShouldBeFalse();
        Root.GetProperty("bruteForceProtected").GetBoolean().ShouldBeTrue();
        Root.GetProperty("accessTokenLifespan").GetInt32().ShouldBe(300);
        Root.GetProperty("revokeRefreshToken").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Web_client_is_public_with_pkce_and_the_api_audience()
    {
        var web = Client("nomax-web");

        web.GetProperty("publicClient").GetBoolean().ShouldBeTrue();
        web.GetProperty("directAccessGrantsEnabled").GetBoolean().ShouldBeFalse();
        web.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString().ShouldBe("S256");
        web.GetProperty("protocolMappers").EnumerateArray()
            .Single(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper")
            .GetProperty("config").GetProperty("included.client.audience").GetString().ShouldBe("nomax-api");
    }

    [Fact]
    public void Api_client_secret_is_never_committed()
    {
        Client("nomax-api").GetProperty("secret").GetString().ShouldBe("${NOMAX_API_CLIENT_SECRET}");
    }

    [Fact]
    public void Dev_users_have_fixed_ids_and_one_role_each()
    {
        var users = Root.GetProperty("users").EnumerateArray()
            .Where(user => user.TryGetProperty("id", out _))
            .ToDictionary(
                user => user.GetProperty("username").GetString()!,
                user => (Id: user.GetProperty("id").GetString(), Roles: user.GetProperty("realmRoles").EnumerateArray()
                    .Select(role => role.GetString()).Where(Roles.IsKnown).ToArray()));

        users["admin.dev"].Id.ShouldBe("a0000000-0000-4000-8000-000000000001");
        users["admin.dev"].Roles.ShouldBe([Roles.Admin]);
        users["sensei.dev"].Id.ShouldBe("a0000000-0000-4000-8000-000000000002");
        users["sensei.dev"].Roles.ShouldBe([Roles.Sensei]);
        users["murid1.dev"].Id.ShouldBe("a0000000-0000-4000-8000-000000000003");
        users["murid1.dev"].Roles.ShouldBe([Roles.Murid]);
        users["murid2.dev"].Id.ShouldBe("a0000000-0000-4000-8000-000000000004");
        users["murid2.dev"].Roles.ShouldBe([Roles.Murid]);
    }

    public void Dispose() => _realm.Dispose();

    private JsonElement Client(string clientId) =>
        Root.GetProperty("clients").EnumerateArray().Single(client => client.GetProperty("clientId").GetString() == clientId);
}
