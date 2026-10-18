using System.Net;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Content;

/// <summary>
/// Authorization matrix of the Content endpoints (docs/09 §1, docs/05 §5): no token → 401, wrong role → 403,
/// unknown resource → 404, locked chapter → 403 chapter_locked. Successful calls are in <see cref="ContentEndpointsTests"/>.
/// </summary>
public sealed class ContentAuthorizationTests(NomaxApiFixture api)
{
    private const string Unknown = "00000000-0000-4000-8000-0000000000ff";
    private const string Book = "b1000000-0000-4000-8000-000000000001";
    private const string Chapter1 = "b2000000-0000-4000-8000-000000000001";
    private const string Chapter2 = "b2000000-0000-4000-8000-000000000002";

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "POST", "/api/content/levels" },
        { "PATCH", $"/api/content/levels/{Unknown}" },
        { "DELETE", $"/api/content/levels/{Unknown}" },
        { "POST", "/api/content/books" },
        { "PATCH", $"/api/content/books/{Unknown}" },
        { "DELETE", $"/api/content/books/{Unknown}" },
        { "POST", "/api/content/chapters" },
        { "PATCH", $"/api/content/chapters/{Unknown}" },
        { "DELETE", $"/api/content/chapters/{Unknown}" },
        { "POST", "/api/content/reference/kana/import" },
        { "PATCH", $"/api/content/reference/kana/{Unknown}" },
        { "PATCH", $"/api/content/reference/kanji/{Unknown}" },
        { "PATCH", $"/api/content/reference/vocab/{Unknown}" },
        { "PATCH", $"/api/content/reference/grammar/{Unknown}" },
        { "PATCH", $"/api/content/reference/sentences/{Unknown}" },
        { "DELETE", $"/api/content/reference/kana/{Unknown}" },
    };

    public static TheoryData<string, string> StaffEndpoints => new()
    {
        { "POST", $"/api/content/chapters/{Chapter1}/materials" },
        { "PATCH", $"/api/content/materials/{Unknown}" },
        { "DELETE", $"/api/content/materials/{Unknown}" },
    };

    public static TheoryData<string, string> SignedInEndpoints => new()
    {
        { "GET", "/api/content/levels" },
        { "GET", $"/api/content/books/{Book}" },
        { "GET", $"/api/content/books/{Book}/cover" },
        { "GET", $"/api/content/chapters/{Chapter1}" },
        { "GET", $"/api/content/chapters/{Chapter1}/materials" },
        { "GET", $"/api/content/materials/{Unknown}/download" },
        { "GET", "/api/content/reference/kana" },
        { "GET", "/api/content/reference/kanji" },
        { "GET", "/api/content/reference/vocab" },
        { "GET", "/api/content/reference/grammar" },
        { "GET", "/api/content/reference/sentences" },
        { "POST", "/api/content/uploads" },
    };

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    [MemberData(nameof(StaffEndpoints))]
    [MemberData(nameof(SignedInEndpoints))]
    public async Task Without_a_token_every_endpoint_is_401(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, token: null, Body(method, path));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Sensei_and_murid_get_403_on_admin_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        foreach (var role in new[] { Roles.Sensei, Roles.Murid })
        {
            using var response = await client.SendAsync(method, path, TestUsers.TokenFor(role), Body(method, path));
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }
    }

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    public async Task Murid_gets_403_on_staff_endpoints(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Murid), Body(method));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Theory]
    [InlineData("PATCH", $"/api/content/levels/{Unknown}")]
    [InlineData("DELETE", $"/api/content/books/{Unknown}")]
    [InlineData("GET", $"/api/content/books/{Unknown}")]
    [InlineData("GET", $"/api/content/chapters/{Unknown}")]
    [InlineData("GET", $"/api/content/chapters/{Unknown}/materials")]
    [InlineData("PATCH", $"/api/content/materials/{Unknown}")]
    [InlineData("DELETE", $"/api/content/reference/vocab/{Unknown}")]
    [InlineData("DELETE", $"/api/content/reference/unknown-type/{Unknown}")]
    public async Task Admin_gets_404_for_unknown_resources(string method, string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync(method, path, TestUsers.TokenFor(Roles.Admin), Body(method));

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [InlineData($"/api/content/chapters/{Chapter2}")]
    [InlineData($"/api/content/chapters/{Chapter2}/materials")]
    [InlineData($"/api/content/reference/kana?chapterId={Chapter2}")]
    public async Task BR_AKSES_01_murid_gets_403_chapter_locked_for_a_closed_chapter(string path)
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync("GET", path, TestUsers.TokenFor(Roles.Murid));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "chapter_locked");
    }

    [Fact]
    public async Task BR_FILE_04_signed_download_without_valid_signature_is_404()
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync("GET", $"/api/content/files/{Unknown}/content?exp=9999999999&sig=forged", token: null);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    // The import only matches multipart requests (endpoint selection runs before authorization), so it gets a form.
    private static object? Body(string method, string? path = null)
    {
        if (path?.EndsWith("/import", StringComparison.Ordinal) == true)
        {
            return new MultipartFormDataContent { { new StringContent("char,script\n"), "file", "kana.csv" } };
        }

        object json = new { version = 0, code = "matrix", name = new { id = "Matrix" }, sortOrder = 0, kind = "link", title = new { id = "x" }, url = "https://example.com" };
        return method is "POST" or "PATCH" ? json : null;
    }
}
