using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;

namespace Nomax.IntegrationTests.Content;

/// <summary>
/// Content behaviour against PostgreSQL: structure with optimistic concurrency and deletion rules (BR-KONTEN-03),
/// reference import, materials (BR-KONTEN-01/02/05) and tus uploads with signed downloads (BR-FILE-01/02/04/06).
/// </summary>
public sealed class ContentEndpointsTests(NomaxApiFixture api)
{
    // Chapter 1 of the kana book, unlocked for the demo class of the seed (murid1 is a member).
    private const string Chapter1 = "b2000000-0000-4000-8000-000000000001";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Admin => TestUsers.TokenFor(Roles.Admin);

    private static string Sensei => TestUsers.TokenFor(Roles.Sensei);

    private static string Murid => TestUsers.TokenFor(Roles.Murid);

    [Fact]
    public async Task BR_KONTEN_03_structure_crud_with_version_check_and_dependents()
    {
        using var client = api.CreateClient();
        var code = $"x{Guid.NewGuid():N}"[..10];
        var level = await client.JsonAsync("POST", "/api/content/levels", Admin, new { code, name = new { id = "Uji", ja = new[] { new { t = "試験", r = "しけん" } } }, sortOrder = 9 });
        var levelId = level.GetProperty("id").GetGuid();
        (await client.StatusAsync("POST", "/api/content/levels", Admin, new { code, name = new { id = "Lagi" }, sortOrder = 1 })).ShouldBe(409);
        (await client.StatusAsync("POST", "/api/content/levels", Admin, new { code = "x2", name = new { id = "Kanji", ja = new[] { new { t = "試験" } } }, sortOrder = 1 })).ShouldBe(400);

        var book = await client.JsonAsync("POST", "/api/content/books", Admin, new { levelId, title = new { id = "Buku" }, sortOrder = 0 });
        var bookId = book.GetProperty("id").GetGuid();
        var chapter = await client.JsonAsync("POST", "/api/content/chapters", Admin, new { bookId, number = 1, title = new { id = "Bab 1" }, sortOrder = 1 });
        var chapterId = chapter.GetProperty("id").GetGuid();
        (await client.StatusAsync("POST", "/api/content/chapters", Admin, new { bookId, number = 1, title = new { id = "Bab 1 lagi" }, sortOrder = 2 })).ShouldBe(409);

        // Optimistic concurrency: the second editor with the old version gets 409 concurrency_conflict.
        var version = chapter.GetProperty("version").GetUInt32();
        var edited = await client.JsonAsync("PATCH", $"/api/content/chapters/{chapterId}", Admin, new { version, title = new { id = "Bab satu" } });
        using var stale = await client.SendAsync("PATCH", $"/api/content/chapters/{chapterId}", Admin, new { version, title = new { id = "Bab 1!" } });
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "concurrency_conflict");
        edited.GetProperty("version").GetUInt32().ShouldNotBe(version);

