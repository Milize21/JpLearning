using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Nomax.Modules.Identity.Infrastructure.Keycloak;

/// <summary>
/// Client-credentials token of the <c>nomax-api</c> service account, cached until shortly before it expires.
/// </summary>
internal sealed class KeycloakAdminTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    TimeProvider timeProvider) : IDisposable
{
    public const string HttpClientName = "keycloak-token";

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        if (_token is { } cached && timeProvider.GetUtcNow() < _expiresAt)
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is { } refreshed && timeProvider.GetUtcNow() < _expiresAt)
            {
                return refreshed;
            }

            var settings = options.Value;
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.AdminClientId,
                ["client_secret"] = settings.AdminClientSecret,
            });

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(settings.TokenEndpoint, form, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new KeycloakAdminException(KeycloakFailure.Unavailable, "Keycloak token endpoint is unreachable.", exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new KeycloakAdminException(
                        KeycloakFailure.Unavailable,
                        $"Keycloak refused the nomax-api client credentials ({(int)response.StatusCode}).");
                }

                var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                    ?? throw new KeycloakAdminException(KeycloakFailure.Unavailable, "Keycloak returned an empty token response.");
                _token = token.AccessToken;
                _expiresAt = timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn) - RefreshMargin;
                return token.AccessToken;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
