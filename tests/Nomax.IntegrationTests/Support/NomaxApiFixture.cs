using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nomax.IntegrationTests.Support;
using Nomax.Modules.Identity.Infrastructure.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(NomaxApiFixture))]

namespace Nomax.IntegrationTests.Support;

/// <summary>
/// The real Nomax.Api (Program.cs) on fresh PostgreSQL and Valkey containers, started once for the test assembly.
/// Startup migrates every module from an empty database and runs the development seed, like on a developer machine.
/// </summary>
public sealed class NomaxApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("nomax")
        .Build();

    private readonly RedisContainer _valkey = new RedisBuilder("valkey/valkey:9.2")
        .Build();

    // Uploads of the tests never mix with the files of a running development API.
    private readonly string _storage = Path.Combine(Path.GetTempPath(), "nomax-tests", Guid.NewGuid().ToString("N"));

    private ApiFactory? _factory;

    internal FakeKeycloakAdmin Keycloak { get; } = new();

    internal IServiceProvider Services => Factory.Services;

    internal string PostgresConnectionString => _postgres.GetConnectionString();

    private ApiFactory Factory => _factory ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _valkey.StartAsync());
        _factory = new ApiFactory(this);
        // Creating a client builds and starts the host, which runs migrations and the seed.
        _factory.CreateClient().Dispose();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>The in-memory server, for clients that bring their own transport (SignalR).</summary>
    internal TestServer Server => Factory.Server;

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _valkey.DisposeAsync();
        if (Directory.Exists(_storage))
        {
            Directory.Delete(_storage, recursive: true);
        }
    }

    private sealed class ApiFactory(NomaxApiFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            // Added after the host's own sources (appsettings, user-secrets), so these win.
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fixture._postgres.GetConnectionString(),
                ["ConnectionStrings:Redis"] = fixture._valkey.GetConnectionString(),
                ["Keycloak:Authority"] = TestJwt.Issuer,
                ["Keycloak:AdminClientSecret"] = "not-used-by-the-fake",
                ["Database:MigrateOnStartup"] = "true",
                ["Serilog:MinimumLevel:Default"] = "Warning",
                // No mail to the developer's Mailpit and no web push from tests; notifications are checked in the database.
                ["Email:Enabled"] = "false",
                ["Storage:RootPath"] = Path.Combine(fixture._storage, "files"),
                ["Storage:UploadPath"] = Path.Combine(fixture._storage, "uploads"),
                ["WebPush:PublicKey"] = string.Empty,
                ["WebPush:PrivateKey"] = string.Empty,
            }));
            builder.ConfigureTestServices(services =>
            {
                TestJwt.TrustTestKey(services);
                services.AddSingleton<IKeycloakAdmin>(fixture.Keycloak);
            });
        }
    }
}
