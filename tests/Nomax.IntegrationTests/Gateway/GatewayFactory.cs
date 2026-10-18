extern alias gateway;

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nomax.IntegrationTests.Support;
using Yarp.ReverseProxy.Forwarder;
using ApiHealthCheck = gateway::Nomax.Gateway.ApiHealthCheck;
using GatewayProgram = gateway::Program;

namespace Nomax.IntegrationTests.Gateway;

/// <summary>
/// The real Gateway with its Development configuration; the API behind it is replaced by <see cref="StubApi"/>.
/// Rate limits are lowered so the tests can reach them.
/// </summary>
public sealed class GatewayFactory : WebApplicationFactory<GatewayProgram>
{
    public const int PerUserPerMinute = 3;
    public const int IcalPerIpPerMinute = 2;

    internal StubApi Api { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:Authority"] = TestJwt.Issuer,
            ["RateLimiting:PerUserPerMinute"] = PerUserPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimiting:IcalPerIpPerMinute"] = IcalPerIpPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ReverseProxy:Clusters:api:Destinations:api:Address"] = "http://api.test/",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
        builder.ConfigureTestServices(services =>
        {
            TestJwt.TrustTestKey(services);
            services.AddSingleton<IForwarderHttpClientFactory>(new StubForwarderHttpClientFactory(Api));
            services.AddHttpClient(ApiHealthCheck.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Api);
        });
    }

    private sealed class StubForwarderHttpClientFactory(StubApi api) : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(api, disposeHandler: false);
    }
}

/// <summary>Records what the Gateway forwards and answers 200 with a small JSON echo.</summary>
internal sealed class StubApi : HttpMessageHandler
{
    public ConcurrentQueue<ForwardedRequest> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var forwarded = new ForwardedRequest(
            request.RequestUri!.AbsolutePath,
            request.RequestUri.Query,
            request.Headers.Authorization?.ToString());
        Requests.Enqueue(forwarded);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { forwarded.Path }), Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}

internal sealed record ForwardedRequest(string Path, string Query, string? Authorization);
