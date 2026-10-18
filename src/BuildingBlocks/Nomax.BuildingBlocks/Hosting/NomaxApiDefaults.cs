using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Serialization;
using StackExchange.Redis;

namespace Nomax.BuildingBlocks.Hosting;

/// <summary>
/// Cross-cutting setup of the API host. Program.cs calls these three methods and registers the modules in between,
/// so integration tests can compose exactly the same pipeline.
/// </summary>
public static class NomaxApiDefaults
{
    /// <summary>JSON body limit (docs/05 §1). tus uploads lift it per endpoint from Phase 1.</summary>
    public const long MaxRequestBodyBytes = 1024 * 1024;

    public const string RedisConnectionStringName = "Redis";

    public static WebApplicationBuilder AddNomaxApiDefaults(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;
        var configuration = builder.Configuration;

        builder.AddNomaxSerilog();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

        services.TryAddSingleton(TimeProvider.System);
        services.ConfigureHttpJsonOptions(options => NomaxJson.Configure(options.SerializerOptions));
        services.AddNomaxProblemDetails();
        services.AddKeycloakJwtBearer(configuration);
        services.AddNomaxAuthorization();
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HttpContextCurrentUser>();

        // Requests arrive through the Gateway on the same machine; trust its X-Forwarded-* headers only.
        services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        services.AddNomaxPostgres();
        services.AddNomaxRedis();
        services.AddHttpClient(KeycloakHealthCheck.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: [HealthEndpoints.ReadyTag])
            .AddCheck<RedisHealthCheck>("valkey", tags: [HealthEndpoints.ReadyTag])
            .AddCheck<KeycloakHealthCheck>("keycloak", tags: [HealthEndpoints.ReadyTag]);

        return builder;
    }

    public static WebApplication UseNomaxApiDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseForwardedHeaders();
        app.UseNomaxRequestLogging();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseNomaxLogContext();
        app.UseMiddleware<ActiveProfileMiddleware>();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapNomaxApiDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapNomaxHealthChecks();
        app.MapNomaxOpenApi();
        return app;
    }

    /// <summary>Valkey/Redis connection (leaderboard, cache, SignalR backplane). Not a source of truth (docs/02 §7).</summary>
    public static IServiceCollection AddNomaxRedis(this IServiceCollection services)
    {
        services.AddRequiredConnectionString(RedisConnectionStringName, "Default for development: localhost:6379.");
        services.TryAddSingleton<IConnectionMultiplexer>(provider =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(RedisConnectionStringName)!;
            var options = ConfigurationOptions.Parse(connectionString);
            // Start even when Valkey is down; /health/ready reports it and the client reconnects in the background.
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        return services;
    }
}