        // Deleting from the top fails while something is below; from the bottom it works.
        using var levelInUse = await client.SendAsync("DELETE", $"/api/content/levels/{levelId}", Admin);
        await levelInUse.ShouldBeProblemAsync(HttpStatusCode.Conflict, "has_dependents");
        (await client.StatusAsync("DELETE", $"/api/content/books/{bookId}", Admin)).ShouldBe(409);
        (await client.StatusAsync("DELETE", $"/api/content/chapters/{chapterId}", Admin)).ShouldBe(204);
        (await client.StatusAsync("DELETE", $"/api/content/books/{bookId}", Admin)).ShouldBe(204);
        (await client.StatusAsync("DELETE", $"/api/content/levels/{levelId}", Admin)).ShouldBe(204);
    }

    [Fact]
    public async Task BR_KONTEN_03_chapter_with_an_unlock_cannot_be_deleted()
    {
        using var client = api.CreateClient();

        using var response = await client.SendAsync("DELETE", $"/api/content/chapters/{Chapter1}", Admin);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "has_dependents");
    }

    [Fact]
    public async Task Reference_import_validates_first_then_creates_and_skips_existing_rows()
    {
        using var client = api.CreateClient();
        var code = $"r{Guid.NewGuid():N}"[..10];
        var level = await client.JsonAsync("POST", "/api/content/levels", Admin, new { code, name = new { id = "Ref" }, sortOrder = 9 });
        var levelId = level.GetProperty("id").GetGuid();
        var book = await client.JsonAsync("POST", "/api/content/books", Admin, new { levelId, title = new { id = "Buku" }, sortOrder = 0 });
        var bookId = book.GetProperty("id").GetGuid();
        await client.JsonAsync("POST", "/api/content/chapters", Admin, new { bookId, number = 1, title = new { id = "Bab 1" }, sortOrder = 1 });
        var word = $"語{Guid.NewGuid():N}"[..6];
        var csv = $"word,reading,meanings_id,meanings_en,chapter\n{word},ご,bahasa,language,1\n{word}2,ごに,dua,two,7\n";

        var dryRun = await ImportAsync(client, "vocab", csv, levelId, bookId, dryRun: true);
        dryRun.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("unknown_chapter");
        dryRun.GetProperty("created").GetInt32().ShouldBe(0);

        var good = csv.Replace(",7\n", ",1\n", StringComparison.Ordinal);
        (await ImportAsync(client, "vocab", good, levelId, bookId, dryRun: true)).GetProperty("toCreate").GetInt32().ShouldBe(2);
        (await ImportAsync(client, "vocab", good, levelId, bookId, dryRun: false)).GetProperty("created").GetInt32().ShouldBe(2);
        (await ImportAsync(client, "vocab", good, levelId, bookId, dryRun: false)).GetProperty("skipped").GetInt32().ShouldBe(2);

        var listed = await client.JsonAsync("GET", $"/api/content/reference/vocab?levelId={levelId}", Admin);
        listed.GetProperty("totalCount").GetInt32().ShouldBe(2);
        listed.GetProperty("items")[0].GetProperty("source").GetString().ShouldBe("Uji integrasi");
        listed.GetProperty("items")[0].GetProperty("license").GetString().ShouldBe("CC BY-SA 4.0");
        // The student has no class with this level: items stay hidden.
        (await client.JsonAsync("GET", $"/api/content/reference/vocab?levelId={levelId}", Murid)).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task BR_KONTEN_01_02_05_materials_publish_time_ownership_and_links()
    {
        using var client = api.CreateClient();
        var future = await client.JsonAsync("POST", $"/api/content/chapters/{Chapter1}/materials", Sensei, new
        {
            kind = "richtext",
            title = new { id = "Bacaan nanti" },
            body = new { format = "md", id = "## Salam\n{日本|にほん}" },
            publishAt = DateTimeOffset.UtcNow.AddDays(1),
        });
        var futureId = future.GetProperty("id").GetGuid();

        var forMurid = await client.JsonAsync("GET", $"/api/content/chapters/{Chapter1}/materials", Murid);
        forMurid.EnumerateArray().Any(material => material.GetProperty("id").GetGuid() == futureId).ShouldBeFalse();
        var forSensei = await client.JsonAsync("GET", $"/api/content/chapters/{Chapter1}/materials", Sensei);
        forSensei.EnumerateArray().Any(material => material.GetProperty("id").GetGuid() == futureId).ShouldBeTrue();

        (await client.StatusAsync("POST", $"/api/content/chapters/{Chapter1}/materials", Sensei, new { kind = "link", title = new { id = "x" }, url = "http://example.com" })).ShouldBe(400);
        (await client.StatusAsync("POST", $"/api/content/chapters/{Chapter1}/materials", Sensei, new { kind = "richtext", title = new { id = "x" }, body = new { format = "md", id = "<script>x</script>" } })).ShouldBe(400);

        // Another sensei sees the material but cannot change it; the admin can.
        var otherSensei = await OtherSenseiAsync(client);
        (await client.StatusAsync("DELETE", $"/api/content/materials/{futureId}", otherSensei)).ShouldBe(403);
        var version = future.GetProperty("version").GetUInt32();
        await client.JsonAsync("PATCH", $"/api/content/materials/{futureId}", Admin, new { version, clearPublishAt = true });
        forMurid = await client.JsonAsync("GET", $"/api/content/chapters/{Chapter1}/materials", Murid);
        forMurid.EnumerateArray().Any(material => material.GetProperty("id").GetGuid() == futureId).ShouldBeTrue();
        (await client.StatusAsync("DELETE", $"/api/content/materials/{futureId}", Sensei)).ShouldBe(204);
    }

    [Fact]
    public async Task BR_FILE_tus_upload_becomes_a_file_material_with_a_signed_attachment_download()
    {
        using var client = api.CreateClient();
        var data = RandomNumberGenerator.GetBytes(2 * 1024 * 1024 + 7);

        using var create = Tus(HttpMethod.Post, "/api/content/uploads", Sensei);
        create.Headers.Add("Upload-Length", data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        create.Headers.Add("Upload-Metadata", Metadata(("filename", "bab1 latihan.pdf"), ("filetype", "application/pdf"), ("purpose", "material")));
        using var created = await client.SendAsync(create, Cancel);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = created.Headers.Location!.ToString();
        var fileId = Guid.Parse(location[(location.LastIndexOf('/') + 1)..]);

        // Two chunks; a different user cannot continue the upload.
        using (var other = Tus(HttpMethod.Head, location, Murid))
        using (var otherResponse = await client.SendAsync(other, Cancel))
        {
            otherResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await PatchAsync(client, location, 0, data[..(1024 * 1024)]);
        await PatchAsync(client, location, 1024 * 1024, data[(1024 * 1024)..]);

        var material = await client.JsonAsync("POST", $"/api/content/chapters/{Chapter1}/materials", Sensei, new { kind = "file", title = new { id = "PDF" }, fileId });
        material.GetProperty("file").GetProperty("sizeBytes").GetInt64().ShouldBe(data.Length);
        material.GetProperty("file").GetProperty("status").GetString().ShouldBe("ready");

        var link = await client.JsonAsync("GET", $"/api/content/materials/{material.GetProperty("id").GetGuid()}/download?redirect=false", Murid);
        using var download = await client.SendAsync("GET", link.GetProperty("url").GetString()!, token: null);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync(Cancel)).ShouldBe(data);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        download.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("bab1 latihan.pdf");
        download.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }

    [Fact]
    public async Task BR_FILE_01_06_oversized_or_wrong_purpose_uploads_are_refused_at_creation()
    {
        using var client = api.CreateClient();

        using var tooLarge = Tus(HttpMethod.Post, "/api/content/uploads", Sensei);
        tooLarge.Headers.Add("Upload-Length", "1073741825");
        tooLarge.Headers.Add("Upload-Metadata", Metadata(("filename", "besar.bin"), ("purpose", "material")));
        using var tooLargeResponse = await client.SendAsync(tooLarge, Cancel);
        tooLargeResponse.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);

        using var wrongRole = Tus(HttpMethod.Post, "/api/content/uploads", Murid);
        wrongRole.Headers.Add("Upload-Length", "10");
        wrongRole.Headers.Add("Upload-Metadata", Metadata(("filename", "a.pdf"), ("purpose", "material")));
        using var wrongRoleResponse = await client.SendAsync(wrongRole, Cancel);
        wrongRoleResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage Tus(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Tus-Resumable", "1.0.0");
        return request;
    }

    private static async Task PatchAsync(HttpClient client, string location, long offset, byte[] chunk)
    {
        using var request = Tus(HttpMethod.Patch, location, Sensei);
        request.Headers.Add("Upload-Offset", offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Content = new ByteArrayContent(chunk);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        using var response = await client.SendAsync(request, Cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static string Metadata(params (string Key, string Value)[] values) =>
        string.Join(',', values.Select(value => $"{value.Key} {Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Value))}"));

    private static async Task<System.Text.Json.JsonElement> ImportAsync(HttpClient client, string type, string csv, Guid levelId, Guid bookId, bool dryRun)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "data.csv" },
            { new StringContent("Uji integrasi"), "source" },
            { new StringContent("CC BY-SA 4.0"), "license" },
            { new StringContent(levelId.ToString()), "levelId" },
            { new StringContent(bookId.ToString()), "bookId" },
        };
        return await client.JsonAsync("POST", $"/api/content/reference/{type}/import?dryRun={(dryRun ? "true" : "false")}", Admin, content);
    }

    private static async Task<string> OtherSenseiAsync(HttpClient client)
    {
        var created = await client.JsonAsync("POST", "/api/identity/users", Admin, new
        {
            username = $"sensei.{Guid.NewGuid():N}"[..15],
            displayName = "Sensei Lain",
            role = "sensei",
        });
        return TestJwt.For(created.GetProperty("user").GetProperty("id").GetGuid(), Roles.Sensei);
    }
}
