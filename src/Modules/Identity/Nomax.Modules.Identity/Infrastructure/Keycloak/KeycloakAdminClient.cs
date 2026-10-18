using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nomax.Modules.Identity.Infrastructure.Keycloak;

/// <summary>
/// Typed HttpClient for the Keycloak Admin API, authenticated as the <c>nomax-api</c> service account
/// (manage-users, view-users, query-users; docs/01 §8). Logs operations and status codes only, never user data.
/// </summary>
internal sealed partial class KeycloakAdminClient(
    HttpClient http,
    KeycloakAdminTokenProvider tokens,
    IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakAdminClient> logger) : IKeycloakAdmin
{
    private Uri AdminBase => options.Value.AdminBaseAddress;

    public async Task<Guid> CreateUserAsync(NewKeycloakUser user, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["username"] = user.Username,
            ["email"] = user.Email,
            ["emailVerified"] = user.Email is not null,
            ["enabled"] = true,
            ["firstName"] = user.DisplayName,
            ["requiredActions"] = new JsonArray("UPDATE_PASSWORD"),
        };

        using var response = await SendAsync(HttpMethod.Post, "users", body, "create user", cancellationToken);
        var location = response.Headers.Location
            ?? throw new KeycloakAdminException(KeycloakFailure.Rejected, "Keycloak did not return the location of the new user.");
        return Guid.Parse(location.Segments[^1]);
    }

    public async Task SetTemporaryPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["type"] = "password", ["value"] = password, ["temporary"] = true };
        using var _ = await SendAsync(HttpMethod.Put, $"users/{userId}/reset-password", body, "set temporary password", cancellationToken);
    }

    public async Task AssignRealmRoleAsync(Guid userId, string realmRole, CancellationToken cancellationToken)
    {
        // The service account may not read /roles (no view-realm), but it may list the roles it can map for a user.
        using var availableResponse = await SendAsync(
            HttpMethod.Get, $"users/{userId}/role-mappings/realm/available", null, "list mappable roles", cancellationToken);
        var available = await availableResponse.Content.ReadFromJsonAsync<JsonArray>(cancellationToken) ?? [];
        var role = available.FirstOrDefault(node => node?["name"]?.GetValue<string>() == realmRole)
            ?? throw new KeycloakAdminException(KeycloakFailure.Rejected, $"Realm role '{realmRole}' cannot be mapped.");

        using var _ = await SendAsync(
            HttpMethod.Post, $"users/{userId}/role-mappings/realm", new JsonArray(role.DeepClone()), "map realm role", cancellationToken);
    }

    public async Task UpdateUserAsync(Guid userId, KeycloakUserChanges changes, CancellationToken cancellationToken)
    {
        // Read-modify-write: with the declarative user profile a partial representation could drop attributes.
        using var current = await SendAsync(HttpMethod.Get, $"users/{userId}", null, "read user", cancellationToken);
        var user = await current.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new KeycloakAdminException(KeycloakFailure.NotFound, "Keycloak returned no user.");

        if (changes.Email is not null)
        {
            user["email"] = changes.Email.Length == 0 ? null : changes.Email;
            user["emailVerified"] = changes.Email.Length > 0;
        }

        if (changes.DisplayName is not null)
        {
            user["firstName"] = changes.DisplayName;
        }

        if (changes.Enabled is { } enabled)
        {
            user["enabled"] = enabled;
        }

        using var _ = await SendAsync(HttpMethod.Put, $"users/{userId}", user, "update user", cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var _ = await SendAsync(HttpMethod.Post, $"users/{userId}/logout", null, "logout user", cancellationToken);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            using var _ = await SendAsync(HttpMethod.Delete, $"users/{userId}", null, "delete user", cancellationToken);
        }
        catch (KeycloakAdminException exception) when (exception.Failure == KeycloakFailure.NotFound)
        {
            // Already gone; nothing to compensate.
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        string operation,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(AdminBase, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(cancellationToken));
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);
            }

            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            LogUnreachable(logger, operation, exception);
            throw new KeycloakAdminException(KeycloakFailure.Unavailable, "Keycloak is unreachable.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, operation, exception);
            throw new KeycloakAdminException(KeycloakFailure.Unavailable, "Keycloak timed out.", exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();
        LogFailed(logger, operation, (int)status);
        throw status switch
        {
            HttpStatusCode.Conflict => new KeycloakAdminException(KeycloakFailure.Conflict, "The username or email already exists in Keycloak."),
            HttpStatusCode.NotFound => new KeycloakAdminException(KeycloakFailure.NotFound, "The user does not exist in Keycloak."),
            HttpStatusCode.BadRequest => new KeycloakAdminException(KeycloakFailure.Rejected, "Keycloak rejected the request."),
            // 401/403 mean the nomax-api secret or service-account roles are wrong: an operator problem, not the caller's.
            _ => new KeycloakAdminException(KeycloakFailure.Unavailable, $"Keycloak answered {(int)status} to '{operation}'."),
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Keycloak admin call '{Operation}' could not reach Keycloak")]
    private static partial void LogUnreachable(ILogger logger, string operation, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Keycloak admin call '{Operation}' answered {StatusCode}")]
    private static partial void LogFailed(ILogger logger, string operation, int statusCode);
}
