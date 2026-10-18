using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Auth;
using Npgsql;
using StackExchange.Redis;

namespace Nomax.BuildingBlocks.Hosting;

/// <summary>
/// <c>/health/live</c> (process is up) and <c>/health/ready</c> (dependencies reachable), docs/02 §15.
/// </summary>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static IEndpointRouteBuilder MapNomaxHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .DisableRateLimiting();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .AllowAnonymous()
            .DisableRateLimiting();
        return endpoints;
    }
}

internal sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", exception);
        }
    }
}

internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (RedisException exception)
        {
            return HealthCheckResult.Unhealthy("Valkey/Redis is unreachable.", exception);
        }
    }
}

internal sealed class KeycloakHealthCheck(IHttpClientFactory httpClientFactory, IOptions<KeycloakOptions> keycloak) : IHealthCheck
{
    public const string HttpClientName = "keycloak-health";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var discovery = new Uri($"{keycloak.Value.Authority.TrimEnd('/')}/.well-known/openid-configuration");
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(discovery, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Keycloak answered {(int)response.StatusCode}.");
        }
        catch (HttpRequestException exception)
        {
            return HealthCheckResult.Unhealthy("Keycloak is unreachable.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Keycloak timed out.", exception);
        }
    }
}
