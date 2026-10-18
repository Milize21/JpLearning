using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nomax.IntegrationTests.Support;

/// <summary>Request helpers shared by the module endpoint tests.</summary>
internal static class ApiCalls
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static HttpRequestMessage Request(string method, string path, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is HttpContent content)
        {
            request.Content = content;
        }
        else if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public static async Task<HttpResponseMessage> SendAsync(this HttpClient client, string method, string path, string? token, object? body = null)
    {
        using var request = Request(method, path, token, body);
        return await client.SendAsync(request, Cancel);
    }

    /// <summary>Sends and requires a success status; returns the JSON body (or default for 204).</summary>
    public static async Task<JsonElement> JsonAsync(this HttpClient client, string method, string path, string? token, object? body = null)
    {
        using var response = await client.SendAsync(method, path, token, body);
        var text = await response.Content.ReadAsStringAsync(Cancel);
        response.IsSuccessStatusCode.ShouldBeTrue($"{method} {path} answered {(int)response.StatusCode}: {text}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<int> StatusAsync(this HttpClient client, string method, string path, string? token, object? body = null)
    {
        using var response = await client.SendAsync(method, path, token, body);
        return (int)response.StatusCode;
    }
}
