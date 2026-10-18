using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Identity;

/// <summary>
/// Authorization matrix of every Identity endpoint (docs/09 §1): no token → 401, wrong role → 403,
/// unknown resource → 404. The 2xx cases are covered in <see cref="IdentityEndpointsTests"/>.
/// </summary>
public sealed class IdentityAuthorizationTests(NomaxApiFixture api)
{
    private const string UnknownId = "00000000-0000-4000-8000-0000000000ff";

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/identity/users" },
        { "GET", $"/api/identity/users/{UnknownId}" },
        { "POST", "/api/identity/users" },
        { "PATCH", $"/api/identity/users/{UnknownId}" },
        { "POST", "/api/identity/users/import?dryRun=true" },
        { "POST", $"/api/identity/users/{UnknownId}/reset-password" },
        { "POST", $"/api/identity/users/{UnknownId}/deactivate" },
        { "POST", $"/api/identity/users/{UnknownId}/activate" },
    };

    public static TheoryData<string, string> OwnEndpoints => new()
    {
        { "GET", "/api/identity/me" },
        { "PATCH", "/api/identity/me/preferences" },
    };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    [MemberData(nameof(OwnEndpoints))]
    public async Task Without_a_token_every_endpoint_is_401(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(Request(method, path, token: null), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Sensei_and_murid_get_403_on_admin_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        foreach (var role in new[] { Roles.Sensei, Roles.Murid })
        {
            using var response = await client.SendAsync(Request(method, path, TestUsers.TokenFor(role)), Cancel);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }
    }

    [Theory]
    [InlineData("GET", $"/api/identity/users/{UnknownId}")]
    [InlineData("PATCH", $"/api/identity/users/{UnknownId}")]
    [InlineData("POST", $"/api/identity/users/{UnknownId}/reset-password")]
    [InlineData("POST", $"/api/identity/users/{UnknownId}/deactivate")]
    [InlineData("POST", $"/api/identity/users/{UnknownId}/activate")]
    public async Task Admin_gets_404_for_an_unknown_account(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(Request(method, path, TestUsers.TokenFor(Roles.Admin)), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [MemberData(nameof(OwnEndpoints))]
    public async Task Every_role_reaches_its_own_profile(string method, string path)
    {
        using var client = api.CreateClient();

        foreach (var role in new[] { Roles.Admin, Roles.Sensei, Roles.Murid })
        {
            using var response = await client.SendAsync(Request(method, path, TestUsers.TokenFor(role)), Cancel);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Token_of_an_account_without_profile_is_403_account_inactive()
    {
        using var client = api.CreateClient();
        var stranger = TestJwt.For(Guid.NewGuid(), Roles.Admin);

        using var response = await client.SendAsync(Request("GET", "/api/identity/me", stranger), Cancel);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "account_inactive");
    }

    private static HttpRequestMessage Request(string method, string path, string? token)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Content = path.Contains("/import", StringComparison.Ordinal)
            ? Csv("username,display_name,role\nmatrix.user,Matrix,murid\n")
            : method is "POST" or "PATCH"
                ? JsonContent.Create(new { displayName = "Matrix User", username = "matrix.user", role = "murid" })
                : null;
        return request;
    }

    private static MultipartFormDataContent Csv(string csv)
    {
        var content = new MultipartFormDataContent();
        var file = new StringContent(csv, Encoding.UTF8, "text/csv");
        content.Add(file, "file", "users.csv");
        return content;
    }
}
