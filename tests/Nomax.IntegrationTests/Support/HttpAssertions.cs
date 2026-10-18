using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Nomax.IntegrationTests.Support;

internal static class HttpAssertions
{
    public static HttpRequestMessage Get(string path, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return request;
    }

    /// <summary>Asserts an RFC 9457 response with the stable <c>code</c> of docs/05 §2 and returns its JSON.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonDocument.Parse(body).RootElement.Clone();
        problem.GetProperty("code").GetString().ShouldBe(code);
        problem.GetProperty("status").GetInt32().ShouldBe((int)status);
        problem.GetProperty("type").GetString().ShouldBe($"https://nomax.local/errors/{code}");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        return problem;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
