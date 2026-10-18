using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nomax.Gateway;

/// <summary>
/// The Gateway is ready when the API it forwards to is alive.
/// </summary>
internal sealed class ApiHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IHealthCheck
{
    public const string HttpClientName = "api-health";
    public const string DestinationAddressKey = "ReverseProxy:Clusters:api:Destinations:api:Address";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (configuration[DestinationAddressKey] is not { Length: > 0 } address)
        {
            return HealthCheckResult.Unhealthy($"{DestinationAddressKey} is not configured.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(new Uri(new Uri(address), "health/live"), cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"The API answered {(int)response.StatusCode}.");
        }
        catch (HttpRequestException exception)
        {
            return HealthCheckResult.Unhealthy("The API is unreachable.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("The API timed out.", exception);
        }
    }
}
