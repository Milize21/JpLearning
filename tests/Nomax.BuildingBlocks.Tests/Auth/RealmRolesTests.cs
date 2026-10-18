using System.Security.Claims;
using Nomax.BuildingBlocks.Auth;

namespace Nomax.BuildingBlocks.Tests.Auth;

public sealed class RealmRolesTests
{
    [Fact]
    public void Keeps_only_application_roles()
    {
        // Shape of a real Keycloak token for a student (verified against the dev realm).
        const string realmAccess = """{"roles":["murid","offline_access","default-roles-nomax","uma_authorization"]}""";

        RealmRoles.Parse(realmAccess).ShouldBe(["murid"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"roles":"admin"}""")]
    [InlineData("""["admin"]""")]
    [InlineData("""{"roles":[1,null,"superuser"]}""")]
    public void Malformed_or_unknown_input_gives_no_roles(string? realmAccess)
    {
        RealmRoles.Parse(realmAccess).ShouldBeEmpty();
    }

    [Fact]
    public void Adds_role_claims_that_IsInRole_understands()
    {
        var identity = new ClaimsIdentity(
            [new Claim(NomaxClaimTypes.RealmAccess, """{"roles":["sensei","offline_access"]}""")],
            authenticationType: "test",
            nameType: NomaxClaimTypes.PreferredUsername,
            roleType: NomaxClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        RealmRoles.AddRoleClaims(principal);
        RealmRoles.AddRoleClaims(principal);

        principal.IsInRole("sensei").ShouldBeTrue();
        principal.IsInRole("offline_access").ShouldBeFalse();
        principal.FindAll(NomaxClaimTypes.Role).Count().ShouldBe(1);
    }

    [Fact]
    public void User_id_comes_from_sub()
    {
        var id = Guid.Parse("a0000000-0000-4000-8000-000000000003");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(NomaxClaimTypes.Subject, id.ToString())], "test"));

        principal.FindUserId().ShouldBe(id);
        new ClaimsPrincipal(new ClaimsIdentity()).FindUserId().ShouldBeNull();
    }
}
