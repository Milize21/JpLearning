using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Nomax.Modules.Identity.Infrastructure.Keycloak;

namespace Nomax.Modules.Identity.Tests.Infrastructure;

public sealed class KeycloakAdminClientTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("6162a6c1-9dd4-4999-a25b-cb25ed5658a8");

    private readonly FakeKeycloak _keycloak = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero));
    private readonly KeycloakAdminTokenProvider _tokens;
    private readonly KeycloakAdminClient _client;
    private readonly HttpClient _http;

    public KeycloakAdminClientTests()
    {
        var options = Options.Create(new KeycloakAdminOptions
        {
            Authority = "http://keycloak.test/realms/nomax",
            AdminClientSecret = "secret",
        });
        _http = new HttpClient(_keycloak);
        _tokens = new KeycloakAdminTokenProvider(new SingleClientFactory(_keycloak), options, _clock);
        _client = new KeycloakAdminClient(_http, _tokens, options, NullLogger<KeycloakAdminClient>.Instance);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public void Admin_base_address_is_derived_from_the_realm_url()
    {
        new KeycloakAdminOptions { Authority = "http://localhost:8081/realms/nomax" }
            .AdminBaseAddress.ShouldBe(new Uri("http://localhost:8081/admin/realms/nomax/"));
    }

    [Fact]
    public async Task Create_user_requires_password_change_and_returns_the_new_id()
    {
        _keycloak.Respond(HttpMethod.Post, "/admin/realms/nomax/users", _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri($"http://keycloak.test/admin/realms/nomax/users/{UserId}");
            return response;
        });

        var id = await _client.CreateUserAsync(new NewKeycloakUser("budi", "budi@contoh.id", "Budi"), Cancel);

        id.ShouldBe(UserId);
        var body = JsonNode.Parse(_keycloak.Bodies["POST /admin/realms/nomax/users"])!;
        body["username"]!.GetValue<string>().ShouldBe("budi");
        body["firstName"]!.GetValue<string>().ShouldBe("Budi");
        body["requiredActions"]![0]!.GetValue<string>().ShouldBe("UPDATE_PASSWORD");
        _keycloak.Authorizations.ShouldAllBe(header => header == "Bearer token-1");
    }

    [Fact]
    public async Task Conflict_and_outage_are_reported_as_failure_kinds()
    {
        _keycloak.Respond(HttpMethod.Post, "/admin/realms/nomax/users", _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var conflict = await Should.ThrowAsync<KeycloakAdminException>(() =>
            _client.CreateUserAsync(new NewKeycloakUser("budi", null, "Budi"), Cancel));
        conflict.Failure.ShouldBe(KeycloakFailure.Conflict);

        _keycloak.Respond(HttpMethod.Post, "/admin/realms/nomax/users", _ => throw new HttpRequestException("connection refused"));
        var outage = await Should.ThrowAsync<KeycloakAdminException>(() =>
            _client.CreateUserAsync(new NewKeycloakUser("budi", null, "Budi"), Cancel));
        outage.Failure.ShouldBe(KeycloakFailure.Unavailable);
    }

    [Fact]
    public async Task Role_is_mapped_from_the_list_of_mappable_roles()
    {
        _keycloak.Respond(HttpMethod.Get, $"/admin/realms/nomax/users/{UserId}/role-mappings/realm/available", _ =>
            Json("""[{"id":"r-admin","name":"admin"},{"id":"r-murid","name":"murid"}]"""));
        _keycloak.Respond(HttpMethod.Post, $"/admin/realms/nomax/users/{UserId}/role-mappings/realm", _ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await _client.AssignRealmRoleAsync(UserId, "murid", Cancel);

        var mapped = JsonNode.Parse(_keycloak.Bodies[$"POST /admin/realms/nomax/users/{UserId}/role-mappings/realm"])!.AsArray();
        mapped.ShouldHaveSingleItem()!["id"]!.GetValue<string>().ShouldBe("r-murid");
    }

    [Fact]
    public async Task Update_keeps_fields_it_does_not_change()
    {
        _keycloak.Respond(HttpMethod.Get, $"/admin/realms/nomax/users/{UserId}", _ =>
            Json("""{"id":"x","username":"budi","email":"old@contoh.id","firstName":"Budi","enabled":true,"attributes":{"locale":["id"]}}"""));
        _keycloak.Respond(HttpMethod.Put, $"/admin/realms/nomax/users/{UserId}", _ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await _client.UpdateUserAsync(UserId, new KeycloakUserChanges(Enabled: false), Cancel);

        var sent = JsonNode.Parse(_keycloak.Bodies[$"PUT /admin/realms/nomax/users/{UserId}"])!;
        sent["enabled"]!.GetValue<bool>().ShouldBeFalse();
        sent["email"]!.GetValue<string>().ShouldBe("old@contoh.id");
        sent["attributes"]!["locale"]![0]!.GetValue<string>().ShouldBe("id");
    }

    [Fact]
    public async Task Service_account_token_is_cached_until_shortly_before_expiry()
    {
        _keycloak.Respond(HttpMethod.Post, $"/admin/realms/nomax/users/{UserId}/logout", _ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await _client.LogoutAsync(UserId, Cancel);
        await _client.LogoutAsync(UserId, Cancel);
        _clock.Advance(TimeSpan.FromSeconds(271));
        await _client.LogoutAsync(UserId, Cancel);

        _keycloak.TokenRequests.ShouldBe(2);
    }

    [Fact]
    public async Task Deleting_a_missing_user_is_not_an_error()
    {
        _keycloak.Respond(HttpMethod.Delete, $"/admin/realms/nomax/users/{UserId}", _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Should.NotThrowAsync(() => _client.DeleteUserAsync(UserId, Cancel));
    }

    public void Dispose()
    {
        _http.Dispose();
        _tokens.Dispose();
        _keycloak.Dispose();
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeKeycloak : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = [];

        public Dictionary<string, string> Bodies { get; } = [];

        public List<string> Authorizations { get; } = [];

        public int TokenRequests { get; private set; }

        public void Respond(HttpMethod method, string path, Func<HttpRequestMessage, HttpResponseMessage> response) =>
            _routes[$"{method} {path}"] = response;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/realms/nomax/protocol/openid-connect/token")
            {
                TokenRequests++;
                return Json($$"""{"access_token":"token-{{TokenRequests}}","expires_in":300}""");
            }

            var key = $"{request.Method} {path}";
            Authorizations.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            if (request.Content is not null)
            {
                Bodies[key] = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return _routes.TryGetValue(key, out var respond) ? respond(request) : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
